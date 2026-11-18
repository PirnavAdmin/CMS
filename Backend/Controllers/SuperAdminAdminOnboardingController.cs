using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/superadmin/admin-onboarding")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SuperAdminAdminOnboardingController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuperAdminAdminOnboardingController(AppDbContext db) => _db = db;

    // Read-only monitoring. Super Admin does not approve or create self-registered Admins.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var admins = await _db.Users.AsNoTracking()
            .Where(x => x.Role == "Admin")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                adminId = x.Id, x.Name, x.Email, x.MobileNumber,
                x.EmailVerified, x.IsActive, x.HospitalId, x.CreatedAt,
                clinicName = x.Hospital != null ? x.Hospital.Name : null
            }).ToListAsync();

        var ids = admins.Select(x => x.adminId).ToList();
        var purchases = await _db.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.AdminUserId.HasValue && ids.Contains(x.AdminUserId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                adminId = x.AdminUserId!.Value, purchaseId = x.Id, x.PurchaseType,
                x.PaymentStatus, x.IsGatewayPaymentVerified, x.GatewayMode,
                x.Amount, x.SubscriptionPlanId, planName = x.SubscriptionPlan.Name,
                x.PurchasedDurationMonths, x.PurchasedIncludesCms,
                x.PurchasedIncludesLab, x.PurchasedIncludesPharmacy,
                x.PurchasedMaxBranches, x.CreatedAt, x.PaidAt, x.ActivatedAt
            }).ToListAsync();

        var latestByAdmin = purchases.GroupBy(x => x.adminId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = admins.Select(a => new
        {
            a.adminId, a.Name, a.Email, a.MobileNumber, a.EmailVerified,
            a.IsActive, a.HospitalId, a.clinicName, a.CreatedAt,
            purchase = latestByAdmin.TryGetValue(a.adminId, out var purchase) ? purchase : null
        });

        return Ok(new { totalCount = admins.Count, admins = result });
    }

    [HttpGet("{adminId:int}")]
    public async Task<IActionResult> GetById(int adminId)
    {
        if (adminId <= 0) return BadRequest(new { message = "Valid Admin ID is required." });
        var admin = await _db.Users.AsNoTracking()
            .Where(x => x.Id == adminId && x.Role == "Admin")
            .Select(x => new
            {
                adminId = x.Id, x.Name, x.Email, x.MobileNumber,
                x.EmailVerified, x.IsActive, x.HospitalId, x.CreatedAt,
                clinicName = x.Hospital != null ? x.Hospital.Name : null
            }).FirstOrDefaultAsync();
        if (admin == null) return NotFound(new { message = "Admin not found." });
        var purchases = await _db.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.AdminUserId == adminId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                purchaseId = x.Id, x.PurchaseType, x.PaymentStatus,
                x.IsGatewayPaymentVerified, x.GatewayMode, x.GatewayOrderId,
                x.GatewayPaymentId, x.Amount, x.SubscriptionPlanId,
                planName = x.SubscriptionPlan.Name, x.PurchasedDurationMonths,
                x.PurchasedIncludesCms, x.PurchasedIncludesLab,
                x.PurchasedIncludesPharmacy, x.PurchasedMaxBranches,
                x.CreatedAt, x.PaidAt, x.ActivatedAt
            }).ToListAsync();
        return Ok(new { admin, purchases });
    }
}
