using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Controllers;

/// <summary>
/// Development-only payment inspection endpoints.
/// No endpoint in this controller changes a purchase's payment status.
/// Simulated payments are handled centrally by POST /api/subscriptions/payments/initiate.
/// </summary>
[ApiController]
[Route("api/subscriptions/payments")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SubscriptionPaymentTestingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public SubscriptionPaymentTestingController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET /api/subscriptions/payments/pending
    // SuperAdmin only; Development only.
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingPayments()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var purchases = await _context.SubscriptionPurchases
            .AsNoTracking()
            .Where(x => x.PaymentStatus == "Pending")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                purchaseId = x.Id,
                x.PurchaseType,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.SubscriptionPlanId,
                planName = x.SubscriptionPlan.Name,
                durationMonths = x.PurchasedDurationMonths,
                includesCms = x.PurchasedIncludesCms,
                includesLab = x.PurchasedIncludesLab,
                includesPharmacy = x.PurchasedIncludesPharmacy,
                x.Amount,
                x.PaymentMethod,
                x.PaymentStatus,
                x.AdminCreationStatus,
                x.PaymentAttemptCount,
                x.LastPaymentAttemptAt,
                x.TransactionReference,
                x.PaymentFailureReason,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new { count = purchases.Count, purchases });
    }

    // GET /api/subscriptions/payments/{purchaseId}/status
    // Anonymous lookup is supported only for local Development UI testing.
    // This endpoint is unavailable in non-Development environments.
    [AllowAnonymous]
    [HttpGet("{purchaseId:int}/status")]
    public async Task<IActionResult> GetStatus(int purchaseId)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (purchaseId <= 0)
            return BadRequest(new { message = "Valid purchase ID required." });

        // Return only the minimal information needed by the local checkout UI.
        // Never expose customer details, gateway identifiers or transaction references
        // through this anonymous endpoint.
        var purchase = await _context.SubscriptionPurchases
            .AsNoTracking()
            .Where(x => x.Id == purchaseId)
            .Select(x => new
            {
                purchaseId = x.Id,
                x.PurchaseType,
                paymentStatus = x.PaymentStatus,
                adminCreationStatus = x.AdminCreationStatus,
                paymentAttemptCount = x.PaymentAttemptCount,
                lastPaymentAttemptAt = x.LastPaymentAttemptAt,
                simulatedPayment = x.PaymentStatus == "Paid",
                realPaymentCollected = false
            })
            .FirstOrDefaultAsync();

        return purchase == null
            ? NotFound(new { message = "Purchase not found." })
            : Ok(purchase);
    }

    // POST /api/subscriptions/payments/confirm/{purchaseId}
    // Retained for compatibility; deliberately cannot mark a purchase Paid.
    [HttpPost("confirm/{purchaseId:int}")]
    public IActionResult ConfirmPayment(int purchaseId) =>
        LegacySimulationDisabled(purchaseId);

    // POST /api/subscriptions/payments/dev-simulate-success/{purchaseId}
    // Retained for compatibility; deliberately cannot mark a purchase Paid.
    [HttpPost("dev-simulate-success/{purchaseId:int}")]
    public IActionResult SimulateSuccess(int purchaseId) =>
        LegacySimulationDisabled(purchaseId);

    private IActionResult LegacySimulationDisabled(int purchaseId)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (purchaseId <= 0)
            return BadRequest(new { message = "Valid purchase ID required." });

        return StatusCode(StatusCodes.Status410Gone, new
        {
            message = "Direct payment confirmation has been retired to prevent bypassing payment tracking.",
            purchaseId,
            useEndpoint = "POST /api/subscriptions/payments/initiate",
            note = "Use the Development-only central payment initiation endpoint with its existing request DTO to simulate success or failure."
        });
    }
}

