using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/SuperAdmin")]
[Route("api/dashboard")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuperAdminController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/SuperAdmin/dashboard
    // GET /api/SuperAdmin/summary
    // Also available under /api/dashboard for existing frontend compatibility.
    [HttpGet("dashboard")]
    [HttpGet("summary")]
    public async Task<IActionResult> GetDashboard()
    {
        var now = DateTime.UtcNow;
        var nextThirtyDays = now.AddDays(30);

        var totalClinics = await _context.Hospitals
            .AsNoTracking()
            .CountAsync(x => x.Email != "platform@cms.local" && x.IsActive);

        var totalAdmins = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Admin" && x.IsActive);

        // Count clinics, not overlapping subscription rows.
        var activeSubscriptions = await _context.ClinicSubscriptions
            .AsNoTracking()
            .Where(x => x.Status == "Active" &&
                        x.StartDate <= now &&
                        x.EndDate > now)
            .Select(x => x.HospitalId)
            .Distinct()
            .CountAsync();

        // Count clinics whose current subscription expires within 30 days.
        var expiringSubscriptions = await _context.ClinicSubscriptions
            .AsNoTracking()
            .Where(x => x.Status == "Active" &&
                        x.StartDate <= now &&
                        x.EndDate > now &&
                        x.EndDate <= nextThirtyDays)
            .Select(x => x.HospitalId)
            .Distinct()
            .CountAsync();

        // Include completed payments even before subscription activation.
        var subscriptionRevenue = await _context.SubscriptionPurchases
            .AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid" && x.PaidAt != null)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        return Ok(new
        {
            totalClinics,
            clinics = totalClinics,
            totalAdmins,
            admins = totalAdmins,
            activeSubscriptions,
            expiringSubscriptions,
            subscriptionRevenue
        });
    }
}
