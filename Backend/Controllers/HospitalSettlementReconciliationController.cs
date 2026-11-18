using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

/// <summary>
/// Hospital-scoped settlement reconciliation inspection and manual execution.
/// This controller does not capture, refund, or allocate patient payments.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/hospital-settlement-reconciliation")]
public sealed class HospitalSettlementReconciliationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HospitalSettlementReconciliationService _reconciliation;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HospitalSettlementReconciliationController> _logger;

    public HospitalSettlementReconciliationController(
        AppDbContext db,
        HospitalSettlementReconciliationService reconciliation,
        IConfiguration configuration,
        ILogger<HospitalSettlementReconciliationController> logger)
    {
        _db = db;
        _reconciliation = reconciliation;
        _configuration = configuration;
        _logger = logger;
    }

    // Revalidate both the JWT hospital claim and the current database assignment.
    // Never accept a caller-supplied HospitalId for reconciliation operations.
    private async Task<int?> GetAuthorizedHospitalIdAsync(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !int.TryParse(User.FindFirstValue("HospitalId"), out var tokenHospitalId) ||
            tokenHospitalId <= 0)
            return null;

        var user = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && x.Role == "Admin")
            .Select(x => new { x.HospitalId })
            .SingleOrDefaultAsync(cancellationToken);

        return user?.HospitalId == tokenHospitalId ? tokenHospitalId : null;
    }

    /// <summary>List this hospital's gateway settlements and reconciliation states.</summary>
    [HttpGet("settlements")]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync(cancellationToken);
        if (hospitalId is null) return Forbid();
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "page must be >= 1 and pageSize must be between 1 and 100." });

        var query = _db.HospitalGatewaySettlements.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (normalized != "Pending" && normalized != "Matched" && normalized != "Review")
                return BadRequest(new { message = "status must be Pending, Matched, or Review." });
            query = query.Where(x => x.ReconciliationStatus == normalized);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.SettledAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.SettlementId,
                x.Mode,
                x.Status,
                x.Amount,
                x.FeeAmount,
                x.TaxAmount,
                x.Utr,
                x.SettledAt,
                x.ReconciliationStatus,
                x.ReconciledAtUtc,
                x.MatchedPaymentCount,
                x.MatchedRefundCount,
                x.UnmatchedTransactionCount,
                x.SettlementDifference,
                x.RequiresManualReview,
                x.LastReconciliationAttemptAtUtc
            }).ToListAsync(cancellationToken);

        return Ok(new { page, pageSize, total, items });
    }

    /// <summary>Read the reconciliation status of a single settlement.</summary>
    [HttpGet("settlements/{settlementRecordId:long}")]
    public async Task<IActionResult> GetAsync(
        long settlementRecordId,
        CancellationToken cancellationToken)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync(cancellationToken);
        if (hospitalId is null) return Forbid();

        var settlement = await _db.HospitalGatewaySettlements.AsNoTracking()
            .Where(x => x.Id == settlementRecordId && x.HospitalId == hospitalId.Value)
            .Select(x => new
            {
                x.Id,
                x.HospitalId,
                x.Mode,
                x.SettlementId,
                x.Status,
                x.Amount,
                x.FeeAmount,
                x.TaxAmount,
                x.Utr,
                x.SettledAt,
                x.SyncedAt,
                x.ReconciliationStatus,
                x.ReconciledAtUtc,
                x.MatchedPaymentAmount,
                x.MatchedRefundAmount,
                x.ExpectedNetAmount,
                x.SettlementDifference,
                x.MatchedPaymentCount,
                x.MatchedRefundCount,
                x.UnmatchedTransactionCount,
                x.UnmatchedGatewayTransactionCount,
                x.UnmatchedLocalTransactionCount,
                x.GatewayTransactionCount,
                x.GatewayEvidenceFetchedAtUtc,
                x.LastReconciliationAttemptAtUtc,
                x.ReconciliationAttemptCount,
                x.RequiresManualReview,
                x.ReconciliationNotes,
                x.UpdatedAtUtc
            }).SingleOrDefaultAsync(cancellationToken);

        return settlement is null
            ? NotFound(new { message = "Settlement not found for this hospital." })
            : Ok(settlement);
    }

    /// <summary>Read per-payment reconciliation records for a settlement.</summary>
    [HttpGet("settlements/{settlementRecordId:long}/payments")]
    public async Task<IActionResult> GetPaymentRecordsAsync(
        long settlementRecordId,
        CancellationToken cancellationToken)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync(cancellationToken);
        if (hospitalId is null) return Forbid();

        var settlement = await _db.HospitalGatewaySettlements.AsNoTracking()
            .Where(x => x.Id == settlementRecordId && x.HospitalId == hospitalId.Value)
            .Select(x => new { x.SettlementId, x.Mode })
            .SingleOrDefaultAsync(cancellationToken);
        if (settlement is null)
            return NotFound(new { message = "Settlement not found for this hospital." });

        var records = await _db.HospitalSettlementRecords.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId.Value &&
                x.SettlementId == settlement.SettlementId && x.Mode == settlement.Mode)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.HospitalPaymentTransactionId,
                x.GatewayPaymentId,
                x.GatewaySettlementTransactionId,
                x.GatewayTransactionType,
                x.Currency,
                x.GrossAmount,
                x.FeeAmount,
                x.TaxAmount,
                x.NetAmount,
                x.GrossDifference,
                x.NetDifference,
                x.ReconciliationStatus,
                x.RequiresManualReview,
                x.ReconciledAtUtc,
                x.ReconciliationNotes
            }).ToListAsync(cancellationToken);

        return Ok(records);
    }

    /// <summary>
    /// Manually reconcile a processed settlement. Disabled by default until
    /// Razorpay settlement evidence and amount semantics have been validated.
    /// </summary>
    [HttpPost("settlements/{settlementRecordId:long}/reconcile")]
    public async Task<IActionResult> ReconcileAsync(
        long settlementRecordId,
        CancellationToken cancellationToken)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync(cancellationToken);
        if (hospitalId is null) return Forbid();

        if (!_configuration.GetValue<bool>("Payments:HospitalSettlementReconciliation:EnableManualExecution"))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Manual settlement reconciliation is disabled pending gateway evidence verification."
            });

        if (settlementRecordId <= 0)
            return BadRequest(new { message = "A valid settlement record ID is required." });

        try
        {
            var result = await _reconciliation.ReconcileAsync(
                hospitalId.Value, settlementRecordId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Settlement not found for this hospital." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Settlement reconciliation rejected for hospital {HospitalId}, settlement record {SettlementRecordId}: {Reason}",
                hospitalId.Value, settlementRecordId, ex.Message);
            return Conflict(new { message = "Settlement reconciliation could not be completed. Review gateway evidence and settlement status." });
        }
    }
}
