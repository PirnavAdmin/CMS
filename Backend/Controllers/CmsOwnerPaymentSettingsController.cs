
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

public class CreateCmsOwnerPaymentSettingsDto
{
    [Required, MaxLength(150)]
    public string MerchantName { get; set; } = "";

    [Required, RegularExpression("^(Test|Live)$")]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(200)]
    public string KeyId { get; set; } = "";

    [Required]
    public string KeySecret { get; set; } = "";

    [Required]
    public string WebhookSecret { get; set; } = "";

    [MaxLength(200)]
    public string? CredentialReference { get; set; }
}

public class UpdateCmsOwnerPaymentSettingsDto
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

    public string? KeySecret { get; set; }
    public string? WebhookSecret { get; set; }
}

[ApiController]
[Route("api/cms-owner/payment-settings")]
[Authorize(Roles = "SuperAdmin")]
public class CmsOwnerPaymentSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly RazorpayCredentialStore _credentialStore;

    public CmsOwnerPaymentSettingsController(
        AppDbContext db,
        IConfiguration configuration,
        RazorpayCredentialStore credentialStore)
    {
        _db = db;
        _configuration = configuration;
        _credentialStore = credentialStore;
    }

    // CMS owner identity must be explicitly configured.
    // The SuperAdmin role alone is insufficient.
    private bool IsOwner()
    {
        return int.TryParse(
                _configuration["CmsOwner:UserId"],
                out var ownerId)
            && ownerId > 0
            && int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId)
            && userId == ownerId
            && User.IsInRole("SuperAdmin");
    }

    private bool HasValidKeyId(string keyId, string mode)
    {
        var prefix = mode == "Test"
            ? "rzp_test_"
            : "rzp_live_";

        return keyId.Trim().StartsWith(
            prefix, StringComparison.Ordinal);
    }

    private async Task<bool> HasCredentials(string mode)
    {
        return await _db.RazorpayMerchantCredentials
            .AsNoTracking()
            .AnyAsync(x =>
                x.MerchantType == "CMS" &&
                x.HospitalId == null &&
                x.Mode == mode &&
                x.IsActive);
    }

    private void Audit(string action, string details)
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
                ?? "CMS Owner",
            Role = "SuperAdmin",
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

    // GET: api/cms-owner/payment-settings
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!IsOwner()) return Forbid();

        var setting = await _db.CmsOwnerMerchantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null)
            return Ok(new
            {
                exists = false,
                provider = "Razorpay",
                merchantName = "CMS Owner",
                mode = "Test",
                isEnabled = false,
                credentialConfigured =
                    await HasCredentials("Test")
            });

        return Ok(new
        {
            setting.Id,
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
                await HasCredentials(setting.Mode),
            setting.UpdatedAt
        });
    }

    // GET: api/cms-owner/payment-settings/credentials
    [HttpGet("credentials")]
    public async Task<IActionResult> GetCredentials()
    {
        if (!IsOwner()) return Forbid();

        var credentials =
            await _db.RazorpayMerchantCredentials
                .AsNoTracking()
                .Where(x =>
                    x.MerchantType == "CMS" &&
                    x.HospitalId == null)
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

    // POST: api/cms-owner/payment-settings
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCmsOwnerPaymentSettingsDto dto)
    {
        if (!IsOwner()) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.MerchantName))
            return BadRequest(new
            {
                message = "Merchant name is required."
            });

        if (dto.Mode is not ("Test" or "Live"))
            return BadRequest(new
            {
                message = "Invalid merchant mode."
            });

        if (string.IsNullOrWhiteSpace(dto.KeyId) ||
            string.IsNullOrWhiteSpace(dto.KeySecret) ||
            string.IsNullOrWhiteSpace(dto.WebhookSecret))
            return BadRequest(new
            {
                message = "All Razorpay credentials are required."
            });

        if (!HasValidKeyId(dto.KeyId, dto.Mode))
            return BadRequest(new
            {
                message = "KeyId does not match Test/Live mode."
            });

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var exists = await _db.CmsOwnerMerchantSettings
            .AnyAsync(x => x.Id == 1);

        if (exists)
            return Conflict(new
            {
                message = "Merchant already exists. Use PUT."
            });

        var activeExists =
            await HasCredentials(dto.Mode);

        if (activeExists)
            return Conflict(new
            {
                message = "Active CMS credentials already exist."
            });

        var now = DateTime.UtcNow;

        var setting = new CmsOwnerMerchantSetting
        {
            Id = 1,
            Provider = "Razorpay",
            MerchantName = dto.MerchantName.Trim(),
            Mode = dto.Mode,
            IsEnabled = false,
            CredentialReference =
                string.IsNullOrWhiteSpace(dto.CredentialReference)
                    ? null
                    : dto.CredentialReference.Trim(),
            UpdatedAt = now
        };

        _db.CmsOwnerMerchantSettings.Add(setting);

        _db.RazorpayMerchantCredentials.Add(
            new RazorpayMerchantCredential
            {
                MerchantType = "CMS",
                HospitalId = null,
                Mode = dto.Mode,
                KeyId = dto.KeyId.Trim(),
                EncryptedKeySecret =
                    _credentialStore.Encrypt(dto.KeySecret),
                EncryptedWebhookSecret =
                    _credentialStore.Encrypt(dto.WebhookSecret),
                IsActive = true,
                CreatedAt = now
            });

        Audit(
            "CMS Merchant Created",
            $"Mode={dto.Mode}, Enabled=false");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return StatusCode(201, new
        {
            message = "CMS merchant created successfully.",
            setting.Id,
            setting.MerchantName,
            setting.Mode,
            setting.IsEnabled,
            credentialConfigured = true
        });
    }

    // PUT: api/cms-owner/payment-settings
    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCmsOwnerPaymentSettingsDto dto)
    {
        if (!IsOwner()) return Forbid();

        if (string.IsNullOrWhiteSpace(dto.MerchantName))
            return BadRequest(new
            {
                message = "Merchant name is required."
            });

        if (dto.Mode is not ("Test" or "Live"))
            return BadRequest(new
            {
                message = "Mode must be Test or Live."
            });

        bool hasKeyId =
            !string.IsNullOrWhiteSpace(dto.KeyId);

        bool hasKeySecret =
            !string.IsNullOrWhiteSpace(dto.KeySecret);

        bool hasWebhookSecret =
            !string.IsNullOrWhiteSpace(dto.WebhookSecret);

        bool rotating =
            hasKeyId || hasKeySecret || hasWebhookSecret;

        if (rotating &&
            !(hasKeyId && hasKeySecret && hasWebhookSecret))
            return BadRequest(new
            {
                message =
                    "Supply all three credential fields together."
            });

        if (rotating &&
            !HasValidKeyId(dto.KeyId!, dto.Mode))
            return BadRequest(new
            {
                message = "Invalid Razorpay KeyId for mode."
            });

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var setting = await _db.CmsOwnerMerchantSettings
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null)
            return NotFound(new
            {
                message = "Merchant not found. Use POST."
            });

        if (dto.IsEnabled &&
            (setting.KycStatus != "Approved" ||
             string.IsNullOrWhiteSpace(
                 setting.ProviderMerchantAccountId)))
            return Conflict(new
            {
                message =
                    "Merchant KYC approval is required."
            });

        if (rotating && setting.IsEnabled)
            return Conflict(new
            {
                message =
                    "Disable merchant before rotating credentials."
            });

        if (dto.IsEnabled &&
            !rotating &&
            !await HasCredentials(dto.Mode))
            return Conflict(new
            {
                message =
                    "Active database credentials are required."
            });

        if (rotating)
        {
            var oldCredentials =
                await _db.RazorpayMerchantCredentials
                    .Where(x =>
                        x.MerchantType == "CMS" &&
                        x.HospitalId == null &&
                        x.Mode == dto.Mode &&
                        x.IsActive)
                    .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var credential in oldCredentials)
            {
                credential.IsActive = false;
                credential.RetiredAt = now;
            }

            // Required for filtered unique active index.
            if (oldCredentials.Count > 0)
                await _db.SaveChangesAsync();

            _db.RazorpayMerchantCredentials.Add(
                new RazorpayMerchantCredential
                {
                    MerchantType = "CMS",
                    HospitalId = null,
                    Mode = dto.Mode,
                    KeyId = dto.KeyId!.Trim(),
                    EncryptedKeySecret =
                        _credentialStore.Encrypt(dto.KeySecret!),
                    EncryptedWebhookSecret =
                        _credentialStore.Encrypt(dto.WebhookSecret!),
                    IsActive = true,
                    CreatedAt = now
                });
        }

        setting.MerchantName = dto.MerchantName.Trim();
        setting.Mode = dto.Mode;
        setting.IsEnabled = dto.IsEnabled;

        if (dto.CredentialReference != null)
            setting.CredentialReference =
                string.IsNullOrWhiteSpace(dto.CredentialReference)
                    ? null
                    : dto.CredentialReference.Trim();

        setting.UpdatedAt = DateTime.UtcNow;

        Audit(
            "CMS Merchant Updated",
            $"Mode={dto.Mode}, Enabled={dto.IsEnabled}, " +
            $"CredentialsRotated={rotating}");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Merchant updated successfully.",
            setting.MerchantName,
            setting.Mode,
            setting.IsEnabled,
            credentialConfigured =
                await HasCredentials(dto.Mode)
        });
    }

    // DELETE: api/cms-owner/payment-settings
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        if (!IsOwner()) return Forbid();

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var setting = await _db.CmsOwnerMerchantSettings
            .FirstOrDefaultAsync(x => x.Id == 1);

        if (setting == null)
            return NotFound(new
            {
                message = "Merchant not found."
            });

        var activeCredentials =
            await _db.RazorpayMerchantCredentials
                .Where(x =>
                    x.MerchantType == "CMS" &&
                    x.HospitalId == null &&
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

        Audit(
            "CMS Merchant Retired",
            $"ActiveCredentialsRetired={activeCredentials.Count}");

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message =
                "Merchant disabled and active credentials retired.",
            retiredCredentials = activeCredentials.Count,
            historicalRecordsPreserved = true
        });
    }
}
