using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private const string PlatformEmail = "platform@cms.local";

    public SuperAdminReportsController(AppDbContext context) => _context = context;

    // These reports contain platform subscription data and staff/branch counts only.
    // They do not expose clinic billing, pharmacy/lab revenue or patient information.

    private async Task<List<ClinicSubscriptionSnapshot>> CurrentSubscriptionsAsync(DateTime now)
    {
        var records = await _context.ClinicSubscriptions.AsNoTracking()
            .Where(x => x.StartDate <= now)
            .Select(x => new ClinicSubscriptionSnapshot
            {
                Id = x.Id,
                HospitalId = x.HospitalId,
                SubscriptionPlanId = x.SubscriptionPlanId,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                SupersededBySubscriptionId = x.SupersededBySubscriptionId
            }).ToListAsync();

        // A superseded upgrade must not displace its replacement subscription.
        // Prefer the currently effective active subscription; otherwise show the
        // most recent non-superseded record for the clinic.
        return records.Where(x => x.SupersededBySubscriptionId == null)
            .GroupBy(x => x.HospitalId)
            .Select(g => g.OrderByDescending(x => x.Status == "Active" && x.EndDate > now)
                          .ThenByDescending(x => x.StartDate)
                          .ThenByDescending(x => x.Id)
                          .First())
            .ToList();
    }

    private sealed class ClinicSubscriptionSnapshot
    {
        public int Id { get; set; }
        public int HospitalId { get; set; }
        public int SubscriptionPlanId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "";
        public int? SupersededBySubscriptionId { get; set; }
    }

    private static string StatusOf(ClinicSubscriptionSnapshot x, DateTime now)
    {
        if (x.Status == "Cancelled") return "Cancelled";
        if (x.Status == "Active" && x.EndDate > now)
            return x.EndDate <= now.AddDays(30) ? "Expiring" : "Active";
        if (x.Status == "Expired" || x.EndDate <= now) return "Expired";
        return "Other";
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var now = DateTime.UtcNow;
        var current = await CurrentSubscriptionsAsync(now);
        var revenue = await _context.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid" && x.PaidAt != null)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        return Ok(new
        {
            totalClinics = await _context.Hospitals.AsNoTracking()
                .CountAsync(x => x.IsActive && x.Email != PlatformEmail),
            totalAdmins = await _context.Users.AsNoTracking()
                .CountAsync(x => x.Role == "Admin" && x.IsActive),
            activeSubscriptions = current.Count(x => StatusOf(x, now) is "Active" or "Expiring"),
            expiringSubscriptions = current.Count(x => StatusOf(x, now) == "Expiring"),
            expiredSubscriptions = current.Count(x => StatusOf(x, now) == "Expired"),
            cancelledSubscriptions = current.Count(x => StatusOf(x, now) == "Cancelled"),
            subscriptionRevenue = revenue
        });
    }

    [HttpGet("clinic-revenue")]
    public async Task<IActionResult> ClinicRevenue()
    {
        var clinics = await _context.Hospitals.AsNoTracking()
            .Where(x => x.Email != PlatformEmail)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var payments = await _context.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid" && x.PaidAt != null && x.HospitalId != null)
            .GroupBy(x => x.HospitalId)
            .Select(g => new { HospitalId = g.Key, Revenue = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync();
        return Ok(clinics.Select(c => new
        {
            clinicId = c.Id,
            clinicName = c.Name,
            subscriptionRevenue = payments.FirstOrDefault(p => p.HospitalId == c.Id)?.Revenue ?? 0m,
            paymentCount = payments.FirstOrDefault(p => p.HospitalId == c.Id)?.Count ?? 0
        }).OrderByDescending(x => x.subscriptionRevenue).ToList());
    }

    [HttpGet("subscription-status")]
    public async Task<IActionResult> SubscriptionStatus()
    {
        var now = DateTime.UtcNow;
        var current = await CurrentSubscriptionsAsync(now);
        return Ok(new
        {
            active = current.Count(x => StatusOf(x, now) == "Active"),
            expiring = current.Count(x => StatusOf(x, now) == "Expiring"),
            expired = current.Count(x => StatusOf(x, now) == "Expired"),
            cancelled = current.Count(x => StatusOf(x, now) == "Cancelled"),
            other = current.Count(x => StatusOf(x, now) == "Other")
        });
    }

    [HttpGet("revenue-trend")]
    public async Task<IActionResult> RevenueTrend()
    {
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
        var payments = await _context.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.PaymentStatus == "Paid" && x.PaidAt >= firstMonth)
            .Select(x => new { x.PaidAt, x.Amount }).ToListAsync();
        return Ok(Enumerable.Range(0, 12).Select(i =>
        {
            var month = firstMonth.AddMonths(i);
            var next = month.AddMonths(1);
            var items = payments.Where(x => x.PaidAt >= month && x.PaidAt < next).ToList();
            return new { month = month.ToString("MMM yyyy"), revenue = items.Sum(x => x.Amount), paymentCount = items.Count };
        }).ToList());
    }

    [HttpGet("branch-count")]
    public async Task<IActionResult> BranchCount()
    {
        var clinics = await _context.Hospitals.AsNoTracking()
            .Where(x => x.Email != PlatformEmail)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var counts = await _context.Branches.AsNoTracking()
            .GroupBy(x => x.HospitalId)
            .Select(g => new { HospitalId = g.Key, Total = g.Count(), Active = g.Count(x => x.IsActive) })
            .ToListAsync();
        return Ok(clinics.Select(c => new
        {
            clinicId = c.Id,
            clinicName = c.Name,
            branchCount = counts.FirstOrDefault(x => x.HospitalId == c.Id)?.Total ?? 0,
            activeBranchCount = counts.FirstOrDefault(x => x.HospitalId == c.Id)?.Active ?? 0
        }).ToList());
    }

    [HttpGet("admin-count")]
    public async Task<IActionResult> AdminCount()
    {
        var clinics = await _context.Hospitals.AsNoTracking()
            .Where(x => x.Email != PlatformEmail)
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        var counts = await _context.Users.AsNoTracking()
            .Where(x => x.Role == "Admin")
            .GroupBy(x => x.HospitalId)
            .Select(g => new { HospitalId = g.Key, Total = g.Count(), Active = g.Count(x => x.IsActive) })
            .ToListAsync();
        return Ok(clinics.Select(c => new
        {
            clinicId = c.Id,
            clinicName = c.Name,
            adminCount = counts.FirstOrDefault(x => x.HospitalId == c.Id)?.Active ?? 0,
            totalAdminCount = counts.FirstOrDefault(x => x.HospitalId == c.Id)?.Total ?? 0
        }).ToList());
    }

    [HttpGet("plan-distribution")]
    public async Task<IActionResult> PlanDistribution()
    {
        var now = DateTime.UtcNow;
        var current = (await CurrentSubscriptionsAsync(now))
            .Where(x => StatusOf(x, now) is "Active" or "Expiring").ToList();
        var plans = await _context.SubscriptionPlans.AsNoTracking()
            .Select(x => new { x.Id, x.PlanType }).ToListAsync();
        var byPlan = current.Join(plans, s => s.SubscriptionPlanId, p => p.Id,
            (s, p) => new { s.HospitalId, p.PlanType })
            .GroupBy(x => x.PlanType)
            .ToDictionary(g => g.Key, g => g.Select(x => x.HospitalId).Distinct().Count());
        var categories = new[] { "Basic", "Super", "Premium" }
            .Concat(byPlan.Keys.Where(x => x != "Basic" && x != "Super" && x != "Premium"))
            .Distinct();
        return Ok(categories.Select(type => new
        {
            planType = type,
            planName = type,
            clinicCount = byPlan.GetValueOrDefault(type, 0)
        }).ToList());
    }
}
