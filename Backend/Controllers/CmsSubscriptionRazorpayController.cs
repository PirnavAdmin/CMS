using System.Linq;
using Microsoft.Extensions.Logging;
using AuthDemo.Models;
using System;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

// =====================================================
// PAYMENTS - CMS OWNER SUBSCRIPTION PAYMENT APIS
//
// CMS owner collects subscription payments.
// Hospital patient billing is handled separately.
// =====================================================

public class VerifyCmsSubscriptionPaymentDto
{
    public int PurchaseId { get; set; }
    public string RazorpayOrderId { get; set; } = "";
    public string RazorpayPaymentId { get; set; } = "";
    public string RazorpaySignature { get; set; } = "";
}

[ApiController]
[Route("api/cms/subscription-payments")]
public class CmsSubscriptionRazorpayController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CmsSubscriptionRazorpayService _gateway;
    private readonly ILogger<CmsSubscriptionRazorpayController> _logger;

    public CmsSubscriptionRazorpayController(
        AppDbContext db,
        CmsSubscriptionRazorpayService gateway,
        ILogger<CmsSubscriptionRazorpayController> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    private int? CurrentUserId()
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id) ? id : null;
    }

    // =================================================
    // PAYMENTS - CREATE CMS SUBSCRIPTION ORDER
    // =================================================

    [HttpPost("create-order/{purchaseId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOrder(int purchaseId)
    {
        var userId = CurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var admin = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId.Value &&
                x.Role == "Admin" &&
                x.IsActive);

        if (admin == null)
            return Unauthorized();

        // Serialize order creation for this purchase in MySQL.
        // This prevents two simultaneous requests from creating separate orders.
        // A durable reservation/outbox is still recommended for recovery after
        // a gateway timeout or a process crash.
        await using var orderTx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var purchase = await _db.SubscriptionPurchases
            .FromSqlInterpolated(
                $"SELECT * FROM SubscriptionPurchases WHERE Id = {purchaseId} FOR UPDATE")
            .SingleOrDefaultAsync();

        if (purchase == null)
            return NotFound();

        // =================================================
        // PAYMENTS - VERIFY HOSPITAL ADMIN OWNERSHIP
        // =================================================

        var isInitial = purchase.PurchaseType == "Initial";
        if (purchase.AdminUserId != admin.Id ||
            (isInitial ? purchase.HospitalId.HasValue || admin.HospitalId.HasValue
                       : !admin.HospitalId.HasValue || purchase.HospitalId != admin.HospitalId))
            return Forbid();

        if (!admin.EmailVerified) return StatusCode(403, new { message = "Verify your email before payment." });

        if (purchase.PurchaseType != "Initial" && purchase.PurchaseType != "Renewal" &&
            purchase.PurchaseType != "Upgrade")
        {
            return BadRequest(new
            {
                message = "Unsupported subscription purchase type."
            });
        }

        if (purchase.PaymentStatus == "Paid" ||
            purchase.IsGatewayPaymentVerified)
        {
            return Conflict(new
            {
                message = "Purchase already paid or verified."
            });
        }

        if (purchase.Amount <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid subscription amount."
            });
        }

        // =================================================
        // PAYMENTS - READ CMS OWNER MERCHANT SETTINGS
        // =================================================

        var settings = await _db.CmsOwnerMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1);

        if (settings == null || !settings.IsEnabled || settings.KycStatus != "Approved" || string.IsNullOrWhiteSpace(settings.ProviderMerchantAccountId))
        {
            return Conflict(new
            {
                message = "CMS owner merchant is disabled."
            });
        }

        if (settings.Mode != "Test" &&
            settings.Mode != "Live")
        {
            return Conflict(new
            {
                message = "Invalid Razorpay merchant mode."
            });
        }

        // =================================================
        // PAYMENTS - PREVENT REUSING EXISTING ORDER
        // =================================================

        if (!string.IsNullOrWhiteSpace(purchase.GatewayOrderId))
        {
            if ((purchase.GatewayMode == "Test" || purchase.GatewayMode == "Live") &&
                !purchase.IsGatewayPaymentVerified && purchase.PaymentStatus != "Paid")
            {
                var existingKeyId = _gateway.GetPublicKeyId(
                    purchase.GatewayMode, purchase.RazorpayCredentialId);

                if (string.IsNullOrWhiteSpace(existingKeyId))
                    return Conflict(new { message = "Original Razorpay merchant credentials are unavailable. Do not create another order; reconcile the existing one." });

                return Ok(new
                {
                    purchaseId = purchase.Id,
                    orderId = purchase.GatewayOrderId,
                    keyId = existingKeyId,
                    amount = decimal.ToInt64(decimal.Round(purchase.Amount * 100m, 0, MidpointRounding.AwayFromZero)),
                    currency = "INR",
                    merchant = "CMS Owner",
                    mode = purchase.GatewayMode,
                    paymentStatus = purchase.PaymentStatus,
                    reusedExistingOrder = true
                });
            }
            return Conflict(new { message = "A Razorpay order already exists for a completed or unverifiable purchase." });
        }

        // This gateway request runs under the purchase lock to serialize
        // concurrent calls. It can hold a DB transaction during network I/O;
        // replace with a durable order reservation/outbox before production.
        // If Razorpay succeeds but the response is lost, do not blindly retry:
        // reconcile the order using the receipt and merchant account first.

        var receipt = $"cms-sub-{purchase.Id}";

        // =================================================
        // PAYMENTS - CREATE ORDER USING CMS OWNER ACCOUNT
        // =================================================

        var result = await _gateway.CreateOrderWithCredentialAsync(
            purchase.Amount,
            receipt,
            settings.Mode);

        // =================================================
        // PAYMENTS - SAVE ORDER AND ORIGINAL MERCHANT MODE
        // =================================================

        purchase.GatewayOrderId = result.OrderId;
        purchase.RazorpayCredentialId = result.CredentialId;

        // Phase 4C: bind the order to its original mode.
        purchase.GatewayMode = settings.Mode;

        purchase.PaymentStatus = "Pending";
        purchase.GatewayPaymentId = null;
        purchase.TransactionReference = null;
        purchase.IsGatewayPaymentVerified = false;
        purchase.GatewayVerifiedAt = null;
        purchase.PaidAt = null;
        purchase.PaymentFailureReason = null;

        purchase.PaymentAttemptCount += 1;
        purchase.LastPaymentAttemptAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await orderTx.CommitAsync();

        return Ok(new
        {
            purchaseId = purchase.Id,
            orderId = result.OrderId,
            keyId = result.KeyId,
            amount = decimal.ToInt64(
                decimal.Round(
                    purchase.Amount * 100m,
                    0,
                    MidpointRounding.AwayFromZero)),
            currency = "INR",
            merchant = "CMS Owner",
            mode = purchase.GatewayMode,
            paymentStatus = purchase.PaymentStatus
        });
    }

    // =================================================
    // PAYMENTS - VERIFY CMS SUBSCRIPTION PAYMENT
    // =================================================

    [HttpPost("verify")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyCmsSubscriptionPaymentDto dto)
    {
        if (dto == null ||
            dto.PurchaseId <= 0 ||
            string.IsNullOrWhiteSpace(dto.RazorpayOrderId) ||
            string.IsNullOrWhiteSpace(dto.RazorpayPaymentId) ||
            string.IsNullOrWhiteSpace(dto.RazorpaySignature))
        {
            return BadRequest(new
            {
                message = "Purchase ID, order ID, payment ID and signature are required."
            });
        }

        var userId = CurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var admin = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId.Value &&
                x.Role == "Admin" &&
                x.IsActive);

        if (admin == null)
            return Unauthorized();

        var purchase = await _db.SubscriptionPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == dto.PurchaseId);

        if (purchase == null)
            return NotFound();

        // =================================================
        // PAYMENTS - VERIFY ADMIN AND HOSPITAL OWNERSHIP
        // =================================================

        var isInitial = purchase.PurchaseType == "Initial";
        if (purchase.AdminUserId != admin.Id ||
            (isInitial ? purchase.HospitalId.HasValue || admin.HospitalId.HasValue
                       : !admin.HospitalId.HasValue || purchase.HospitalId != admin.HospitalId))
            return Forbid();

        if (!admin.EmailVerified) return StatusCode(403, new { message = "Verify your email before payment." });

        if (purchase.GatewayOrderId != dto.RazorpayOrderId)
        {
            return BadRequest(new
            {
                message = "Razorpay order mismatch."
            });
        }

        // =================================================
        // PAYMENTS - USE ORIGINAL ORDER MODE
        //
        // Do not use current merchant settings.Mode.
        // =================================================

        var mode = purchase.GatewayMode;

        if (mode != "Test" && mode != "Live")
        {
            return Conflict(new
            {
                message = "Original gateway mode is missing or invalid."
            });
        }

        // =================================================
        // PAYMENTS - VERIFY RAZORPAY CHECKOUT SIGNATURE
        // =================================================

        var validSignature = _gateway.VerifySignature(
            dto.RazorpayOrderId,
            dto.RazorpayPaymentId,
            dto.RazorpaySignature,
            mode,
            purchase.RazorpayCredentialId);

        if (!validSignature)
        {
            return BadRequest(new
            {
                message = "Invalid Razorpay payment signature."
            });
        }

        // =================================================
        // PAYMENTS - VERIFY ACTUAL CAPTURED PAYMENT
        //
        // Gateway API must confirm:
        // - payment status = captured
        // - matching Razorpay order
        // - matching purchase amount
        // - expected currency
        // =================================================

        var captured = await _gateway.IsCapturedPaymentAsync(
            dto.RazorpayPaymentId,
            dto.RazorpayOrderId,
            purchase.Amount,
            mode,
            purchase.RazorpayCredentialId);

        if (!captured)
        {
            return Conflict(new
            {
                message = "Razorpay has not confirmed a matching captured payment."
            });
        }

        // =================================================
        // PAYMENTS - TRANSACTIONAL PAYMENT RECORDING
        // =================================================

        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var locked = await _db.SubscriptionPurchases
            .FromSqlInterpolated(
                $"SELECT * FROM SubscriptionPurchases WHERE Id = {dto.PurchaseId} FOR UPDATE")
            .FirstOrDefaultAsync();

        if (locked == null)
        {
            await tx.RollbackAsync();
            return NotFound();
        }

        // Recheck tenant ownership and order identity
        // after acquiring the database lock.
        if (locked.AdminUserId != admin.Id ||
            (isInitial ? locked.HospitalId.HasValue || admin.HospitalId.HasValue : locked.HospitalId != admin.HospitalId) ||
            locked.GatewayOrderId != dto.RazorpayOrderId ||
            locked.GatewayMode != mode ||
            locked.RazorpayCredentialId != purchase.RazorpayCredentialId ||
            locked.Amount != purchase.Amount)
        {
            await tx.RollbackAsync();

            return Conflict(new
            {
                message = "Purchase ownership or payment details changed."
            });
        }

        // =================================================
        // PAYMENTS - IDEMPOTENT SUCCESS
        // =================================================

        if (locked.IsGatewayPaymentVerified &&
            locked.PaymentStatus == "Paid" &&
            locked.GatewayPaymentId == dto.RazorpayPaymentId)
        {
            await tx.CommitAsync();

            return Ok(new
            {
                purchaseId = locked.Id,
                paymentStatus = locked.PaymentStatus,
                gatewayPaymentId = locked.GatewayPaymentId,
                isGatewayPaymentVerified = true,
                gatewayVerifiedAt = locked.GatewayVerifiedAt,
                subscriptionActivated = locked.ActivatedAt.HasValue,
                alreadyProcessed = true
            });
        }

        // Never replace another recorded payment ID.
        if (!string.IsNullOrWhiteSpace(locked.GatewayPaymentId) &&
            locked.GatewayPaymentId != dto.RazorpayPaymentId)
        {
            await tx.RollbackAsync();

            return Conflict(new
            {
                message = "A different gateway payment is already recorded."
            });
        }

        // Prevent modifying an activated purchase.
        if (locked.ActivatedAt.HasValue)
        {
            await tx.RollbackAsync();

            return Conflict(new
            {
                message = "Purchase already activated; payment evidence cannot be replaced."
            });
        }

        // =================================================
        // PAYMENTS - RECORD VERIFIED CAPTURED PAYMENT
        // =================================================

        var verifiedAt = DateTime.UtcNow;

        locked.GatewayPaymentId = dto.RazorpayPaymentId;
        locked.TransactionReference = dto.RazorpayPaymentId;

        locked.IsGatewayPaymentVerified = true;
        locked.GatewayVerifiedAt = verifiedAt;

        locked.PaymentStatus = "Paid";
        if (isInitial) locked.AdminCreationStatus = "Created";
        locked.PaidAt ??= verifiedAt;
        locked.PaymentFailureReason = null;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            purchaseId = locked.Id,
            paymentStatus = locked.PaymentStatus,
            gatewayPaymentId = locked.GatewayPaymentId,
            gatewayMode = locked.GatewayMode,
            isGatewayPaymentVerified = locked.IsGatewayPaymentVerified,
            gatewayVerifiedAt = locked.GatewayVerifiedAt,
            paidAt = locked.PaidAt,
            subscriptionActivated = false,
            message = isInitial
                ? "Captured Initial subscription payment verified. Admin onboarding is active; create your clinic to activate the subscription."
                : "Captured Razorpay payment verified and recorded."
        });
    }

    // =================================================
    // PAYMENTS - GET SUBSCRIPTION PAYMENT STATUS
    // =================================================

    [HttpGet("{purchaseId:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStatus(int purchaseId)
    {
        var userId = CurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var admin = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId.Value &&
                x.Role == "Admin" &&
                x.IsActive);

        if (admin == null)
            return Unauthorized();

        var purchase = await _db.SubscriptionPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == purchaseId && x.AdminUserId == admin.Id &&
                (x.PurchaseType == "Initial"
                    ? x.HospitalId == null && admin.HospitalId == null
                    : x.HospitalId == admin.HospitalId && admin.HospitalId != null));

        if (purchase == null)
            return NotFound();

        return Ok(new
        {
            purchase.Id,
            purchase.PurchaseType,
            purchase.Amount,
            purchase.PaymentStatus,
            purchase.GatewayOrderId,
            purchase.GatewayPaymentId,
            purchase.GatewayMode,
            purchase.IsGatewayPaymentVerified,
            purchase.GatewayVerifiedAt,
            purchase.PaidAt,
            purchase.ActivatedAt,
            subscriptionActivated = purchase.ActivatedAt.HasValue
        });
    }
    // =================================================
    // PAYMENTS - CMS SUBSCRIPTION REFUND
    // =================================================
    public sealed class CmsRefundRequestDto
    {
        public decimal Amount { get; set; }
        public string? Reason { get; set; }
        public string? IdempotencyKey { get; set; }
    }

    [HttpPost("refunds/{purchaseId:int}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RefundPurchase(
        int purchaseId,
        [FromBody] CmsRefundRequestDto dto)
    {
        if (purchaseId <= 0)
            return BadRequest(new { message = "Invalid subscription purchase ID." });

        if (dto == null)
            return BadRequest(new { message = "Refund details are required." });

        if (dto.Amount <= 0m ||
            decimal.Round(dto.Amount, 2) != dto.Amount)
        {
            return BadRequest(new { message = "Refund amount must be positive and have at most two decimal places." });
        }

        if (dto.Reason != null && dto.Reason.Length > 300)
            return BadRequest(new { message = "Refund reason cannot exceed 300 characters." });

        if (dto.IdempotencyKey != null &&
            (string.IsNullOrWhiteSpace(dto.IdempotencyKey) ||
             dto.IdempotencyKey.Length > 100))
        {
            return BadRequest(new { message = "Idempotency key must be 1 to 100 characters." });
        }

        // Clients should reuse this key when retrying the same request.
        // Older callers that omit it remain supported; their requests
        // receive a newly generated key in the response.
        var requestKey = dto.IdempotencyKey ?? Guid.NewGuid().ToString("N");
        CmsSubscriptionRefund refund;
        string paymentId;
        string mode;
        int? credentialId;

        // Lock the purchase while calculating and reserving the refundable
        // balance. Never make an external gateway call inside this transaction.
        await using (var tx = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable))
        {
            var purchase = await _db.SubscriptionPurchases
                .FromSqlInterpolated(
                    $"SELECT * FROM SubscriptionPurchases WHERE Id = {purchaseId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (purchase == null)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "Subscription purchase not found." });
            }

            // Check an existing request before evaluating the remaining balance.
            // Repeated requests must never trigger another gateway refund.
            var existing = await _db.CmsSubscriptionRefunds
                .AsNoTracking()
                .SingleOrDefaultAsync(x =>
                    x.SubscriptionPurchaseId == purchaseId &&
                    x.IdempotencyKey == requestKey);

            if (existing != null)
            {
                await tx.CommitAsync();

                if (existing.Amount != dto.Amount ||
                    !string.Equals(existing.Reason, dto.Reason, StringComparison.Ordinal))
                {
                    return Conflict(new
                    {
                        message = "This idempotency key was already used for different refund details.",
                        existingRefundId = existing.Id
                    });
                }

                // A requested refund may have reached Razorpay already.
                // Do not retry it automatically, even if the gateway ID
                // is still a local REQUESTED-* placeholder.
                return Ok(new
                {
                    existing.Id,
                    existing.GatewayRefundId,
                    existing.GatewayPaymentId,
                    existing.Amount,
                    existing.Status,
                    purchaseId,
                    idempotencyKey = requestKey,
                    alreadyRequested = true,
                    requiresReconciliation = existing.Status == "requested"
                });
            }

            if (!purchase.IsGatewayPaymentVerified ||
                purchase.PaymentStatus != "Paid" ||
                string.IsNullOrWhiteSpace(purchase.GatewayPaymentId) ||
                purchase.GatewayMode is not ("Test" or "Live"))
            {
                await tx.RollbackAsync();
                return Conflict(new
                {
                    message = "Only independently verified Razorpay purchases can be refunded."
                });
            }

            var reserved = await _db.CmsSubscriptionRefunds
                .Where(x => x.SubscriptionPurchaseId == purchaseId &&
                            x.Status != "failed")
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var refundable = purchase.Amount - reserved;
            if (dto.Amount > refundable)
            {
                await tx.RollbackAsync();
                return BadRequest(new
                {
                    message = "Refund amount exceeds the refundable purchase balance.",
                    refundableAmount = refundable
                });
            }

            paymentId = purchase.GatewayPaymentId;
            mode = purchase.GatewayMode;
            credentialId = purchase.RazorpayCredentialId;

            refund = new CmsSubscriptionRefund
            {
                SubscriptionPurchaseId = purchase.Id,
                HospitalId = purchase.HospitalId,
                GatewayPaymentId = paymentId,
                IdempotencyKey = requestKey,
                GatewayRefundId = "REQUESTED-" + Guid.NewGuid().ToString("N"),
                Amount = dto.Amount,
                Mode = mode,
                Status = "requested",
                Reason = dto.Reason
            };

            _db.CmsSubscriptionRefunds.Add(refund);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        try
        {
            // Always use the original payment's merchant mode and credential.
            var result = await _gateway.RefundAsync(
                paymentId,
                dto.Amount,
                mode,
                dto.Reason,
                credentialId);

            if (string.IsNullOrWhiteSpace(result.RefundId))
                throw new InvalidOperationException("Razorpay refund response did not contain a refund ID.");

            // The webhook may have updated this refund while the gateway call
            // was in progress. Reload and lock the row before saving the result.
            // Never regress a terminal webhook outcome to an earlier status.
            _db.Entry(refund).State = EntityState.Detached;
            await using var resultTx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            var storedRefund = await _db.CmsSubscriptionRefunds
                .FromSqlInterpolated(
                    $"SELECT * FROM CmsSubscriptionRefunds WHERE Id = {refund.Id} FOR UPDATE")
                .SingleAsync();

            if (!string.IsNullOrWhiteSpace(storedRefund.GatewayRefundId) &&
                !storedRefund.GatewayRefundId.StartsWith("REQUESTED-", StringComparison.Ordinal) &&
                storedRefund.GatewayRefundId != result.RefundId)
            {
                await resultTx.RollbackAsync();
                return Conflict(new
                {
                    message = "Refund gateway ID differs from the previously recorded refund. Manual reconciliation required.",
                    refundId = storedRefund.Id,
                    purchaseId,
                    idempotencyKey = requestKey,
                    requiresReconciliation = true
                });
            }

            storedRefund.GatewayRefundId = result.RefundId;
            if (storedRefund.Status != "processed" &&
                storedRefund.Status != "failed")
            {
                // An unknown status is not proof of failure. Keep the
                // reservation until the gateway outcome is reconciled.
                if (result.Status == "processed" ||
                    result.Status == "failed" ||
                    result.Status == "pending" ||
                    result.Status == "created")
                    storedRefund.Status = result.Status;
                if (result.Status == "processed")
                    storedRefund.CompletedAt ??= DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            await resultTx.CommitAsync();

            return Ok(new
            {
                storedRefund.Id,
                storedRefund.GatewayRefundId,
                storedRefund.GatewayPaymentId,
                storedRefund.Amount,
                storedRefund.Status,
                purchaseId,
                idempotencyKey = requestKey,
                requiresReconciliation = storedRefund.Status != "processed" && storedRefund.Status != "failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CMS refund outcome requires reconciliation. RefundId={RefundId}, PurchaseId={PurchaseId}",
                refund.Id, purchaseId);

            // A timeout, lost response, or failed database update does NOT
            // prove Razorpay rejected the refund. Keep the reserved record
            // for manual/webhook reconciliation; never issue a blind retry.
            return StatusCode(502, new
            {
                message = "Refund outcome is uncertain. Check Razorpay and reconcile this request before retrying.",
                refundId = refund.Id,
                purchaseId,
                idempotencyKey = requestKey,
                requiresReconciliation = true
            });
        }
    }

    // =================================================
    // CMS REFUND RECONCILIATION - SUPERADMIN ONLY
    // =================================================
    // Read-only Razorpay lookup: this endpoint NEVER issues a new refund.
    [HttpPost("refunds/{refundId:long}/reconcile")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ReconcileRefund(long refundId)
    {
        if (refundId <= 0)
            return BadRequest(new { message = "A valid refund ID is required." });

        var snapshot = await _db.CmsSubscriptionRefunds.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == refundId);
        if (snapshot == null)
            return NotFound(new { message = "CMS refund not found." });

        var purchase = await _db.SubscriptionPurchases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == snapshot.SubscriptionPurchaseId);
        if (purchase == null ||
            string.IsNullOrWhiteSpace(purchase.GatewayPaymentId) ||
            purchase.GatewayMode is not ("Test" or "Live") ||
            purchase.GatewayMode != snapshot.Mode ||
            purchase.GatewayPaymentId != snapshot.GatewayPaymentId)
            return Conflict(new
            {
                message = "Original CMS payment identity or merchant mode cannot be verified. Manual review required."
            });

        // A REQUESTED-* ID is only a local reservation. An amount-only match
        // cannot safely prove which Razorpay refund belongs to this request.
        if (string.IsNullOrWhiteSpace(snapshot.GatewayRefundId) ||
            snapshot.GatewayRefundId.StartsWith("REQUESTED-", StringComparison.Ordinal))
            return Conflict(new
            {
                message = "Refund has no confirmed Razorpay ID. Review Razorpay refunds for the original payment and link the correct ID through an audited manual recovery process.",
                refundId = snapshot.Id,
                requiresManualReview = true
            });

        (string RefundId, string PaymentId, long AmountPaise,
            string? Currency, string Status) gatewayRefund;
        try
        {
            gatewayRefund = await _gateway.FetchRefundAsync(
                snapshot.GatewayRefundId, snapshot.Mode, purchase.RazorpayCredentialId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CMS refund lookup failed for local refund {RefundId}", refundId);
            return StatusCode(502, new
            {
                message = "Unable to confirm the refund with Razorpay. No local status was changed.",
                refundId,
                requiresReconciliation = true
            });
        }

        if (gatewayRefund.RefundId != snapshot.GatewayRefundId ||
            gatewayRefund.PaymentId != snapshot.GatewayPaymentId ||
            gatewayRefund.Currency != "INR" ||
            gatewayRefund.AmountPaise != checked(decimal.ToInt64(snapshot.Amount * 100m)) ||
            snapshot.Amount * 100m != gatewayRefund.AmountPaise)
            return Conflict(new
            {
                message = "Gateway refund identity, currency or amount mismatch. Manual review required.",
                refundId,
                requiresManualReview = true
            });

        if (gatewayRefund.Status is not ("created" or "pending" or "processed" or "failed"))
            return Conflict(new
            {
                message = "Unknown gateway refund status. Manual review required.",
                refundId,
                gatewayStatus = gatewayRefund.Status
            });

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var locked = await _db.CmsSubscriptionRefunds
            .FromSqlInterpolated(
                $"SELECT * FROM CmsSubscriptionRefunds WHERE Id = {refundId} FOR UPDATE")
            .SingleOrDefaultAsync();

        if (locked == null ||
            locked.SubscriptionPurchaseId != snapshot.SubscriptionPurchaseId ||
            locked.GatewayRefundId != snapshot.GatewayRefundId ||
            locked.GatewayPaymentId != snapshot.GatewayPaymentId ||
            locked.Mode != snapshot.Mode ||
            locked.Amount != snapshot.Amount)
            return Conflict(new
            {
                message = "Refund changed during reconciliation. Retry after reviewing the record.",
                refundId
            });

        // Never let an older gateway observation undo a processed refund.
        if (locked.Status == "processed" && gatewayRefund.Status != "processed")
            return Conflict(new
            {
                message = "Local refund is already processed but the gateway returned a different status. Manual review required.",
                refundId,
                localStatus = locked.Status,
                gatewayStatus = gatewayRefund.Status
            });

        // A previously failed refund unexpectedly becoming processed needs
        // manual verification, not an automatic accounting change.
        if (locked.Status == "failed" && gatewayRefund.Status != "failed")
            return Conflict(new
            {
                message = "A terminal failed refund has a conflicting gateway status. Manual review required.",
                refundId,
                localStatus = locked.Status,
                gatewayStatus = gatewayRefund.Status
            });

        locked.Status = gatewayRefund.Status;
        if (locked.Status == "processed")
            locked.CompletedAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            refundId = locked.Id,
            purchaseId = locked.SubscriptionPurchaseId,
            gatewayRefundId = locked.GatewayRefundId,
            status = locked.Status,
            completedAt = locked.CompletedAt,
            requiresReconciliation = locked.Status is "created" or "pending"
        });
    }

}
