using System.Data;
using System.Security.Claims;
using System.Text.Json;
using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;



[ApiController]
[Route("api/hospital-payments")]
[Authorize(Roles = "Receptionist,Admin,Pharmacist")]
[RequirePermission("Billing")]
public sealed class HospitalPaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;
    private readonly HospitalPaymentOrderService _orderService;
    private readonly HospitalPaymentAllocationService _allocationService;
    private readonly HospitalRefundReconciliationService _refundReconciliation;
    private readonly ILogger<HospitalPaymentsController> _logger;

    public HospitalPaymentsController(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway,
        HospitalPaymentOrderService orderService,
        HospitalPaymentAllocationService allocationService,
        HospitalRefundReconciliationService refundReconciliation,
        ILogger<HospitalPaymentsController> logger)
    {
        _db = db;
        _gateway = gateway;
        _orderService = orderService;
        _allocationService = allocationService;
        _refundReconciliation = refundReconciliation;
        _logger = logger;
    }

    private int HospitalId =>
        int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    private int? BranchId =>
        int.TryParse(User.FindFirstValue("BranchId"), out var id) ? id : null;

    private bool BranchAllowed(int? billBranch) =>
        User.IsInRole("Admin") || (BranchId.HasValue && BranchId == billBranch);

    private bool CanAccessBill(Billing bill) =>
        BranchAllowed(bill.BranchId)
        && (!User.IsInRole("Pharmacist")
            || string.Equals(bill.BillingType, "Pharmacy", StringComparison.OrdinalIgnoreCase));

    private void AddHospitalPaymentAudit(string action, string detail)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "Admin",
            Role = "Admin",
            ClinicId = HospitalId,
            Action = action,
            SystemAction = detail,
            IsLoginActivity = false,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Browser = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            LoginTime = DateTime.UtcNow
        });
    }

    // Resolve the exact merchant for an already captured payment. Never fall back
    // to the hospital's default credential when the original source is inconsistent.
    private async Task<int?> CredentialForTransactionAsync(HospitalPaymentTransaction payment)
    {
        if (payment.PaymentSource == "Order" && payment.HospitalPaymentOrderId.HasValue
            && !payment.HospitalPaymentQrId.HasValue)
        {
            var order = await _db.HospitalPaymentOrders.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == payment.HospitalPaymentOrderId.Value && x.HospitalId == payment.HospitalId
                && x.Mode == payment.Mode && x.BillingId == payment.BillingId);
            if (order == null || order.GatewayPaymentId != payment.GatewayPaymentId
                || order.GatewayOrderId != payment.GatewayOrderId)
                throw new InvalidOperationException("Original Razorpay order identity is inconsistent.");
            return order.RazorpayCredentialId;
        }
        if (payment.PaymentSource == "Qr" && payment.HospitalPaymentQrId.HasValue
            && !payment.HospitalPaymentOrderId.HasValue)
        {
            var qr = await _db.HospitalPaymentQrs.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == payment.HospitalPaymentQrId.Value && x.HospitalId == payment.HospitalId
                && x.Mode == payment.Mode && x.BillingId == payment.BillingId);
            if (qr == null || qr.GatewayPaymentId != payment.GatewayPaymentId || qr.Amount != payment.Amount)
                throw new InvalidOperationException("Original Razorpay QR identity is inconsistent.");
            return qr.RazorpayCredentialId;
        }
        throw new InvalidOperationException("Payment source is inconsistent.");
    }

    private async Task<decimal> PaidBalanceAsync(int billingId)
    {
        var manualAndGateway = await _db.BillingPaymentTransactions
            .AsNoTracking()
            .Where(x => x.BillingId == billingId
                && x.HospitalId == HospitalId
                && x.Status == "Paid")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var refunds = await _db.HospitalPaymentRefunds
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.BillingId == billingId
                && x.Status == "processed")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        return Math.Max(0m, manualAndGateway - refunds);
    }

    // Read-only discovery of payments with a known gateway payment ID but no
    // captured local allocation. This does not call Razorpay or change invoices.
    // Payments without a stored gateway payment ID cannot be safely discovered
    // from local data alone and require a separate merchant-scoped gateway scan.
    [HttpGet("reconciliation/candidates")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetReconciliationCandidates(
        [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (HospitalId <= 0) return Forbid();
        if (limit is < 1 or > 100)
            return BadRequest(new { message = "Limit must be between 1 and 100." });

        var orderCandidates = await _db.HospitalPaymentOrders.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayOrderId != null && x.GatewayOrderId != ""
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.HospitalPaymentOrderId == x.Id
                    && t.GatewayPaymentId == x.GatewayPaymentId && t.Status == "Captured"))
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new { x.Id, x.BillingId, x.GatewayPaymentId, x.Amount, x.Mode, x.CreatedAt })
            .ToListAsync(cancellationToken);

        var qrCandidates = await _db.HospitalPaymentQrs.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayQrId != null && x.GatewayQrId != ""
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.HospitalPaymentQrId == x.Id
                    && t.GatewayPaymentId == x.GatewayPaymentId && t.Status == "Captured"))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => new { x.Id, x.BillingId, x.GatewayPaymentId, x.Amount, x.Mode, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var candidates = orderCandidates.Select(x => new {
            paymentSource = "Order",
            orderId = (long?)x.Id,
            qrId = (long?)null,
            x.BillingId,
            x.GatewayPaymentId,
            x.Amount,
            x.Mode,
            createdAtUtc = x.CreatedAt
        })
            .Concat(qrCandidates.Select(x => new {
                paymentSource = "Qr",
                orderId = (long?)null,
                qrId = (long?)x.Id,
                x.BillingId,
                x.GatewayPaymentId,
                x.Amount,
                x.Mode,
                createdAtUtc = x.CreatedAtUtc
            }))
            .OrderBy(x => x.createdAtUtc)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            total = candidates.Count,
            candidates,
            note = "Local candidates only; not proof of capture. Verify each payment using reconciliation/run or reconciliation/batch. Payments without a known gateway payment ID are not included."
        });
    }

    // Read-only operational summary. These counts are local candidates, not
    // confirmation that Razorpay captured funds or that recovery will succeed.
    [HttpGet("reconciliation/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetReconciliationSummary(
        [FromQuery] int minimumAgeMinutes = 10,
        CancellationToken cancellationToken = default)
    {
        if (HospitalId <= 0) return Forbid();
        if (minimumAgeMinutes is < 1 or > 1440)
            return BadRequest(new { message = "minimumAgeMinutes must be between 1 and 1440." });

        var cutoffUtc = DateTime.UtcNow.AddMinutes(-minimumAgeMinutes);
        var pendingOrders = await _db.HospitalPaymentOrders.AsNoTracking()
            .CountAsync(x => x.HospitalId == HospitalId
                && x.CreatedAt <= cutoffUtc
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayOrderId != null && x.GatewayOrderId != ""
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.GatewayPaymentId == x.GatewayPaymentId
                    && t.Status == "Captured"), cancellationToken);
        var pendingQr = await _db.HospitalPaymentQrs.AsNoTracking()
            .CountAsync(x => x.HospitalId == HospitalId
                && x.CreatedAtUtc <= cutoffUtc
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayQrId != null && x.GatewayQrId != ""
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.GatewayPaymentId == x.GatewayPaymentId
                    && t.Status == "Captured"), cancellationToken);

        return Ok(new
        {
            hospitalId = HospitalId,
            generatedAtUtc = DateTime.UtcNow,
            minimumAgeMinutes,
            localOrderCandidates = pendingOrders,
            localQrCandidates = pendingQr,
            totalLocalCandidates = pendingOrders + pendingQr,
            automatedSchedulerEnabled = false,
            note = "Local records only. Counts may include conflicts and are not proof of Razorpay capture. No payments were changed."
        });
    }

    // Operator-triggered bounded discovery + verification. No untrusted payment IDs
    // are accepted from the request; all candidates come from this hospital's DB.
    // This is intentionally NOT a background job: automated retry requires a
    // distributed lease, durable checkpoints, and gateway rate-limit controls.
    [HttpPost("reconciliation/discovered-batch")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReconcileDiscoveredCandidates(
        [FromQuery] int limit = 10,
        [FromQuery] int minimumAgeMinutes = 10,
        [FromQuery] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (HospitalId <= 0) return Forbid();
        if (limit is < 1 or > 25)
            return BadRequest(new { message = "Limit must be between 1 and 25." });
        if (minimumAgeMinutes is < 1 or > 1440)
            return BadRequest(new { message = "minimumAgeMinutes must be between 1 and 1440." });

        // Avoid racing normal checkout/webhook processing for newly created records.
        // This is a grace period, not a distributed lock or retry guarantee.
        var createdBeforeUtc = DateTime.UtcNow.AddMinutes(-minimumAgeMinutes);

        var orders = await _db.HospitalPaymentOrders.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayOrderId != null && x.GatewayOrderId != ""
                && x.CreatedAt <= createdBeforeUtc
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.GatewayPaymentId == x.GatewayPaymentId
                    && t.Status == "Captured"))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Take(limit)
            .Select(x => new { x.Id, x.GatewayPaymentId, CreatedAt = x.CreatedAt })
            .ToListAsync(cancellationToken);

        var qrs = await _db.HospitalPaymentQrs.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.GatewayPaymentId != null && x.GatewayPaymentId != ""
                && x.GatewayQrId != null && x.GatewayQrId != ""
                && x.CreatedAtUtc <= createdBeforeUtc
                && !_db.HospitalPaymentTransactions.Any(t =>
                    t.HospitalId == HospitalId && t.GatewayPaymentId == x.GatewayPaymentId
                    && t.Status == "Captured"))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Take(limit)
            .Select(x => new { x.Id, x.GatewayPaymentId, CreatedAt = x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var selected = orders.Select(x => new {
            Source = "Order",
            x.Id,
            x.GatewayPaymentId,
            x.CreatedAt
        }).Concat(qrs.Select(x => new {
            Source = "Qr",
            x.Id,
            x.GatewayPaymentId,
            x.CreatedAt
        }))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Take(limit)
            .ToList();

        // Duplicate gateway IDs across sources indicate inconsistent local state.
        // Never guess the correct invoice; surface the conflict for manual review.
        var ambiguous = selected.GroupBy(x => x.GatewayPaymentId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        if (ambiguous.Count != 0)
            return Conflict(new
            {
                status = "ReviewRequired",
                message = "Conflicting payment IDs were discovered. Resolve the source mapping before recovery.",
                conflictingPaymentCount = ambiguous.Count
            });

        if (selected.Count == 0)
            return Ok(new
            {
                total = 0,
                recoveredOrAlreadyAllocated = 0,
                verifiedNeedsAllocation = 0,
                requiresAttention = 0,
                message = "No locally discoverable candidates. Gateway-only payments are not included."
            });

        // Preview exactly the selected local candidates without calling Razorpay,
        // allocating an invoice, or writing reconciliation history.
        if (dryRun)
            return Ok(new
            {
                dryRun = true,
                total = selected.Count,
                minimumAgeMinutes,
                candidates = selected.Select(x => new {
                    paymentSource = x.Source,
                    orderId = x.Source == "Order" ? (long?)x.Id : null,
                    qrId = x.Source == "Qr" ? (long?)x.Id : null,
                    gatewayPaymentId = x.GatewayPaymentId,
                    createdAtUtc = x.CreatedAt
                }),
                message = "Preview only. No gateway calls or invoice allocations were performed. Candidate eligibility may change before execution."
            });

        var batch = new HospitalPaymentRecoveryBatchRequest
        {
            Payments = selected.Select(x => new HospitalPaymentRecoveryRequest
            {
                PaymentSource = x.Source,
                OrderId = x.Source == "Order" ? x.Id : null,
                QrId = x.Source == "Qr" ? x.Id : null,
                GatewayPaymentId = x.GatewayPaymentId!
            }).ToList()
        };
        return await RunPaymentReconciliationBatch(batch, cancellationToken);
    }

    // Hospital-scoped reconciliation audit history. This reads existing AuditLogs;
    // no new table or migration is required. It contains batch summaries only,
    // never gateway secrets or patient information.
    [HttpGet("reconciliation/history")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetReconciliationHistory(
        [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (HospitalId <= 0) return Forbid();
        if (limit is < 1 or > 100)
            return BadRequest(new { message = "Limit must be between 1 and 100." });

        var history = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.ClinicId == HospitalId
                && x.Action == "HospitalPaymentReconciliationBatch")
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new {
                x.Id,
                x.Timestamp,
                x.UserId,
                x.UserName,
                summary = x.SystemAction
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            total = history.Count,
            history,
            note = "Completed batch-run summaries only. This is not a gateway transaction ledger."
        });
    }

    // Safe, bounded recovery for known payment IDs. This endpoint never creates a
    // Razorpay payment or refund, and never guesses which payment belongs to an order.
    [HttpPost("reconciliation/run")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RunPaymentReconciliation(
        [FromBody] HospitalPaymentRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (HospitalId <= 0) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.PaymentSource)
            || string.IsNullOrWhiteSpace(request.GatewayPaymentId)
            || request.GatewayPaymentId.Length > 150)
            return BadRequest(new { message = "PaymentSource and GatewayPaymentId are required." });

        var source = request.PaymentSource.Trim();
        if (source == "Order")
        {
            if (!request.OrderId.HasValue || request.OrderId <= 0)
                return BadRequest(new { message = "OrderId is required for order recovery." });
            var order = await _db.HospitalPaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.OrderId.Value && x.HospitalId == HospitalId,
                    cancellationToken);
            if (order == null) return NotFound(new { message = "Hospital order not found." });
            if (order.Mode is not ("Test" or "Live") || string.IsNullOrWhiteSpace(order.GatewayOrderId))
                return Conflict(new { status = "ReviewRequired", message = "Order has no valid gateway identity." });
            var bill = await _db.Billings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == order.BillingId && x.HospitalId == HospitalId,
                    cancellationToken);
            if (bill == null) return Conflict(new { status = "ReviewRequired", message = "Invoice is missing." });
            var remote = await _gateway.FetchPaymentAsync(HospitalId, order.Mode,
                request.GatewayPaymentId, order.RazorpayCredentialId);
            if (!remote.Captured || remote.OrderId != order.GatewayOrderId
                || remote.Currency != "INR"
                || remote.AmountPaise != checked((long)(order.Amount * 100m)))
                return Conflict(new { status = "ReviewRequired", message = "Gateway capture identity or amount does not match." });
            var allocated = await RecordCaptureAsync(HospitalId, order.Id, request.GatewayPaymentId);
            return allocated
                ? Ok(new { status = "Reconciled", source, orderId = order.Id, gatewayPaymentId = request.GatewayPaymentId })
                : Conflict(new { status = "ReviewRequired", message = "Captured payment could not be allocated safely." });
        }
        if (source == "Qr")
        {
            if (!request.QrId.HasValue || request.QrId <= 0)
                return BadRequest(new { message = "QrId is required for QR recovery." });
            var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.QrId.Value && x.HospitalId == HospitalId,
                    cancellationToken);
            if (qr == null) return NotFound(new { message = "Hospital QR not found." });
            if (string.IsNullOrWhiteSpace(qr.GatewayQrId) || qr.Mode is not ("Test" or "Live"))
                return Conflict(new { status = "ReviewRequired", message = "QR gateway identity is missing." });
            var verified = await _gateway.VerifyCapturedQrPaymentAsync(HospitalId, qr.Mode,
                qr.GatewayQrId, request.GatewayPaymentId, qr.Amount, qr.RazorpayCredentialId);
            if (!verified)
                return Conflict(new { status = "ReviewRequired", message = "Gateway could not verify QR payment." });
            var allocated = await _db.HospitalPaymentTransactions.AsNoTracking().AnyAsync(
                x => x.HospitalId == HospitalId && x.HospitalPaymentQrId == qr.Id
                     && x.GatewayPaymentId == request.GatewayPaymentId && x.PaymentSource == "Qr"
                     && x.Status == "Captured", cancellationToken);
            if (allocated)
                return Ok(new
                {
                    status = "AlreadyAllocated",
                    source,
                    qrId = qr.Id,
                    gatewayPaymentId = request.GatewayPaymentId
                });

            // Admin-triggered recovery is NOT a Razorpay webhook. Reuse the same
            // atomic QR allocation routine, but do not fabricate a webhook signature.
            // The routine performs its own merchant-scoped Razorpay capture check.
            var recoveryEventId = "QR-RECOVERY-" + qr.Id + "-" + request.GatewayPaymentId;
            var recoveryPayload = JsonSerializer.Serialize(new
            {
                recovery = true,
                qrId = qr.Id,
                gatewayQrId = qr.GatewayQrId,
                gatewayPaymentId = request.GatewayPaymentId,
                hospitalId = HospitalId,
                mode = qr.Mode
            });
            var allocation = await HandleQrCreditedAsync(qr.Mode, recoveryPayload,
                recoveryEventId, "reconciliation.qr.verified", qr.GatewayQrId,
                request.GatewayPaymentId, verifiedRecovery: true);
            if (allocation is OkObjectResult ok && ok.Value != null)
            {
                var resultJson = JsonSerializer.SerializeToElement(ok.Value);
                var allocationStatus = resultJson.TryGetProperty("status", out var st)
                    ? st.GetString() : null;
                if (allocationStatus == "Processed")
                {
                    var capturedNow = await _db.HospitalPaymentTransactions.AsNoTracking()
                        .AnyAsync(x => x.HospitalId == HospitalId
                            && x.HospitalPaymentQrId == qr.Id
                            && x.GatewayPaymentId == request.GatewayPaymentId
                            && x.PaymentSource == "Qr"
                            && x.Status == "Captured", cancellationToken);
                    if (capturedNow)
                        return Ok(new
                        {
                            status = "Reconciled",
                            source,
                            qrId = qr.Id,
                            gatewayPaymentId = request.GatewayPaymentId
                        });
                }
            }
            return Conflict(new
            {
                status = "ReviewRequired",
                source,
                qrId = qr.Id,
                gatewayPaymentId = request.GatewayPaymentId,
                message = "Verified QR payment could not be allocated safely; inspect reconciliation event and invoice."
            });
        }
        return BadRequest(new { message = "PaymentSource must be Order or Qr." });
    }

    public sealed class HospitalPaymentRecoveryRequest
    {
        public string PaymentSource { get; set; } = "";
        public string GatewayPaymentId { get; set; } = "";
        public long? OrderId { get; set; }
        public long? QrId { get; set; }
    }

    // Bounded manual batch recovery. Each entry is independently validated using
    // the same merchant-scoped rules as the single-payment recovery endpoint.
    // Process sequentially to avoid concurrent operations on the scoped DbContext.
    [HttpPost("reconciliation/batch")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RunPaymentReconciliationBatch(
        [FromBody] HospitalPaymentRecoveryBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (HospitalId <= 0) return Forbid();
        if (request?.Payments == null || request.Payments.Count is < 1 or > 25)
            return BadRequest(new { message = "Supply between 1 and 25 payment records." });

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var gatewayPaymentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in request.Payments)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PaymentSource)
                || string.IsNullOrWhiteSpace(item.GatewayPaymentId)
                || item.GatewayPaymentId.Length > 150
                || item.PaymentSource is not ("Order" or "Qr")
                || (item.PaymentSource == "Order" && (!item.OrderId.HasValue || item.OrderId <= 0 || item.QrId.HasValue))
                || (item.PaymentSource == "Qr" && (!item.QrId.HasValue || item.QrId <= 0 || item.OrderId.HasValue)))
                return BadRequest(new { message = "Every item requires a valid payment source, gateway payment ID and exactly one source ID." });
            var key = item.PaymentSource + ":" + (item.OrderId?.ToString() ?? item.QrId?.ToString())
                + ":" + item.GatewayPaymentId;
            if (!seen.Add(key))
                return BadRequest(new { message = "Duplicate recovery entries are not allowed." });
            // A gateway payment cannot be allocated to multiple invoices, even
            // when the caller supplies different source IDs or source types.
            if (!gatewayPaymentIds.Add(item.GatewayPaymentId))
                return BadRequest(new { message = "The same gateway payment ID cannot appear more than once in a batch." });
        }

        var results = new List<object>();
        var recoveredOrAllocated = 0;
        var verifiedNeedsAllocation = 0;
        var requiresAttention = 0;
        foreach (var item in request.Payments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Reuse the single-item recovery action, including gateway checks.
                var response = await RunPaymentReconciliation(item, cancellationToken);

                var statusCode = response switch
                {
                    ObjectResult objectResult => objectResult.StatusCode ?? 200,
                    StatusCodeResult statusResult => statusResult.StatusCode,
                    _ => 200
                };

                // HTTP 200 does not necessarily mean the invoice is recovered:
                // QR verification can succeed while allocation is still pending.
                string? recoveryStatus = null;
                if (response is ObjectResult outcomeResponse && outcomeResponse.Value != null)
                {
                    var outcomeJson = JsonSerializer.SerializeToElement(outcomeResponse.Value);
                    if (outcomeJson.ValueKind == JsonValueKind.Object
                        && outcomeJson.TryGetProperty("status", out var statusElement)
                        && statusElement.ValueKind == JsonValueKind.String)
                        recoveryStatus = statusElement.GetString();
                }
                if (statusCode is >= 200 and < 300
                    && recoveryStatus is "Reconciled" or "AlreadyAllocated")
                    recoveredOrAllocated++;
                else if (statusCode is >= 200 and < 300
                    && recoveryStatus == "VerifiedNeedsAllocation")
                    verifiedNeedsAllocation++;
                else
                    requiresAttention++;
                results.Add(new
                {
                    item.PaymentSource,
                    item.OrderId,
                    item.QrId,
                    item.GatewayPaymentId,
                    httpStatus = statusCode,
                    outcome = response is ObjectResult obj ? obj.Value : null
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch payment recovery failed for source {Source}, payment {PaymentId}",
                    item.PaymentSource, item.GatewayPaymentId);
                // Unknown gateway outcomes are never retried automatically.
                requiresAttention++;
                results.Add(new
                {
                    item.PaymentSource,
                    item.OrderId,
                    item.QrId,
                    item.GatewayPaymentId,
                    httpStatus = 502,
                    outcome = new { status = "ReviewRequired", message = "Recovery could not be completed; investigate before retrying." }
                });
            }
        }

        // Persist an operator audit summary, without storing gateway IDs or PII.
        // Failure to write an audit must not be reported as a successful audited run.
        AddHospitalPaymentAudit("HospitalPaymentReconciliationBatch",
            $"Total={results.Count}; RecoveredOrAlreadyAllocated={recoveredOrAllocated}; "
            + $"VerifiedNeedsAllocation={verifiedNeedsAllocation}; RequiresAttention={requiresAttention}");
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            total = results.Count,
            recoveredOrAlreadyAllocated = recoveredOrAllocated,
            verifiedNeedsAllocation,
            requiresAttention,
            // Retain this legacy property for clients that consume it, but it
            // now counts only payments that are actually allocated.
            successfulChecks = recoveredOrAllocated,
            results
        });
    }

    public sealed class HospitalPaymentRecoveryBatchRequest
    {
        public List<HospitalPaymentRecoveryRequest> Payments { get; set; } = new();
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateHospitalPaymentOrderDto dto)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (dto == null || dto.BillingId <= 0)
            return BadRequest(new { message = "Valid BillingId is required." });

        var bill = await _db.Billings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.BillingId && x.HospitalId == HospitalId);

        if (bill == null)
            return NotFound(new { message = "Invoice not found for this hospital." });

        if (!CanAccessBill(bill))
            return Forbid();

        // The shared service performs merchant checks, payment balance
        // calculations, reservations, idempotency and gateway order creation.
        return await _orderService.CreateAsync(
            HospitalId, bill, dto.Amount, dto.IdempotencyKey);
    }

    [HttpGet("orders/{localOrderId}")]
    public async Task<IActionResult> GetOrderStatus(string localOrderId)
    {
        if (HospitalId <= 0)
            return Forbid();

        var order = await _db.HospitalPaymentOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.LocalOrderId == localOrderId && x.HospitalId == HospitalId);

        if (order == null)
            return NotFound(new { message = "Payment order not found." });

        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == order.BillingId && x.HospitalId == HospitalId);

        if (bill == null || !CanAccessBill(bill))
            return Forbid();

        return Ok(new
        {
            order.LocalOrderId,
            order.BillingId,
            order.Amount,
            order.Currency,
            order.Status,
            order.GatewayPaymentId,
            order.CreatedAt,
            order.PaidAt
        });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyHospitalPaymentDto dto)
    {
        if (HospitalId <= 0)
            return Forbid();

        var order = await _db.HospitalPaymentOrders.FirstOrDefaultAsync(
            x => x.LocalOrderId == dto.LocalOrderId && x.HospitalId == HospitalId);

        if (order == null)
            return NotFound(new { message = "Payment order not found." });

        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == order.BillingId && x.HospitalId == HospitalId);

        if (bill == null || !CanAccessBill(bill))
            return Forbid();

        if (order.GatewayOrderId != dto.RazorpayOrderId)
            return BadRequest(new { message = "Razorpay order mismatch." });

        if (!_gateway.VerifyCheckoutSignature(
            HospitalId,
            order.Mode,
            dto.RazorpayOrderId,
            dto.RazorpayPaymentId,
            dto.RazorpaySignature,
            order.RazorpayCredentialId))
        {
            return BadRequest(new { message = "Invalid Razorpay checkout signature." });
        }

        var payment = await _gateway.FetchPaymentAsync(
            HospitalId,
            order.Mode,
            dto.RazorpayPaymentId,
            order.RazorpayCredentialId);

        if (!payment.Captured
            || payment.OrderId != order.GatewayOrderId
            || payment.Currency != "INR"
            || payment.AmountPaise != checked((long)(order.Amount * 100m)))
        {
            return Conflict(new
            {
                message = "Razorpay has not confirmed a matching captured payment. Order remains pending until gateway confirmation."
            });
        }

        var result = await RecordCaptureAsync(HospitalId, order.Id, dto.RazorpayPaymentId);

        return result
            ? Ok(new { message = "Hospital payment verified and allocated to invoice.", status = "Captured" })
            : Conflict(new { message = "Payment could not be allocated." });
    }

    private Task<bool> RecordCaptureAsync(int hospitalId, long orderId, string paymentId)
        => _allocationService.AllocateOrderAsync(hospitalId, orderId, paymentId);

    [AllowAnonymous]
    [HttpPost("webhooks/razorpay/{mode}")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Webhook(string mode)
    {
        if (mode is not ("Test" or "Live"))
            return BadRequest();

        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync();

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch
        {
            return BadRequest();
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var ev)
                ? ev.GetString() ?? ""
                : "";

            var eventId = Request.Headers["X-Razorpay-Event-Id"].ToString();

            if (string.IsNullOrWhiteSpace(eventId))
                return BadRequest(new { message = "Missing Razorpay event ID." });

            string? orderId = null;
            string? webhookPaymentId = null;
            string? webhookRefundId = null;

            JsonElement paymentEntity = default;
            JsonElement refundEntity = default;
            JsonElement disputeEntity = default;

            var hasPaymentEntity = root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("payment", out var payment)
                && payment.TryGetProperty("entity", out paymentEntity);

            var hasDisputeEntity = root.TryGetProperty("payload", out payload)
                && payload.TryGetProperty("dispute", out var dispute)
                && dispute.TryGetProperty("entity", out disputeEntity);

            var hasRefundEntity = root.TryGetProperty("payload", out payload)
                && payload.TryGetProperty("refund", out var refund)
                && refund.TryGetProperty("entity", out refundEntity);

            if (hasPaymentEntity)
            {
                if (paymentEntity.TryGetProperty("order_id", out var oid))
                    orderId = oid.GetString();

                if (paymentEntity.TryGetProperty("id", out var pid))
                    webhookPaymentId = pid.GetString();
            }

            if (hasRefundEntity)
            {
                if (refundEntity.TryGetProperty("payment_id", out var rpid))
                    webhookPaymentId = rpid.GetString();

                if (refundEntity.TryGetProperty("id", out var rid))
                    webhookRefundId = rid.GetString();
            }

            if (hasDisputeEntity
                && disputeEntity.TryGetProperty("payment_id", out var disputePaymentId))
            {
                webhookPaymentId = disputePaymentId.GetString();
            }

            // QR events are associated with a QR ID, not necessarily a Razorpay order.
            // Handle them before the existing order-based webhook routing.
            if (eventType == "qr_code.credited")
            {
                string? gatewayQrId = null;
                if (root.TryGetProperty("payload", out var qrPayload)
                    && qrPayload.TryGetProperty("qr_code", out var qrWrapper)
                    && qrWrapper.TryGetProperty("entity", out var qrEntity)
                    && qrEntity.TryGetProperty("id", out var qrIdElement))
                    gatewayQrId = qrIdElement.GetString();

                if (string.IsNullOrWhiteSpace(gatewayQrId)
                    || string.IsNullOrWhiteSpace(webhookPaymentId))
                    return BadRequest(new { message = "QR webhook is missing QR or payment identifier." });

                return await HandleQrCreditedAsync(
                    mode, raw, eventId, eventType, gatewayQrId, webhookPaymentId);
            }

            // Refund events can belong to an order payment or a QR payment.
            // Resolve the merchant from the captured transaction rather than requiring an order.
            if (eventType is "refund.processed" or "refund.failed")
            {
                if (!hasRefundEntity || string.IsNullOrWhiteSpace(webhookRefundId)
                    || string.IsNullOrWhiteSpace(webhookPaymentId))
                    return BadRequest(new { message = "Refund webhook is missing payment or refund identity." });

                return await HandleRefundWebhookAsync(
                    mode, raw, eventId, eventType, webhookPaymentId, webhookRefundId, refundEntity);
            }

            // Disputes, like refunds, may be raised against QR payments with no order ID.
            if (eventType.StartsWith("payment.dispute.", StringComparison.OrdinalIgnoreCase))
            {
                if (!hasDisputeEntity || string.IsNullOrWhiteSpace(webhookPaymentId))
                    return BadRequest(new { message = "Dispute webhook is missing payment identity." });
                return await HandleDisputeWebhookAsync(mode, raw, eventId, eventType,
                    webhookPaymentId, disputeEntity);
            }

            HospitalPaymentOrder? order = null;
            HospitalPaymentTransaction? refundTransaction = null;

            if (!string.IsNullOrWhiteSpace(orderId))
            {
                order = await _db.HospitalPaymentOrders.FirstOrDefaultAsync(
                    x => x.GatewayOrderId == orderId && x.Mode == mode);
            }

            if (order == null && !string.IsNullOrWhiteSpace(webhookPaymentId))
            {
                refundTransaction = await _db.HospitalPaymentTransactions
                    .FirstOrDefaultAsync(x =>
                        x.GatewayPaymentId == webhookPaymentId && x.Mode == mode);

                if (refundTransaction != null)
                {
                    order = await _db.HospitalPaymentOrders.FirstOrDefaultAsync(
                        x => x.Id == refundTransaction.HospitalPaymentOrderId);
                }
            }

            if (order == null)
                return NotFound(new { message = "No hospital payment order matches this webhook." });

            var signature = Request.Headers["X-Razorpay-Signature"].ToString();

            if (!_gateway.VerifyWebhookSignature(order.HospitalId, mode, raw, signature, order.RazorpayCredentialId))
                return Unauthorized(new { message = "Invalid Razorpay webhook signature." });

            var inbox = await _db.HospitalPaymentWebhookEvents.FirstOrDefaultAsync(
                x => x.EventId == eventId && x.Mode == mode);

            if (inbox != null)
            {
                // An event identifier must never be replayed across merchants.
                if (inbox.HospitalId != order.HospitalId)
                    return Conflict(new { message = "Webhook event belongs to another hospital." });

                if (inbox.EventType != eventType || inbox.PayloadJson != raw)
                {
                    return Conflict(new
                    {
                        message = "Webhook event ID was reused with different content."
                    });
                }

                if (inbox.ProcessingStatus is "Processed" or "Ignored")
                {
                    return Ok(new
                    {
                        received = true,
                        duplicate = true,
                        status = inbox.ProcessingStatus
                    });
                }

                inbox.LastError = null;
                inbox.ProcessingStatus = "Received";
            }
            else
            {
                inbox = new HospitalPaymentWebhookEvent
                {
                    EventId = eventId,
                    EventType = eventType,
                    HospitalId = order.HospitalId,
                    Mode = mode,
                    GatewayOrderId = orderId,
                    PayloadJson = raw,
                    ProcessingStatus = "Received"
                };

                _db.HospitalPaymentWebhookEvents.Add(inbox);
            }

            await _db.SaveChangesAsync();

            try
            {
                if (eventType == "payment.captured"
                    && hasPaymentEntity
                    && !string.IsNullOrWhiteSpace(webhookPaymentId))
                {
                    var fetched = await _gateway.FetchPaymentAsync(
                        order.HospitalId,
                        mode,
                        webhookPaymentId,
                        order.RazorpayCredentialId);

                    if (fetched.Captured
                        && fetched.OrderId == order.GatewayOrderId
                        && fetched.Currency == "INR"
                        && fetched.AmountPaise == checked((long)(order.Amount * 100m)))
                    {
                        if (!await RecordCaptureAsync(order.HospitalId, order.Id, webhookPaymentId))
                            inbox.ProcessingStatus = "ReviewRequired";
                    }
                    else
                    {
                        inbox.ProcessingStatus = "ReviewRequired";
                    }
                }
                else if ((eventType == "refund.processed" || eventType == "refund.failed")
                    && hasRefundEntity
                    && refundTransaction != null
                    && !string.IsNullOrWhiteSpace(webhookRefundId))
                {
                    var savedRefund = await _db.HospitalPaymentRefunds.FirstOrDefaultAsync(
                        x => x.GatewayRefundId == webhookRefundId
                            && x.HospitalId == order.HospitalId);

                    if (savedRefund == null ||
                        savedRefund.HospitalPaymentTransactionId != refundTransaction.Id)
                    {
                        // Keep an unmatched provider event visible for manual reconciliation.
                        inbox.ProcessingStatus = "ReviewRequired";
                        inbox.LastError = "Refund webhook did not match a recorded hospital refund.";
                    }
                    else
                    {
                        // A delayed failure must not reverse a processed refund.
                        if (eventType == "refund.processed")
                        {
                            savedRefund.Status = "processed";
                            savedRefund.CompletedAt ??= DateTime.UtcNow;
                        }
                        else if (savedRefund.Status != "processed")
                        {
                            savedRefund.Status = "failed";
                        }

                        // Flush the updated status before querying SQL aggregates.
                        await _db.SaveChangesAsync();

                        refundTransaction.RefundedAmount =
                            await _db.HospitalPaymentRefunds
                                .Where(x =>
                                    x.HospitalPaymentTransactionId == refundTransaction.Id
                                    && x.Status == "processed")
                                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                        var bill = await _db.Billings.FirstOrDefaultAsync(
                            x => x.Id == refundTransaction.BillingId
                                && x.HospitalId == order.HospitalId);

                        if (bill != null)
                        {
                            var collected = await _db.BillingPaymentTransactions
                                .Where(x => x.BillingId == bill.Id
                                    && x.HospitalId == order.HospitalId
                                    && x.Status == "Paid")
                                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                            var refundedTotal = await _db.HospitalPaymentRefunds
                                .Where(x => x.BillingId == bill.Id
                                    && x.HospitalId == order.HospitalId
                                    && x.Status == "processed")
                                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                            bill.Status = collected - refundedTotal >= bill.TotalAmount
                                ? "Paid"
                                : collected > refundedTotal
                                    ? "PartiallyPaid"
                                    : "Pending";
                        }
                    }

                    if (inbox.ProcessingStatus == "Received")
                        inbox.ProcessingStatus = "Processed";
                }
                else if (eventType == "payment.failed")
                {
                    // A failed attempt does not mean the Razorpay order cannot
                    // later receive a successful captured payment.
                    inbox.ProcessingStatus = "Processed";
                }
                else
                {
                    inbox.ProcessingStatus = "Ignored";
                }

                if (inbox.ProcessingStatus == "Received")
                    inbox.ProcessingStatus = "Processed";

                inbox.ProcessedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    received = true,
                    status = inbox.ProcessingStatus
                });
            }
            catch (Exception ex)
            {
                inbox.ProcessingStatus = "Failed";
                inbox.LastError = ex.Message[..Math.Min(1000, ex.Message.Length)];

                await _db.SaveChangesAsync();

                _logger.LogError(
                    ex,
                    "Hospital webhook processing failed for event {EventId}",
                    eventId);

                return StatusCode(500, new
                {
                    message = "Webhook accepted but reconciliation failed; retry delivery."
                });
            }
        }
    }

    // Signed dispute notifications are resolved against the original order OR QR merchant.
    private async Task<IActionResult> HandleDisputeWebhookAsync(
        string mode, string raw, string eventId, string eventType,
        string gatewayPaymentId, JsonElement disputeEntity)
    {
        var payment = await _db.HospitalPaymentTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.GatewayPaymentId == gatewayPaymentId && x.Mode == mode);
        if (payment == null)
            return NotFound(new { message = "No hospital payment matches this dispute." });

        int? credentialId;
        try { credentialId = await CredentialForTransactionAsync(payment); }
        catch (InvalidOperationException)
        { return Conflict(new { message = "Dispute payment source requires review." }); }

        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        if (!_gateway.VerifyWebhookSignature(payment.HospitalId, mode, raw, signature, credentialId))
            return Unauthorized(new { message = "Invalid Razorpay webhook signature." });

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var inbox = await _db.HospitalPaymentWebhookEvents
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.Mode == mode);
        if (inbox != null)
        {
            if (inbox.HospitalId != payment.HospitalId || inbox.EventType != eventType
                || inbox.PayloadJson != raw)
                return Conflict(new { message = "Webhook event ID conflicts with existing content." });
            if (inbox.ProcessingStatus is "Processed" or "Ignored")
                return Ok(new { received = true, duplicate = true });
        }
        else
        {
            inbox = new HospitalPaymentWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                HospitalId = payment.HospitalId,
                Mode = mode,
                GatewayOrderId = payment.GatewayOrderId,
                PayloadJson = raw,
                ProcessingStatus = "Received"
            };
            _db.HospitalPaymentWebhookEvents.Add(inbox);
        }
        try
        {
            var disputeId = disputeEntity.TryGetProperty("id", out var did)
                ? did.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(disputeId))
                throw new InvalidOperationException("Dispute webhook is missing dispute ID.");

            var disputeRecord = await _db.HospitalPaymentDisputes.FirstOrDefaultAsync(
                x => x.HospitalId == payment.HospitalId
                    && x.GatewayDisputeId == disputeId);

            var status = eventType switch
            {
                "payment.dispute.created" => "Created",
                "payment.dispute.under_review" => "UnderReview",
                "payment.dispute.action_required" => "ActionRequired",
                "payment.dispute.won" => "Won",
                "payment.dispute.lost" => "Lost",
                "payment.dispute.closed" =>
                    disputeRecord != null
                    && (disputeRecord.Status == "Won" || disputeRecord.Status == "Lost")
                        ? disputeRecord.Status
                        : "Closed",
                _ => "Unknown"
            };

            decimal disputeAmount =
                disputeEntity.TryGetProperty("amount", out var da)
                && da.TryGetInt64(out var dap)
                    ? dap / 100m
                    : payment.Amount;

            DateTime? due = null;

            if (disputeEntity.TryGetProperty("respond_by", out var dueEl)
                && dueEl.TryGetInt64(out var dueEpoch)
                && dueEpoch > 0)
            {
                due = DateTimeOffset.FromUnixTimeSeconds(dueEpoch).UtcDateTime;
            }
            else if (disputeEntity.TryGetProperty(
                "evidence_due_by",
                out var evidenceDueEl)
                && evidenceDueEl.TryGetInt64(out var evidenceDueEpoch)
                && evidenceDueEpoch > 0)
            {
                due = DateTimeOffset.FromUnixTimeSeconds(
                    evidenceDueEpoch).UtcDateTime;
            }

            var reason = disputeEntity.TryGetProperty("reason_code", out var reasonEl)
                ? reasonEl.GetString()
                : null;

            if (disputeRecord == null)
            {
                disputeRecord = new HospitalPaymentDispute
                {
                    HospitalId = payment.HospitalId,
                    HospitalPaymentTransactionId = payment.Id,
                    GatewayDisputeId = disputeId,
                    GatewayPaymentId = payment.GatewayPaymentId,
                    Amount = disputeAmount,
                    Currency = disputeEntity.TryGetProperty("currency", out var cur)
                        ? cur.GetString() ?? "INR"
                        : "INR",
                    Reason = reason,
                    Status = status,
                    EvidenceDueBy = due,
                    PayloadJson = raw
                };

                _db.HospitalPaymentDisputes.Add(disputeRecord);
            }
            else
            {
                disputeRecord.Status = status;
                disputeRecord.Reason = reason ?? disputeRecord.Reason;
                disputeRecord.EvidenceDueBy = due ?? disputeRecord.EvidenceDueBy;
                disputeRecord.PayloadJson = raw;
                disputeRecord.UpdatedAt = DateTime.UtcNow;
            }

            if (status is "Won" or "Lost" or "Closed")
                disputeRecord.ClosedAt = DateTime.UtcNow;

            var disputeBill = await _db.Billings.FirstOrDefaultAsync(
                x => x.Id == payment.BillingId
                    && x.HospitalId == payment.HospitalId);

            if (disputeBill != null)
            {
                var paidForBill = await _db.BillingPaymentTransactions
                    .Where(x => x.BillingId == disputeBill.Id
                        && x.HospitalId == payment.HospitalId
                        && x.Status == "Paid")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var refundedForBill = await _db.HospitalPaymentRefunds
                    .Where(x => x.BillingId == disputeBill.Id
                        && x.HospitalId == payment.HospitalId
                        && x.Status == "processed")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var lostForBill = await _db.HospitalPaymentDisputes
                    .Where(x => x.HospitalId == payment.HospitalId
                        && x.Transaction.BillingId == disputeBill.Id
                        && x.Status == "Lost"
                        && x.Id != disputeRecord.Id)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                if (status == "Lost")
                    lostForBill += disputeAmount;

                var remainingPaid = Math.Max(
                    0m,
                    paidForBill - refundedForBill - lostForBill);

                disputeBill.Status = remainingPaid >= disputeBill.TotalAmount
                    ? "Paid"
                    : remainingPaid > 0
                        ? "PartiallyPaid"
                        : "Pending";
            }

            inbox.ProcessingStatus = "Processed";
            inbox.LastError = null;
            inbox.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new { received = true, status = inbox.ProcessingStatus });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispute webhook processing failed for event {EventId}", eventId);
            return StatusCode(500, new { message = "Dispute webhook processing failed; retry delivery." });
        }
    }

    // Resolve and reconcile refund webhooks for BOTH Razorpay orders and QR payments.
    // This handler never creates a gateway refund; it reads the provider state via
    // HospitalRefundReconciliationService before changing any refund or invoice totals.
    private async Task<IActionResult> HandleRefundWebhookAsync(
        string mode, string raw, string eventId, string eventType,
        string gatewayPaymentId, string gatewayRefundId, JsonElement refundEntity)
    {
        var payment = await _db.HospitalPaymentTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.GatewayPaymentId == gatewayPaymentId && x.Mode == mode);
        if (payment == null)
            return NotFound(new { message = "No captured hospital transaction matches the refund webhook." });

        int? credentialId;
        if (payment.PaymentSource == "Order" && payment.HospitalPaymentOrderId.HasValue
            && !payment.HospitalPaymentQrId.HasValue)
        {
            var order = await _db.HospitalPaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == payment.HospitalPaymentOrderId.Value
                    && x.HospitalId == payment.HospitalId && x.Mode == mode);
            if (order == null || order.GatewayPaymentId != gatewayPaymentId
                || order.GatewayOrderId != payment.GatewayOrderId
                || order.BillingId != payment.BillingId)
                return Conflict(new { message = "Original payment order identity is inconsistent." });
            credentialId = order.RazorpayCredentialId;
        }
        else if (payment.PaymentSource == "Qr" && payment.HospitalPaymentQrId.HasValue
            && !payment.HospitalPaymentOrderId.HasValue)
        {
            var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == payment.HospitalPaymentQrId.Value
                    && x.HospitalId == payment.HospitalId && x.Mode == mode);
            if (qr == null || qr.GatewayPaymentId != gatewayPaymentId
                || qr.BillingId != payment.BillingId || qr.Amount != payment.Amount)
                return Conflict(new { message = "Original QR payment identity is inconsistent." });
            credentialId = qr.RazorpayCredentialId;
        }
        else
        {
            return Conflict(new { message = "Payment source is inconsistent; refund requires review." });
        }

        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        if (!_gateway.VerifyWebhookSignature(payment.HospitalId, mode, raw, signature, credentialId))
            return Unauthorized(new { message = "Invalid Razorpay webhook signature." });

        // An event ID is unique to its exact payload and hospital, including retries.
        var inbox = await _db.HospitalPaymentWebhookEvents
            .SingleOrDefaultAsync(x => x.EventId == eventId && x.Mode == mode);
        if (inbox != null)
        {
            if (inbox.HospitalId != payment.HospitalId
                || inbox.EventType != eventType || inbox.PayloadJson != raw)
                return Conflict(new { message = "Webhook event ID conflicts with an existing event." });
            if (inbox.ProcessingStatus is "Processed" or "Ignored")
                return Ok(new { received = true, duplicate = true, status = inbox.ProcessingStatus });
        }
        else
        {
            inbox = new HospitalPaymentWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                HospitalId = payment.HospitalId,
                Mode = mode,
                GatewayOrderId = payment.GatewayOrderId,
                PayloadJson = raw,
                ProcessingStatus = "Received"
            };
            _db.HospitalPaymentWebhookEvents.Add(inbox);
        }

        // Match a known provider ID first. An uncertain refund can still have
        // REQUESTED-* locally if Razorpay replied after a network timeout.
        var refund = await _db.HospitalPaymentRefunds.AsNoTracking()
            .SingleOrDefaultAsync(x => x.HospitalPaymentTransactionId == payment.Id
                && x.HospitalId == payment.HospitalId
                && x.GatewayRefundId == gatewayRefundId);
        if (refund == null && refundEntity.TryGetProperty("amount", out var amountElement)
            && amountElement.TryGetInt64(out var amountPaise) && amountPaise > 0)
        {
            var placeholders = await _db.HospitalPaymentRefunds.AsNoTracking()
                .Where(x => x.HospitalPaymentTransactionId == payment.Id
                    && x.HospitalId == payment.HospitalId
                    && x.GatewayRefundId.StartsWith("REQUESTED-")
                    && x.Amount == amountPaise / 100m)
                .Take(2).ToListAsync();
            if (placeholders.Count == 1)
                refund = placeholders[0];
        }

        if (refund == null)
        {
            inbox.ProcessingStatus = "ReviewRequired";
            inbox.LastError = "Gateway refund does not match a unique local refund; manual reconciliation required.";
            await _db.SaveChangesAsync();
            return Ok(new { received = true, status = inbox.ProcessingStatus });
        }

        // Save the inbox before calling the read-only reconciliation service.
        inbox.ProcessingStatus = "Received";
        inbox.LastError = null;
        await _db.SaveChangesAsync();

        try
        {
            var result = await _refundReconciliation.ReconcileAsync(payment.HospitalId, refund.Id);
            if (result.Outcome == "Reconciled"
                && string.Equals(result.GatewayRefundId, gatewayRefundId, StringComparison.Ordinal))
            {
                // The provider is authoritative. A late webhook can carry an
                // older state; reconciliation has already fetched current state.
                inbox.ProcessingStatus = "Processed";
                inbox.LastError = null;
            }
            else
            {
                inbox.ProcessingStatus = "ReviewRequired";
                inbox.LastError = (result.Outcome + ": " + result.Message)[..Math.Min(1000, result.Outcome.Length + result.Message.Length + 2)];
            }
            inbox.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                received = true,
                status = inbox.ProcessingStatus,
                refundId = refund.Id,
                reconciliation = result.Outcome
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refund webhook reconciliation failed for event {EventId}", eventId);
            inbox.ProcessingStatus = "Failed";
            inbox.LastError = ex.Message[..Math.Min(1000, ex.Message.Length)];
            await _db.SaveChangesAsync();
            return StatusCode(500, new { message = "Refund reconciliation failed; retry delivery." });
        }
    }

    // QR webhook/reconciliation entry point. Gateway verification happens before
    // shared allocation. The shared service owns the financial transaction.
    private async Task<IActionResult> HandleQrCreditedAsync(
        string mode, string raw, string eventId, string eventType,
        string gatewayQrId, string paymentId, bool verifiedRecovery = false)
    {
        var qr = await _db.HospitalPaymentQrs.AsNoTracking()
            .SingleOrDefaultAsync(x => x.GatewayQrId == gatewayQrId && x.Mode == mode);
        if (qr == null)
            return NotFound(new { message = "No hospital QR matches this payment." });

        if (!verifiedRecovery)
        {
            var signature = Request.Headers["X-Razorpay-Signature"].ToString();
            if (!_gateway.VerifyWebhookSignature(
                qr.HospitalId, mode, raw, signature, qr.RazorpayCredentialId))
                return Unauthorized(new { message = "Invalid Razorpay webhook signature." });
        }
        else if (HospitalId <= 0 || qr.HospitalId != HospitalId || !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        // Never reuse an event ID with different content or a different merchant.
        var existingEvent = await _db.HospitalPaymentWebhookEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId && x.Mode == mode);
        if (existingEvent != null)
        {
            if (existingEvent.HospitalId != qr.HospitalId ||
                existingEvent.EventType != eventType || existingEvent.PayloadJson != raw)
                return Conflict(new { message = "Webhook event ID conflicts with existing content." });

            if (existingEvent.ProcessingStatus == "Processed")
                return Ok(new { received = true, duplicate = true, status = "Processed" });
        }

        try
        {
            // Signed webhook alone is not proof of captured funds.
            var verified = await _gateway.VerifyCapturedQrPaymentAsync(
                qr.HospitalId, mode, gatewayQrId, paymentId,
                qr.Amount, qr.RazorpayCredentialId);

            // This method owns a separate serializable transaction and never
            // calls Razorpay. Do not wrap it in another EF transaction.
            var allocated = verified && await _allocationService.AllocateQrAsync(
                qr.HospitalId, qr.Id, paymentId);

            // Persist the inbox outcome after allocation. If this write fails,
            // the next delivery safely checks the already-allocated ledgers.
            var inbox = await _db.HospitalPaymentWebhookEvents
                .SingleOrDefaultAsync(x => x.EventId == eventId && x.Mode == mode);
            if (inbox == null)
            {
                inbox = new HospitalPaymentWebhookEvent
                {
                    EventId = eventId,
                    EventType = eventType,
                    HospitalId = qr.HospitalId,
                    Mode = mode,
                    PayloadJson = raw,
                    ProcessingStatus = "Received"
                };
                _db.HospitalPaymentWebhookEvents.Add(inbox);
            }
            else if (inbox.HospitalId != qr.HospitalId ||
                     inbox.EventType != eventType || inbox.PayloadJson != raw)
            {
                return Conflict(new { message = "Webhook event ID conflicts with existing content." });
            }

            inbox.ProcessingStatus = allocated ? "Processed" : "ReviewRequired";
            inbox.LastError = allocated ? null : verified
                ? "Verified QR payment could not be allocated safely; manual review required."
                : "Captured QR payment identity, amount, or merchant could not be verified.";
            inbox.ProcessedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { received = true, status = inbox.ProcessingStatus });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "QR processing failed for event {EventId}", eventId);
            return StatusCode(500, new
            {
                message = "QR payment processing requires retry or reconciliation."
            });
        }
    }

    [HttpPost("transactions/{transactionId:long}/refunds")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Refund(
        long transactionId,
        [FromBody] CreateHospitalRefundDto dto)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (transactionId <= 0 || dto == null || dto.Amount <= 0m ||
            decimal.Round(dto.Amount, 2) != dto.Amount)
            return BadRequest(new { message = "A valid transaction and positive refund amount (maximum two decimal places) are required." });

        HospitalPaymentRefund refund;
        string paymentId;
        string mode;
        int? credentialId;

        // Reserve the refundable balance under a SQL Server update lock.
        // No external Razorpay request is made while holding this transaction.
        await using (var reservation = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var transaction = await _db.HospitalPaymentTransactions
                .FromSqlInterpolated($"SELECT * FROM [HospitalPaymentTransactions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {transactionId}")
                .SingleOrDefaultAsync();

            if (transaction == null || transaction.HospitalId != HospitalId)
                return NotFound(new { message = "Hospital payment transaction not found." });

            if (transaction.Status != "Captured" ||
                string.IsNullOrWhiteSpace(transaction.GatewayPaymentId) ||
                transaction.Mode is not ("Test" or "Live"))
                return Conflict(new { message = "Only captured Razorpay payments can be refunded." });

            // Resolve the exact merchant credential from the original payment source.
            // QR payments have no Razorpay order; never fall back to another merchant.
            if (transaction.HospitalPaymentOrderId.HasValue &&
                !transaction.HospitalPaymentQrId.HasValue &&
                transaction.PaymentSource == "Order")
            {
                var order = await _db.HospitalPaymentOrders.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == transaction.HospitalPaymentOrderId.Value &&
                                               x.HospitalId == HospitalId && x.Mode == transaction.Mode);
                if (order == null || order.GatewayPaymentId != transaction.GatewayPaymentId ||
                    order.BillingId != transaction.BillingId || order.Status != "Captured")
                    return Conflict(new { message = "Original Razorpay order could not be verified." });

                credentialId = order.RazorpayCredentialId;
            }
            else if (transaction.HospitalPaymentQrId.HasValue &&
                     !transaction.HospitalPaymentOrderId.HasValue &&
                     transaction.PaymentSource == "Qr")
            {
                var qr = await _db.HospitalPaymentQrs.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == transaction.HospitalPaymentQrId.Value &&
                                               x.HospitalId == HospitalId && x.Mode == transaction.Mode);
                if (qr == null || qr.GatewayPaymentId != transaction.GatewayPaymentId ||
                    qr.BillingId != transaction.BillingId || qr.Status != "Paid" ||
                    qr.Amount != transaction.Amount)
                    return Conflict(new { message = "Original Razorpay QR payment could not be verified." });

                credentialId = qr.RazorpayCredentialId;
            }
            else
            {
                return Conflict(new { message = "Payment source is inconsistent; manual review is required." });
            }

            // The captured gateway payment must still match the original merchant,
            // amount and currency before a refund is initiated.
            var capturedPayment = await _gateway.FetchPaymentAsync(
                HospitalId, transaction.Mode, transaction.GatewayPaymentId, credentialId);
            if (!capturedPayment.Captured || capturedPayment.Currency != "INR" ||
                capturedPayment.AmountPaise != checked((long)(transaction.Amount * 100m)))
                return Conflict(new { message = "Razorpay could not verify the original captured payment." });

            var reserved = await _db.HospitalPaymentRefunds
                .Where(x => x.HospitalPaymentTransactionId == transactionId &&
                            x.HospitalId == HospitalId && x.Status != "failed")
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var refundable = Math.Max(0m, transaction.Amount - reserved);
            if (dto.Amount > refundable)
                return BadRequest(new { message = "Refund amount exceeds the refundable balance.", refundableAmount = refundable });

            paymentId = transaction.GatewayPaymentId;
            mode = transaction.Mode;
            refund = new HospitalPaymentRefund
            {
                HospitalPaymentTransactionId = transaction.Id,
                HospitalId = HospitalId,
                BillingId = transaction.BillingId,
                GatewayRefundId = "REQUESTED-" + Guid.NewGuid().ToString("N"),
                Amount = dto.Amount,
                Reason = dto.Reason,
                Status = "requested"
            };

            _db.HospitalPaymentRefunds.Add(refund);
            await _db.SaveChangesAsync();
            await reservation.CommitAsync();
        }

        try
        {
            var result = await _gateway.RefundAsync(
                HospitalId, mode, paymentId, dto.Amount, dto.Reason, credentialId);

            if (string.IsNullOrWhiteSpace(result.RefundId))
                throw new InvalidOperationException("Razorpay did not return a refund identifier.");

            // Reload under a lock: the webhook may have completed the refund first.
            await using var resultTx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var saved = await _db.HospitalPaymentRefunds
                .FromSqlInterpolated($"SELECT * FROM [HospitalPaymentRefunds] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {refund.Id}")
                .SingleAsync();

            if (!saved.GatewayRefundId.StartsWith("REQUESTED-", StringComparison.Ordinal) &&
                saved.GatewayRefundId != result.RefundId)
                return Conflict(new { message = "Refund identifier conflict; manual reconciliation required.", refundId = saved.Id });

            saved.GatewayRefundId = result.RefundId;
            if (saved.Status is not ("processed" or "failed"))
            {
                saved.Status = result.Status is "processed" or "failed" or "pending" or "created"
                    ? result.Status : "ReviewRequired";
                if (saved.Status == "processed")
                    saved.CompletedAt ??= DateTime.UtcNow;
            }

            // Persist refund status before aggregate queries so SQL sees the update.
            await _db.SaveChangesAsync();

            var transaction = await _db.HospitalPaymentTransactions
                .FirstAsync(x => x.Id == transactionId && x.HospitalId == HospitalId);
            transaction.RefundedAmount = await _db.HospitalPaymentRefunds
                .Where(x => x.HospitalPaymentTransactionId == transactionId &&
                            x.HospitalId == HospitalId && x.Status == "processed")
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var bill = await _db.Billings.FirstOrDefaultAsync(
                x => x.Id == transaction.BillingId && x.HospitalId == HospitalId);
            if (bill != null)
            {
                var paid = await _db.BillingPaymentTransactions
                    .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "Paid")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;
                var refunded = await _db.HospitalPaymentRefunds
                    .Where(x => x.BillingId == bill.Id && x.HospitalId == HospitalId && x.Status == "processed")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;
                var remaining = Math.Max(0m, paid - refunded);
                bill.Status = remaining >= bill.TotalAmount ? "Paid" :
                    remaining > 0m ? "PartiallyPaid" : "Pending";
            }

            await _db.SaveChangesAsync();
            await resultTx.CommitAsync();

            return Ok(new
            {
                saved.Id,
                saved.GatewayRefundId,
                saved.Amount,
                saved.Status,
                requiresReconciliation = saved.Status == "ReviewRequired" || saved.Status == "requested"
            });
        }
        catch (Exception ex)
        {
            // A timeout is not proof of failure. Never issue a second refund automatically.
            _logger.LogError(ex,
                "Hospital refund outcome uncertain for refund {RefundId}, transaction {TransactionId}",
                refund.Id, transactionId);

            try
            {
                await using var checkScope = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var saved = await _db.HospitalPaymentRefunds
                    .FromSqlInterpolated($"SELECT * FROM [HospitalPaymentRefunds] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {refund.Id}")
                    .SingleOrDefaultAsync();
                if (saved != null && saved.Status is not ("processed" or "failed"))
                {
                    saved.Status = "ReviewRequired";
                    await _db.SaveChangesAsync();
                }
                await checkScope.CommitAsync();
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Could not flag hospital refund {RefundId} for review", refund.Id);
            }

            return StatusCode(502, new
            {
                message = "Refund outcome is uncertain. Reconcile with Razorpay before retrying.",
                refundId = refund.Id,
                requiresReconciliation = true
            });
        }
    }

    // Reconcile a single hospital refund without creating another Razorpay refund.
    // Admin-only, hospital-scoped; accepts either a route ID or a query refundId.
    [HttpPost("refunds/reconcile")]
    [HttpPost("refunds/{refundId:long}/reconcile")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReconcileRefund(
        [FromRoute] long? refundId,
        [FromQuery(Name = "refundId")] long? queryRefundId,
        CancellationToken cancellationToken)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (refundId.HasValue && queryRefundId.HasValue && refundId != queryRefundId)
            return BadRequest(new { message = "Route and query refund IDs must match." });

        var id = refundId ?? queryRefundId;
        if (!id.HasValue || id.Value <= 0)
            return BadRequest(new { message = "A valid refundId is required." });

        // The service checks the original hospital, transaction, merchant and refund.
        // It only performs gateway GET requests; no second refund is issued.
        try
        {
            var result = await _refundReconciliation.ReconcileAsync(
                HospitalId, id.Value, cancellationToken);

            return result.Outcome switch
            {
                "Invalid" => BadRequest(result),
                "NotFound" => NotFound(result),
                "ReviewRequired" => Conflict(result),
                "GatewayUnavailable" => StatusCode(StatusCodes.Status502BadGateway, result),
                "Reconciled" => Ok(result),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Unexpected reconciliation outcome.", refundId = id.Value })
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Hospital refund reconciliation failed for hospital {HospitalId}, refund {RefundId}",
                HospitalId, id.Value);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Unable to complete refund reconciliation. No new refund was issued.", refundId = id.Value });
        }
    }

    // Swagger multipart/form-data upload configuration
    [HttpPost("disputes/{disputeId:long}/documents")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadDisputeEvidence(
        [FromRoute] long disputeId,
        IFormFile file)
    {
        if (file == null || file.Length <= 0 || file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "Upload a file up to 10 MB." });

        var allowed = new[]
        {
            "application/pdf",
            "image/jpg",
            "image/jpeg",
            "image/png"
        };

        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only PDF, JPG/JPEG and PNG evidence files are supported."
            });
        }

        var dispute = await _db.HospitalPaymentDisputes
            .Include(x => x.Transaction)
            .FirstOrDefaultAsync(x => x.Id == disputeId && x.HospitalId == HospitalId);

        if (dispute == null)
            return NotFound(new { message = "Hospital dispute not found." });

        if (dispute.Status is not ("Created" or "ActionRequired"))
        {
            return Conflict(new
            {
                message = "Evidence can only be uploaded while the dispute is open or action is required."
            });
        }

        try
        {
            using var stream = file.OpenReadStream();

            var documentId = await _gateway.UploadDisputeDocumentAsync(
                HospitalId,
                dispute.Transaction.Mode,
                stream,
                file.FileName,
                file.ContentType,
                await CredentialForTransactionAsync(dispute.Transaction));

            var ids = JsonSerializer.Deserialize<List<string>>(
                dispute.EvidenceDocumentIdsJson) ?? new List<string>();

            if (!ids.Contains(documentId, StringComparer.Ordinal))
                ids.Add(documentId);

            dispute.EvidenceDocumentIdsJson = JsonSerializer.Serialize(ids);
            dispute.UpdatedAt = DateTime.UtcNow;

            AddHospitalPaymentAudit(
                "Hospital Dispute Evidence Uploaded",
                $"Evidence document {documentId} uploaded for dispute {dispute.GatewayDisputeId}.");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                documentId,
                uploadedDocumentCount = ids.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hospital dispute evidence upload failed for dispute {DisputeId}",
                disputeId);

            return StatusCode(502, new
            {
                message = "Unable to upload dispute evidence to Razorpay."
            });
        }
    }

    [HttpPost("disputes/{disputeId:long}/contest")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ContestDispute(
        long disputeId,
        [FromBody] ContestPaymentDisputeDto dto)
    {
        var dispute = await _db.HospitalPaymentDisputes
            .Include(x => x.Transaction)
            .FirstOrDefaultAsync(x => x.Id == disputeId && x.HospitalId == HospitalId);

        if (dispute == null)
            return NotFound(new { message = "Hospital dispute not found." });

        if (dispute.Status is not ("Created" or "ActionRequired"))
        {
            return Conflict(new
            {
                message = "Only open disputes or disputes requiring action can be contested."
            });
        }

        var evidenceType = dto.EvidenceType?.Trim().ToLowerInvariant() ?? "";

        var evidenceTypes = new[]
        {
            "shipping_proof",
            "billing_proof",
            "cancellation_proof",
            "customer_communication",
            "proof_of_service",
            "explanation_letter",
            "refund_confirmation",
            "access_activity_log",
            "refund_cancellation_policy",
            "term_and_conditions"
        };

        if (!evidenceTypes.Contains(evidenceType, StringComparer.Ordinal))
            return BadRequest(new { message = "Unsupported evidence type." });

        if (string.IsNullOrWhiteSpace(dto.Summary) || dto.Summary.Length > 1000)
        {
            return BadRequest(new
            {
                message = "Summary is required and cannot exceed 1000 characters."
            });
        }

        var uploaded = JsonSerializer.Deserialize<List<string>>(
            dispute.EvidenceDocumentIdsJson) ?? new List<string>();

        var ids = dto.DocumentIds?.Distinct(StringComparer.Ordinal).ToList()
            ?? new List<string>();

        if (ids.Count == 0
            || ids.Any(x => !uploaded.Contains(x, StringComparer.Ordinal)))
        {
            return BadRequest(new
            {
                message = "Provide at least one document ID previously uploaded for this dispute."
            });
        }

        var amount = dto.Amount ?? dispute.Amount;

        if (amount <= 0
            || amount > dispute.Amount
            || decimal.Round(amount, 2) != amount)
        {
            return BadRequest(new
            {
                message = "Contested amount must be positive and cannot exceed the dispute amount."
            });
        }

        try
        {
            var result = await _gateway.ContestDisputeAsync(
                HospitalId,
                dispute.Transaction.Mode,
                dispute.GatewayDisputeId,
                amount,
                dto.Summary.Trim(),
                evidenceType,
                ids,
                await CredentialForTransactionAsync(dispute.Transaction));

            dispute.Status = result.Status.Equals(
                "under_review",
                StringComparison.OrdinalIgnoreCase)
                    ? "UnderReview"
                    : result.Status;

            dispute.PayloadJson = result.PayloadJson;
            dispute.UpdatedAt = DateTime.UtcNow;

            AddHospitalPaymentAudit(
                "Hospital Payment Dispute Contested",
                $"Dispute {dispute.GatewayDisputeId} submitted for {amount:0.00} with {ids.Count} evidence document(s).");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                dispute.GatewayDisputeId,
                dispute.Status,
                submittedAmount = amount,
                documentIds = ids
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hospital dispute contest failed for dispute {DisputeId}",
                disputeId);

            return StatusCode(502, new
            {
                message = "Unable to submit dispute evidence to Razorpay."
            });
        }
    }

    [HttpPost("disputes/{disputeId:long}/accept")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AcceptDispute(long disputeId)
    {
        var dispute = await _db.HospitalPaymentDisputes
            .Include(x => x.Transaction)
            .FirstOrDefaultAsync(x => x.Id == disputeId && x.HospitalId == HospitalId);

        if (dispute == null)
            return NotFound(new { message = "Hospital dispute not found." });

        if (dispute.Status is not ("Created" or "ActionRequired"))
            return Conflict(new { message = "Only open disputes can be accepted." });

        try
        {
            var result = await _gateway.AcceptDisputeAsync(
                HospitalId,
                dispute.Transaction.Mode,
                dispute.GatewayDisputeId,
                await CredentialForTransactionAsync(dispute.Transaction));

            dispute.Status = "Lost";
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;
            dispute.PayloadJson = result.PayloadJson;

            var bill = await _db.Billings.FirstOrDefaultAsync(
                x => x.Id == dispute.Transaction.BillingId && x.HospitalId == HospitalId);

            if (bill != null)
            {
                var paid = await _db.BillingPaymentTransactions
                    .Where(x => x.BillingId == bill.Id
                        && x.HospitalId == HospitalId
                        && x.Status == "Paid")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var refunds = await _db.HospitalPaymentRefunds
                    .Where(x => x.BillingId == bill.Id
                        && x.HospitalId == HospitalId
                        && x.Status == "processed")
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                var lost = await _db.HospitalPaymentDisputes
                    .Where(x => x.HospitalId == HospitalId
                        && x.Transaction.BillingId == bill.Id
                        && x.Status == "Lost"
                        && x.Id != dispute.Id)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0m;

                lost += dispute.Amount;

                var remaining = Math.Max(0m, paid - refunds - lost);

                bill.Status = remaining >= bill.TotalAmount
                    ? "Paid"
                    : remaining > 0
                        ? "PartiallyPaid"
                        : "Pending";
            }

            AddHospitalPaymentAudit(
                "Hospital Payment Dispute Accepted",
                $"Dispute {dispute.GatewayDisputeId} accepted; provider status {result.Status}.");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                dispute.GatewayDisputeId,
                dispute.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hospital dispute acceptance failed for dispute {DisputeId}",
                disputeId);

            return StatusCode(502, new
            {
                message = "Unable to accept dispute at Razorpay."
            });
        }
    }

    [HttpGet("disputes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDisputes([FromQuery] string? status)
    {
        var query = _db.HospitalPaymentDisputes
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.GatewayDisputeId,
                x.GatewayPaymentId,
                x.Amount,
                x.Currency,
                x.Reason,
                x.Status,
                x.EvidenceDueBy,
                x.EvidenceDocumentIdsJson,
                x.CreatedAt,
                x.UpdatedAt,
                x.ClosedAt
            })
            .ToListAsync();

        return Ok(new
        {
            hospitalId = HospitalId,
            disputes = rows
        });
    }

    [HttpPost("settlements/sync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SyncSettlements(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (from == default
            || to == default
            || to <= from
            || (to - from).TotalDays > 93)
        {
            return BadRequest(new
            {
                message = "Provide a valid UTC date range of no more than 93 days."
            });
        }

        var merchant = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.HospitalId == HospitalId);

        if (merchant == null
            || merchant.KycStatus != "Approved"
            || string.IsNullOrWhiteSpace(merchant.ProviderMerchantAccountId))
        {
            return Conflict(new
            {
                message = "Hospital merchant onboarding is not approved."
            });
        }

        try
        {
            var settlements = await _gateway.FetchSettlementsAsync(
                HospitalId,
                merchant.Mode,
                from,
                to);

            var inserted = 0;
            var updated = 0;

            foreach (var item in settlements)
            {
                var row = await _db.HospitalGatewaySettlements.FirstOrDefaultAsync(
                    x => x.HospitalId == HospitalId
                        && x.Mode == merchant.Mode
                        && x.SettlementId == item.Id);

                if (row == null)
                {
                    row = new HospitalGatewaySettlement
                    {
                        HospitalId = HospitalId,
                        Mode = merchant.Mode,
                        SettlementId = item.Id
                    };

                    _db.HospitalGatewaySettlements.Add(row);
                    inserted++;
                }
                else
                {
                    updated++;
                }

                row.Status = item.Status;
                row.Amount = item.Amount;
                row.FeeAmount = item.Fees;
                row.TaxAmount = item.Tax;
                row.Utr = item.Utr;
                row.SettledAt = item.CreatedAt;
                row.SyncedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                hospitalId = HospitalId,
                mode = merchant.Mode,
                fetched = settlements.Count,
                inserted,
                updated,
                message = "Settlement payout records synced. Payment-level reconciliation remains available through the settlement import endpoint."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hospital settlement sync failed for {HospitalId}",
                HospitalId);

            return StatusCode(502, new
            {
                message = "Unable to sync Razorpay settlements. Check merchant credentials and settlement API access."
            });
        }
    }

    [HttpPost("settlements/reconcile")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReconcileSettlements(
        [FromBody] SettlementImportDto dto)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (dto.Rows == null || dto.Rows.Count == 0 || dto.Rows.Count > 1000)
        {
            return BadRequest(new
            {
                message = "Provide between 1 and 1000 settlement rows."
            });
        }

        var matched = 0;
        var exceptions = new List<object>();
        var seenPaymentIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in dto.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.GatewayPaymentId)
                || !seenPaymentIds.Add(row.GatewayPaymentId))
            {
                exceptions.Add(new
                {
                    row.GatewayPaymentId,
                    reason = "Missing or repeated payment ID in settlement import."
                });
                continue;
            }
            var payment = await _db.HospitalPaymentTransactions.FirstOrDefaultAsync(
                x => x.HospitalId == HospitalId
                    && x.GatewayPaymentId == row.GatewayPaymentId);

            if (payment == null)
            {
                exceptions.Add(new
                {
                    row.GatewayPaymentId,
                    reason = "Payment not found for this hospital."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SettlementId) || row.SettledAt == default
                || row.SettledAt > DateTime.UtcNow.AddMinutes(10))
            {
                exceptions.Add(new
                {
                    row.GatewayPaymentId,
                    reason = "SettlementId and SettledAt are required."
                });
                continue;
            }

            if (payment.Status != "Captured" || payment.Mode is not ("Test" or "Live")
                || payment.PaymentSource is not ("Order" or "Qr")
                || row.GrossAmount != payment.Amount
                || row.GrossAmount <= 0m
                || row.FeeAmount < 0
                || row.TaxAmount < 0
                || row.FeeAmount + row.TaxAmount > row.GrossAmount)
            {
                exceptions.Add(new
                {
                    row.GatewayPaymentId,
                    reason = "Gross amount or fee/tax values do not reconcile."
                });
                continue;
            }

            try
            {
                var credentialId = await CredentialForTransactionAsync(payment);
                var gatewayPayment = await _gateway.FetchPaymentAsync(
                    HospitalId, payment.Mode, payment.GatewayPaymentId, credentialId);
                if (!gatewayPayment.Captured || gatewayPayment.Currency != "INR"
                    || gatewayPayment.AmountPaise != checked((long)(payment.Amount * 100m)))
                {
                    exceptions.Add(new { row.GatewayPaymentId, reason = "Gateway capture could not be verified." });
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Settlement payment verification failed for {PaymentId}", row.GatewayPaymentId);
                exceptions.Add(new { row.GatewayPaymentId, reason = "Merchant or gateway verification unavailable; not imported." });
                continue;
            }

            // A captured payment must not be assigned to multiple settlements.
            var existingSettlement = await _db.HospitalSettlementRecords.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HospitalId == HospitalId
                    && x.HospitalPaymentTransactionId == payment.Id);
            if (existingSettlement != null)
            {
                if (existingSettlement.SettlementId == row.SettlementId
                    && existingSettlement.GrossAmount == row.GrossAmount
                    && existingSettlement.FeeAmount == row.FeeAmount
                    && existingSettlement.TaxAmount == row.TaxAmount)
                {
                    matched++;
                }
                else
                {
                    exceptions.Add(new
                    {
                        row.GatewayPaymentId,
                        reason = "Payment already has a different settlement allocation."
                    });
                }
                continue;
            }

            var duplicate = await _db.HospitalSettlementRecords.AnyAsync(
                x => x.HospitalId == HospitalId
                    && x.SettlementId == row.SettlementId
                    && x.HospitalPaymentTransactionId == payment.Id);

            if (duplicate)
            {
                matched++;
                continue;
            }

            _db.HospitalSettlementRecords.Add(new HospitalSettlementRecord
            {
                HospitalId = HospitalId,
                HospitalPaymentTransactionId = payment.Id,
                SettlementId = row.SettlementId,
                GrossAmount = row.GrossAmount,
                FeeAmount = row.FeeAmount,
                TaxAmount = row.TaxAmount,
                NetAmount = row.GrossAmount - row.FeeAmount - row.TaxAmount,
                SettledAt = row.SettledAt,
                ReconciliationStatus = "Matched"
            });

            matched++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            matched,
            exceptionCount = exceptions.Count,
            exceptions
        });
    }

    [HttpGet("reports/revenue")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> HospitalRevenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string mode = "Live")
    {
        if (HospitalId <= 0)
            return Forbid();

        if (mode is not ("Test" or "Live" or "All"))
            return BadRequest(new { message = "Mode must be Test, Live or All." });

        var start = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var end = (to ?? DateTime.UtcNow.Date.AddDays(1)).Date.AddDays(1);

        if (end <= start || (end - start).TotalDays > 366)
        {
            return BadRequest(new
            {
                message = "Invalid date range; maximum is 366 days."
            });
        }

        var txs = _db.HospitalPaymentTransactions
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.CapturedAt >= start
                && x.CapturedAt < end
                && (mode == "All" || x.Mode == mode));

        var gross = await txs.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var refundsQuery = _db.HospitalPaymentRefunds
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.CompletedAt >= start
                && x.CompletedAt < end
                && x.Status == "processed");

        if (mode != "All")
            refundsQuery = refundsQuery.Where(x => x.Transaction.Mode == mode);

        var refunds = await refundsQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var settlementQuery = _db.HospitalSettlementRecords
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.SettledAt >= start
                && x.SettledAt < end);

        if (mode != "All")
            settlementQuery = settlementQuery.Where(x => x.Transaction.Mode == mode);

        var settlements = await settlementQuery.SumAsync(x => (decimal?)x.NetAmount) ?? 0m;
        var settlementGross = await settlementQuery.SumAsync(x => (decimal?)x.GrossAmount) ?? 0m;
        var settlementFees = await settlementQuery.SumAsync(x => (decimal?)x.FeeAmount) ?? 0m;
        var settlementTax = await settlementQuery.SumAsync(x => (decimal?)x.TaxAmount) ?? 0m;

        var manual = await _db.BillingPaymentTransactions
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.PaymentMethod != "Razorpay"
                && x.Status == "Paid"
                && x.PaidAt >= start
                && x.PaidAt < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var pending = await _db.HospitalPaymentOrders
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.CreatedAt >= start
                && x.CreatedAt < end
                && x.Status == "Pending"
                && (mode == "All" || x.Mode == mode))
            .CountAsync();

        var disputeQuery = _db.HospitalPaymentDisputes
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.CreatedAt >= start
                && x.CreatedAt < end
                && (mode == "All" || x.Transaction.Mode == mode));

        var openDisputes = await disputeQuery.CountAsync(
            x => x.Status != "Won"
                && x.Status != "Lost"
                && x.Status != "Closed");

        var lostDisputeQuery = _db.HospitalPaymentDisputes
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.Status == "Lost"
                && x.ClosedAt >= start
                && x.ClosedAt < end
                && (mode == "All" || x.Transaction.Mode == mode));

        var lostDisputeAmount =
            await lostDisputeQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var payoutQuery = _db.HospitalGatewaySettlements
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId
                && x.SettledAt >= start
                && x.SettledAt < end
                && x.Status == "processed");

        if (mode != "All")
            payoutQuery = payoutQuery.Where(x => x.Mode == mode);

        var providerSettlementPayouts =
            await payoutQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var providerSettlementFees =
            await payoutQuery.SumAsync(x => (decimal?)x.FeeAmount) ?? 0m;

        var providerSettlementTax =
            await payoutQuery.SumAsync(x => (decimal?)x.TaxAmount) ?? 0m;

        return Ok(new
        {
            hospitalId = HospitalId,
            mode,
            from = start,
            to = end.AddDays(-1),
            gatewayGrossCollections = gross,
            manualCollections = manual,
            refunds,
            netCollections = gross + manual - refunds - lostDisputeAmount,
            settledGrossAmount = settlementGross,
            providerFees = settlementFees,
            providerFeeTax = settlementTax,
            settledNetAmount = settlements,
            unsettledGatewayAmount = gross - settlementGross,
            providerSettlementPayouts,
            providerSettlementFees,
            providerSettlementTax,
            openDisputes,
            lostDisputeAmount,
            pendingOrders = pending
        });
    }
}