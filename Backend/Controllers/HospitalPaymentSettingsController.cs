
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public class CreateHospitalPaymentSettingsDto
{
    [Required, MaxLength(150)]
    public string MerchantName { get; set; } = "";

    [Required, RegularExpression("^(Test|Live)$")]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(200)]
    public string KeyId { get; set; } = "";

    [Required, MaxLength(4096)]
    public string KeySecret { get; set; } = "";

    [Required, MaxLength(4096)]
    public string WebhookSecret { get; set; } = "";

    [MaxLength(200)]
    public string? CredentialReference { get; set; }
}

public class UpdateHospitalPaymentSettingsDto
{
    [Required, MaxLength(150)]
    public string MerchantName { get; set; } = "";

    [Required, RegularExpression("^(Test|Live)$")]
    public string Mode { get; set; } = "Test";

    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string? CredentialReference { get; set; }

    [MaxLength(200)]
    public string? KeyId { get; set; }

    [MaxLength(4096)]
    public string? KeySecret { get; set; }

    [MaxLength(4096)]
    public string? WebhookSecret { get; set; }
}

[ApiController]
[Route("api/hospital/payment-settings")]
[Authorize(Roles = "Admin")]
public class HospitalPaymentSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly RazorpayCredentialStore _credentialStore;

    public HospitalPaymentSettingsController(
        AppDbContext db,
        IConfiguration configuration,
        RazorpayCredentialStore credentialStore)
    {
        _db = db;
        _configuration = configuration;
        _credentialStore = credentialStore;
    }

    // Hospital ownership must be verified separately
    // from the Admin role.
    // Configure:
    // MerchantOwners:Hospitals:<hospitalId>:UserId
    private async Task<int?> GetOwnedHospitalIdAsync()
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
            return null;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null ||
            !user.IsActive ||
            user.Role != "Admin")
            return null;

        // Convert handles both int and int? HospitalId.
        var hospitalId = Convert.ToInt32(user.HospitalId);

        if (hospitalId <= 0)
            return null;

        var ownerKey =
            $"MerchantOwners:Hospitals:{hospitalId}:UserId";

        if (!int.TryParse(
            _configuration[ownerKey], out var ownerId))
            return null;

        return ownerId == userId ? hospitalId : null;
    }

    private static bool IsValidKey(
        string keyId, string mode)
    {
        var prefix = mode == "Test"
            ? "rzp_test_"
            : "rzp_live_";

        return keyId.Trim().StartsWith(
            prefix, StringComparison.Ordinal);
    }

    private Task<bool> HasCredentialsAsync(
        int hospitalId, string mode)
    {
        return _db.RazorpayMerchantCredentials
            .AsNoTracking()
            .AnyAsync(x =>
                x.MerchantType == "Hospital" &&
                x.HospitalId == hospitalId &&
                x.Mode == mode &&
                x.IsActive &&
                x.KeyId != "" &&
                x.EncryptedKeySecret != "" &&
                x.EncryptedWebhookSecret != "");
    }

    private void AddAudit(
        int hospitalId,
        string action,
        string details)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var actorId))
            return;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            UserName =
                User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "Hospital Owner",
            Role = "Admin",
            ClinicId = hospitalId,
            Action = action,
            SystemAction = details,
            IsLoginActivity = false,
            IpAddress = HttpContext.Connection
                .RemoteIpAddress?.ToString(),
            Browser = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            LoginTime = DateTime.UtcNow
        });
    }

    // =========================================
    // GET - VIEW MERCHANT DETAILS
    // =========================================

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var hospitalId = await GetOwnedHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        var setting = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.HospitalId == hospitalId.Value);

        if (setting == null)
        {
            return Ok(new
            {
                exists = false,
                hospitalId = hospitalId.Value,
                provider = "Razorpay",
                mode = "Test",
                isEnabled = false,
                kycStatus = "NotStarted",
                credentialConfigured =
                    await HasCredentialsAsync(
                        hospitalId.Value, "Test")
            });
        }

        return Ok(new
        {
            exists = true,
            setting.HospitalId,
            setting.Provider,
            setting.MerchantName,
            setting.Mode,
            setting.IsEnabled,
            setting.KycStatus,
            setting.ProviderMerchantAccountId,
            setting.KycSubmittedAt,
            setting.KycReviewedAt,
            setting.CredentialReference,
            credentialConfigured =
                await HasCredentialsAsync(
                    hospitalId.Value, setting.Mode),
            setting.UpdatedAt
        });
    }

    // =========================================
    // GET - VIEW CREDENTIAL HISTORY
    // Secrets are never returned.
    // =========================================

    [HttpGet("credentials")]
    public async Task<IActionResult> GetCredentials()
    {
        var hospitalId = await GetOwnedHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        var credentials =
            await _db.RazorpayMerchantCredentials
                .AsNoTracking()
                .Where(x =>
                    x.MerchantType == "Hospital" &&
                    x.HospitalId == hospitalId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.Mode,
                    x.KeyId,
                    x.IsActive,
                    x.CreatedAt,
                    x.RetiredAt
                })
                .ToListAsync();

        return Ok(credentials);
    }

    // =========================================
    // POST - CREATE MERCHANT
    // =========================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHospitalPaymentSettingsDto dto)
    {
        var hospitalId = await GetOwnedHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.MerchantName) ||
            string.IsNullOrWhiteSpace(dto.KeyId) ||
            string.IsNullOrWhiteSpace(dto.KeySecret) ||
            string.IsNullOrWhiteSpace(dto.WebhookSecret))
        {
            return BadRequest(new
            {
                message =
                    "Merchant name and all credentials are required."
            });
        }

        if (!IsValidKey(dto.KeyId, dto.Mode))
        {
            return BadRequest(new
            {
                message =
                    "KeyId does not match the selected mode."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var exists = await _db.HospitalMerchantSettings
            .AnyAsync(x =>
                x.HospitalId == hospitalId.Value);

        if (exists)
        {
            return Conflict(new
            {
                message =
                    "Merchant already exists. Use PUT."
            });
        }

        var activeExists =
            await HasCredentialsAsync(
                hospitalId.Value, dto.Mode);

        if (activeExists)
        {
            return Conflict(new
            {
                message =
                    "Active credentials already exist."
            });
        }

        var now = DateTime.UtcNow;

        _db.HospitalMerchantSettings.Add(
            new HospitalMerchantSetting
            {
                HospitalId = hospitalId.Value,
                Provider = "Razorpay",
                MerchantName = dto.MerchantName.Trim(),
                Mode = dto.Mode,
                IsEnabled = false,
                CredentialReference =
                    string.IsNullOrWhiteSpace(
                        dto.CredentialReference)
                        ? null
                        : dto.CredentialReference.Trim(),
                UpdatedAt = now
            });

        _db.RazorpayMerchantCredentials.Add(
            new RazorpayMerchantCredential
            {
                MerchantType = "Hospital",
                HospitalId = hospitalId.Value,
                Mode = dto.Mode,
                KeyId = dto.KeyId.Trim(),
                EncryptedKeySecret =
                    _credentialStore.Encrypt(
                        dto.KeySecret),
                EncryptedWebhookSecret =
                    _credentialStore.Encrypt(
                        dto.WebhookSecret),
                IsActive = true,
                CreatedAt = now
            });

        AddAudit(
            hospitalId.Value,
            "Hospital Merchant Created",
            $"Mode={dto.Mode}; Enabled=false");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return StatusCode(201, new
        {
            message =
                "Hospital merchant created successfully.",
            hospitalId = hospitalId.Value,
            merchantName = dto.MerchantName.Trim(),
            dto.Mode,
            isEnabled = false,
            credentialConfigured = true
        });
    }

    // =========================================
    // PUT - UPDATE MERCHANT
    // =========================================

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateHospitalPaymentSettingsDto dto)
    {
        var hospitalId = await GetOwnedHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.MerchantName))
        {
            return BadRequest(new
            {
                message = "Merchant name is required."
            });
        }

        bool hasKey =
            !string.IsNullOrWhiteSpace(dto.KeyId);
        bool hasSecret =
            !string.IsNullOrWhiteSpace(dto.KeySecret);
        bool hasWebhook =
            !string.IsNullOrWhiteSpace(dto.WebhookSecret);

        bool any = hasKey || hasSecret || hasWebhook;
        bool all = hasKey && hasSecret && hasWebhook;

        if (any && !all)
        {
            return BadRequest(new
            {
                message =
                    "Provide all three credentials together."
            });
        }

        if (all && !IsValidKey(dto.KeyId!, dto.Mode))
        {
            return BadRequest(new
            {
                message =
                    "KeyId does not match the selected mode."
            });
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var setting = await _db.HospitalMerchantSettings
            .FirstOrDefaultAsync(
                x => x.HospitalId == hospitalId.Value);

        if (setting == null)
        {
            return NotFound(new
            {
                message = "Merchant not found. Use POST."
            });
        }

        if (dto.IsEnabled &&
            (setting.KycStatus != "Approved" ||
             string.IsNullOrWhiteSpace(
                 setting.ProviderMerchantAccountId)))
        {
            return Conflict(new
            {
                message =
                    "Approved merchant KYC is required."
            });
        }

        if (all && setting.IsEnabled)
        {
            return Conflict(new
            {
                message =
                    "Disable merchant before rotating credentials."
            });
        }

        if (dto.IsEnabled &&
            !all &&
            !await HasCredentialsAsync(
                hospitalId.Value, dto.Mode))
        {
            return Conflict(new
            {
                message =
                    "Active database credentials are required."
            });
        }

        if (all)
        {
            var oldCredentials =
                await _db.RazorpayMerchantCredentials
                    .Where(x =>
                        x.MerchantType == "Hospital" &&
                        x.HospitalId == hospitalId.Value &&
                        x.Mode == dto.Mode &&
                        x.IsActive)
                    .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var credential in oldCredentials)
            {
                credential.IsActive = false;
                credential.RetiredAt = now;
            }

            if (oldCredentials.Count > 0)
                await _db.SaveChangesAsync();

            _db.RazorpayMerchantCredentials.Add(
                new RazorpayMerchantCredential
                {
                    MerchantType = "Hospital",
                    HospitalId = hospitalId.Value,
                    Mode = dto.Mode,
                    KeyId = dto.KeyId!.Trim(),
                    EncryptedKeySecret =
                        _credentialStore.Encrypt(
                            dto.KeySecret!),
                    EncryptedWebhookSecret =
                        _credentialStore.Encrypt(
                            dto.WebhookSecret!),
                    IsActive = true,
                    CreatedAt = now
                });
        }

        setting.MerchantName = dto.MerchantName.Trim();
        setting.Mode = dto.Mode;
        setting.IsEnabled = dto.IsEnabled;

        if (dto.CredentialReference != null)
        {
            setting.CredentialReference =
                string.IsNullOrWhiteSpace(
                    dto.CredentialReference)
                    ? null
                    : dto.CredentialReference.Trim();
        }

        setting.UpdatedAt = DateTime.UtcNow;

        AddAudit(
            hospitalId.Value,
            "Hospital Merchant Updated",
            $"Mode={dto.Mode}; Enabled={dto.IsEnabled}; " +
            $"CredentialsRotated={all}");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message =
                "Hospital merchant updated successfully.",
            setting.HospitalId,
            setting.MerchantName,
            setting.Mode,
            setting.IsEnabled,
            credentialConfigured =
                await HasCredentialsAsync(
                    hospitalId.Value, dto.Mode)
        });
    }

    // =========================================
    // DELETE - RETIRE MERCHANT
    // =========================================

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var hospitalId = await GetOwnedHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var setting = await _db.HospitalMerchantSettings
            .FirstOrDefaultAsync(
                x => x.HospitalId == hospitalId.Value);

        if (setting == null)
        {
            return NotFound(new
            {
                message = "Merchant not found."
            });
        }

        var activeCredentials =
            await _db.RazorpayMerchantCredentials
                .Where(x =>
                    x.MerchantType == "Hospital" &&
                    x.HospitalId == hospitalId.Value &&
                    x.IsActive)
                .ToListAsync();

        var now = DateTime.UtcNow;

        setting.IsEnabled = false;
        setting.UpdatedAt = now;

        foreach (var credential in activeCredentials)
        {
            credential.IsActive = false;
            credential.RetiredAt = now;
        }

        AddAudit(
            hospitalId.Value,
            "Hospital Merchant Retired",
            $"CredentialsRetired={activeCredentials.Count}");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message =
                "Merchant disabled and credentials retired.",
            retiredCredentials = activeCredentials.Count,
            historicalRecordsPreserved = true
        });
    }
}
