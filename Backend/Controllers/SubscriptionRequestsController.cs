using AuthDemo.Data;
using AuthDemo.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/subscription-requests")]
public class SubscriptionRequestsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SubscriptionRequestsController(AppDbContext context)
    {
        _context = context;
    }

    // Retired legacy write endpoint. Historical records are retained read-only.
    // New customers must use POST /api/subscriptions/checkout.
    [AllowAnonymous]
    [HttpPost]
    public IActionResult Create([FromBody] CreateSubscriptionRequestDto? dto)
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            message = "This legacy subscription request endpoint is retired. Create purchases through the subscription checkout flow.",
            legacyEndpoint = "/api/subscription-requests",
            checkoutEndpoint = "/api/subscriptions/checkout",
            plansEndpoint = "/api/subscriptions/public/plans",
            requiresCheckout = true,
            paymentStatusVerified = false
        });
    }

    // Historical records only; Status/PaidAt here must not authorize onboarding.
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        if (status?.Length > 50)
            return BadRequest(new { message = "Status filter must be 50 characters or fewer." });

        var query = _context.SubscriptionRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var requests = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.SubscriptionPlanId,
                x.PlanName,
                x.Amount,
                x.DurationMonths,
                x.IncludesCms,
                x.IncludesLab,
                x.IncludesPharmacy,
                x.Status,
                x.CreatedAt,
                x.PaidAt,
                x.AdminUserId,
                x.HospitalId
            })
            .ToListAsync();

        return Ok(requests);
    }

    // Historical request details; not proof of a verified payment.
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "Valid subscription request ID is required." });

        var request = await _context.SubscriptionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return request == null
            ? NotFound(new { message = "Subscription request not found." })
            : Ok(request);
    }
}

