using System.Security.Claims;
using System;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public sealed class ReviewMerchantOnboardingDto
{
    public string Status { get; set; } = "UnderReview";
    public string? ProviderMerchantAccountId { get; set; }
    public string? Notes { get; set; }
}

[ApiController]
[Route("api/hospital/payment-onboarding")]
[Authorize(Roles = "Admin")]
public sealed class HospitalPaymentOnboardingController : ControllerBase
{
    private readonly AppDbContext _db;
    public HospitalPaymentOnboardingController(AppDbContext db) => _db = db;
    private int? HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!HospitalId.HasValue) return Forbid();
        var setting = await _db.HospitalMerchantSettings.AsNoTracking().SingleOrDefaultAsync(x => x.HospitalId == HospitalId.Value);
        return Ok(setting == null ? new { status = "NotStarted", providerMerchantAccountId = (string?)null, notes = (string?)null, submittedAt = (DateTime?)null, reviewedAt = (DateTime?)null } : new { status = setting.KycStatus, providerMerchantAccountId = setting.ProviderMerchantAccountId, notes = setting.KycNotes, submittedAt = setting.KycSubmittedAt, reviewedAt = setting.KycReviewedAt });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit()
    {
        if (!HospitalId.HasValue) return Forbid();
        var setting = await _db.HospitalMerchantSettings.SingleOrDefaultAsync(x => x.HospitalId == HospitalId.Value);
        if (setting == null) return Conflict(new { message = "Save hospital Razorpay merchant settings before submitting onboarding." });
        if (setting.KycStatus == "Approved") return Conflict(new { message = "Merchant is already approved." });
        setting.KycStatus = "Submitted";
        setting.KycSubmittedAt = DateTime.UtcNow;
        setting.KycNotes = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            _db.AuditLogs.Add(new AuditLog { UserId = actorId, UserName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin", Role = "Admin", ClinicId = HospitalId.Value, Action = "Hospital Payment Merchant Onboarding Submitted", SystemAction = "Hospital merchant KYC/onboarding submitted for Super Admin review.", IsLoginActivity = false, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), Browser = Request.Headers.UserAgent.ToString(), Timestamp = DateTime.UtcNow, LoginTime = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Merchant onboarding submitted for Super Admin review. Razorpay KYC approval still occurs outside this application.", status = setting.KycStatus, submittedAt = setting.KycSubmittedAt });
    }
}

[ApiController]
[Route("api/platform/payment-onboarding")]
[Authorize(Roles = "SuperAdmin")]
public sealed class PlatformPaymentOnboardingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    public PlatformPaymentOnboardingController(AppDbContext db, IConfiguration configuration) { _db = db; _configuration = configuration; }

    private void AddAudit(string action, string detail, int? hospitalId = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "SuperAdmin",
            Role = "SuperAdmin",
            ClinicId = hospitalId,
            Action = action,
            SystemAction = detail,
            IsLoginActivity = false,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Browser = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            LoginTime = DateTime.UtcNow
        });
    }

    [HttpGet("hospitals")]
    public async Task<IActionResult> Hospitals()
    {
        var rows = await _db.HospitalMerchantSettings.AsNoTracking().OrderBy(x => x.HospitalId)
            .Select(x => new { x.HospitalId, x.MerchantName, x.Mode, x.IsEnabled, x.KycStatus, x.ProviderMerchantAccountId, x.KycNotes, x.KycSubmittedAt, x.KycReviewedAt }).ToListAsync();
        return Ok(rows);
    }

    [HttpPut("hospitals/{hospitalId:int}")]
    public async Task<IActionResult> ReviewHospital(int hospitalId, [FromBody] ReviewMerchantOnboardingDto dto)
    {
        if (dto.Status is not ("NotStarted" or "Submitted" or "UnderReview" or "Approved" or "Rejected")) return BadRequest(new { message = "Invalid onboarding status." });
        var setting = await _db.HospitalMerchantSettings.SingleOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (setting == null) return NotFound(new { message = "Hospital merchant settings not found." });
        var accountId = dto.ProviderMerchantAccountId?.Trim();
        if (dto.Status == "Approved")
        {
            var prefix = $"Payments:Hospitals:{hospitalId}:Razorpay:{setting.Mode}:";
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(_configuration[prefix + "KeyId"]) || string.IsNullOrWhiteSpace(_configuration[prefix + "KeySecret"]) || string.IsNullOrWhiteSpace(_configuration[prefix + "WebhookSecret"]))
                return Conflict(new { message = "Approval requires the Razorpay merchant account ID and complete backend credentials for the selected mode. Razorpay approval itself must be confirmed externally." });
        }
        setting.KycStatus = dto.Status; setting.ProviderMerchantAccountId = accountId; setting.KycNotes = dto.Notes?.Trim(); setting.KycReviewedAt = DateTime.UtcNow;
        if (dto.Status == "Submitted" && !setting.KycSubmittedAt.HasValue) setting.KycSubmittedAt = DateTime.UtcNow;
        if (dto.Status != "Approved") setting.IsEnabled = false;
        AddAudit("Hospital Payment Merchant KYC Review", $"Hospital {hospitalId}: status changed to {dto.Status}; provider account ID {(string.IsNullOrWhiteSpace(accountId) ? "not supplied" : "recorded")}.", hospitalId);
        await _db.SaveChangesAsync();
        return Ok(new { setting.HospitalId, setting.KycStatus, setting.ProviderMerchantAccountId, setting.KycReviewedAt, setting.IsEnabled });
    }

    [HttpGet("cms-owner")]
    public async Task<IActionResult> CmsOwner()
    {
        var x = await _db.CmsOwnerMerchantSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1);
        if (x == null) return Ok(new { status = "NotStarted", providerMerchantAccountId = (string?)null, notes = (string?)null });
        return Ok(new { status = x.KycStatus, providerMerchantAccountId = x.ProviderMerchantAccountId, notes = x.KycNotes, submittedAt = x.KycSubmittedAt, reviewedAt = x.KycReviewedAt, x.Mode, x.IsEnabled });
    }

    [HttpPut("cms-owner")]
    public async Task<IActionResult> ReviewCmsOwner([FromBody] ReviewMerchantOnboardingDto dto)
    {
        if (dto.Status is not ("NotStarted" or "Submitted" or "UnderReview" or "Approved" or "Rejected")) return BadRequest(new { message = "Invalid onboarding status." });
        var setting = await _db.CmsOwnerMerchantSettings.SingleOrDefaultAsync(x => x.Id == 1);
        if (setting == null) { setting = new CmsOwnerMerchantSetting { Id = 1 }; _db.CmsOwnerMerchantSettings.Add(setting); }
        var accountId = dto.ProviderMerchantAccountId?.Trim();
        if (dto.Status == "Approved")
        {
            var prefix = $"Payments:CmsOwner:Razorpay:{setting.Mode}:";
            if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(_configuration[prefix + "KeyId"]) || string.IsNullOrWhiteSpace(_configuration[prefix + "KeySecret"]) || string.IsNullOrWhiteSpace(_configuration[prefix + "WebhookSecret"]))
                return Conflict(new { message = "Approval requires the Razorpay merchant account ID and complete backend credentials for the selected mode. Razorpay approval itself must be confirmed externally." });
        }
        setting.KycStatus = dto.Status; setting.ProviderMerchantAccountId = accountId; setting.KycNotes = dto.Notes?.Trim(); setting.KycReviewedAt = DateTime.UtcNow;
        if (dto.Status == "Submitted" && !setting.KycSubmittedAt.HasValue) setting.KycSubmittedAt = DateTime.UtcNow;
        if (dto.Status != "Approved") setting.IsEnabled = false;
        AddAudit("CMS Owner Payment Merchant KYC Review", $"CMS Owner merchant status changed to {dto.Status}; provider account ID {(string.IsNullOrWhiteSpace(accountId) ? "not supplied" : "recorded")}.");
        await _db.SaveChangesAsync();
        return Ok(new { setting.KycStatus, setting.ProviderMerchantAccountId, setting.KycReviewedAt, setting.IsEnabled });
    }
}
