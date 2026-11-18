
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Controllers;

// =====================================================
// PAYMENTS - CMS OWNER RAZORPAY WEBHOOK CONTROLLER
//
// 1. Verify webhook signature.
// 2. Save webhook JSON in the durable inbox.
// 3. Prevent duplicate event records.
// 4. Reconcile payment.captured events.
// 5. Retry events that have not been processed.
//
// IMPORTANT:
// CMS subscription payments only.
// Hospital patient billing is separate.
// =====================================================

[ApiController]
[Route("api/cms/subscription-payments/webhook")]
public sealed class CmsSubscriptionWebhookController
    : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CmsRazorpayWebhookVerifier _verifier;
    private readonly IConfiguration _configuration;
    private readonly CmsRazorpayReconciliationService
        _reconciliation;
    private readonly ILogger<CmsSubscriptionWebhookController>
        _logger;

    // =====================================================
    // PAYMENTS - DEPENDENCY INJECTION
    // =====================================================

    public CmsSubscriptionWebhookController(
        AppDbContext db,
        CmsRazorpayWebhookVerifier verifier,
        IConfiguration configuration,
        CmsRazorpayReconciliationService reconciliation,
        ILogger<CmsSubscriptionWebhookController> logger)
    {
        _db = db;
        _verifier = verifier;
        _configuration = configuration;
        _reconciliation = reconciliation;
        _logger = logger;
    }

    // =====================================================
    // PAYMENTS - RECEIVE RAZORPAY WEBHOOK
    // =====================================================

    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Receive()
    {
        // =================================================
        // PAYMENTS - SERVER-CONTROLLED MERCHANT MODE
        // =================================================

        // Must be explicitly configured per webhook endpoint.
        // Never infer Test/Live from an untrusted request body or header.
        var mode = _configuration[
            "Payments:CmsOwner:WebhookMode"];

        if (mode != "Test" && mode != "Live")
        {
            return StatusCode(503, new
            {
                message = "Invalid CMS webhook configuration."
            });
        }

        // =================================================
        // PAYMENTS - READ RAZORPAY HEADERS
        // =================================================

        var signature = Request.Headers[
            "X-Razorpay-Signature"].ToString();

        var eventId = Request.Headers[
            "X-Razorpay-Event-Id"].ToString();

        // =================================================
        // PAYMENTS - READ ORIGINAL REQUEST BODY
        // =================================================

        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8);

        var rawBody = await reader.ReadToEndAsync();

        // =================================================
        // PAYMENTS - VERIFY WEBHOOK SIGNATURE
        //
        // Never save or process an unverified webhook.
        // =================================================

        if (!_verifier.Verify(
                rawBody,
                signature,
                mode))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid Razorpay webhook signature."
            });
        }

        // =================================================
        // PAYMENTS - VALIDATE EVENT ID
        // =================================================

        if (string.IsNullOrWhiteSpace(eventId) ||
            eventId.Length > 150)
        {
            return BadRequest(new
            {
                message =
                    "Invalid Razorpay webhook event ID."
            });
        }

        // =================================================
        // PAYMENTS - PARSE EVENT TYPE
        // =================================================

        string eventType;

        try
        {
            using var document = JsonDocument.Parse(
                rawBody);

            if (!document.RootElement.TryGetProperty(
                    "event",
                    out var eventElement) ||
                eventElement.ValueKind !=
                    JsonValueKind.String)
            {
                return BadRequest(new
                {
                    message =
                        "Razorpay event type is missing."
                });
            }

            eventType = eventElement.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(eventType) ||
                eventType.Length > 100)
            {
                return BadRequest(new
                {
                    message =
                        "Invalid Razorpay event type."
                });
            }
        }
        catch (JsonException)
        {
            return BadRequest(new
            {
                message =
                    "Invalid Razorpay webhook JSON."
            });
        }

        // =================================================
        // PAYMENTS - CHECK EXISTING EVENT
        // =================================================

        var existing = await _db.CmsPaymentWebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.EventId == eventId &&
                x.Mode == mode);

        if (existing != null)
        {
            // Do not allow the same event ID to be reused
            // for a different event type or payload.

            if (existing.EventType != eventType ||
                existing.PayloadJson != rawBody)
            {
                return Conflict(new
                {
                    message =
                        "Webhook event ID has conflicting content."
                });
            }

            return await ReconcileEventAsync(
                existing.Id,
                eventId,
                eventType,
                true);
        }

        // =================================================
        // PAYMENTS - CREATE DURABLE WEBHOOK RECORD
        // =================================================

        var record = new CmsPaymentWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            Mode = mode,

            // PAYMENTS - STORE ORIGINAL JSON
            PayloadJson = rawBody,

            ProcessingStatus = "Received",
            AttemptCount = 0,
            ReceivedAt = DateTime.UtcNow
        };

        _db.CmsPaymentWebhookEvents.Add(record);

        // =================================================
        // PAYMENTS - SAVE WEBHOOK EVENT
        // =================================================

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted
            // the same webhook event first.
            //
            // Detach the failed insert before continuing.

            _db.Entry(record).State =
                EntityState.Detached;

            var duplicate =
                await _db.CmsPaymentWebhookEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.EventId == eventId &&
                        x.Mode == mode);

            if (duplicate == null)
            {
                // Not a duplicate-key error.
                // Preserve the original failure.
                throw;
            }

            if (duplicate.EventType != eventType ||
                duplicate.PayloadJson != rawBody)
            {
                return Conflict(new
                {
                    message =
                        "Webhook event ID has conflicting content."
                });
            }

            return await ReconcileEventAsync(
                duplicate.Id,
                eventId,
                eventType,
                true);
        }

        // =================================================
        // PAYMENTS - RECONCILE NEW EVENT
        // =================================================

        return await ReconcileEventAsync(
            record.Id,
            eventId,
            eventType,
            false);
    }

    // =====================================================
    // PAYMENTS - PROCESS STORED WEBHOOK EVENT
    //
    // Only payment.captured is eligible for payment
    // reconciliation in the current service.
    //
    // Other events are stored for future processing.
    // =====================================================

    private async Task<IActionResult> ReconcileEventAsync(
        long recordId,
        string eventId,
        string eventType,
        bool duplicate)
    {
        // Apply duplicate protection to ALL event types, not only payment.captured.
        var alreadyStored = await _db.CmsPaymentWebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId);

        if (alreadyStored == null)
            return StatusCode(503, new { message = "Webhook record missing." });

        if (alreadyStored.ProcessingStatus == "Processed")
            return Ok(new
            {
                received = true,
                duplicate = true,
                eventId,
                processingStatus = "Processed",
                message = "Webhook already processed."
            });

        if (eventType == "refund.processed" || eventType == "refund.failed")
        {
            // Extract identifiers only from the signature-verified, stored payload.
            string? refundId;
            string? paymentId;
            long? refundAmountPaise;
            string? refundCurrency;
            try
            {
                using var refundDoc = JsonDocument.Parse(alreadyStored.PayloadJson);
                if (!refundDoc.RootElement.TryGetProperty("payload", out var payload) ||
                    payload.ValueKind != JsonValueKind.Object ||
                    !payload.TryGetProperty("refund", out var wrapper) ||
                    wrapper.ValueKind != JsonValueKind.Object ||
                    !wrapper.TryGetProperty("entity", out var entity) ||
                    entity.ValueKind != JsonValueKind.Object)
                    return BadRequest(new { message = "Refund entity is missing from webhook payload." });

                refundId = ReadJsonString(entity, "id");
                paymentId = ReadJsonString(entity, "payment_id");
                refundCurrency = ReadJsonString(entity, "currency");
                refundAmountPaise = entity.TryGetProperty("amount", out var amountElement) &&
                    amountElement.ValueKind == JsonValueKind.Number &&
                    amountElement.TryGetInt64(out var paise) && paise > 0
                        ? paise : null;
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Invalid refund webhook JSON." });
            }

            if (string.IsNullOrWhiteSpace(refundId) ||
                string.IsNullOrWhiteSpace(paymentId))
                return BadRequest(new { message = "Refund ID or payment ID is missing." });

            // Only accept a refund with an explicit amount and INR currency.
            // Razorpay reports the refund amount in the smallest currency unit.
            if (!refundAmountPaise.HasValue || refundCurrency != "INR")
                return StatusCode(503, new
                {
                    message = "Refund amount or currency could not be verified; reconciliation required."
                });

            // The gateway refund ID is authoritative. Never guess which partial
            // refund is intended from the payment ID or the newest request.
            // Lock both records to serialize concurrent webhook deliveries.
            await using var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            var refundEvent = await _db.CmsPaymentWebhookEvents
                .FromSqlInterpolated(
                    $"SELECT * FROM CmsPaymentWebhookEvents WHERE Id = {recordId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (refundEvent == null)
            {
                await tx.RollbackAsync();
                return StatusCode(503, new { message = "Refund webhook record missing." });
            }

            // Never apply evidence from a webhook whose stored identity changed
            // between the initial read and the database lock.
            if (refundEvent.EventId != alreadyStored.EventId ||
                refundEvent.EventType != alreadyStored.EventType ||
                refundEvent.Mode != alreadyStored.Mode ||
                refundEvent.PayloadJson != alreadyStored.PayloadJson)
            {
                await tx.RollbackAsync();
                return StatusCode(503, new { message = "Refund webhook changed during processing." });
            }

            if (refundEvent.ProcessingStatus == "Processed")
            {
                await tx.CommitAsync();
                return Ok(new
                {
                    received = true,
                    duplicate = true,
                    eventId,
                    processingStatus = "Processed"
                });
            }

            var savedRefund = await _db.CmsSubscriptionRefunds
                .FromSqlInterpolated(
                    $"SELECT * FROM CmsSubscriptionRefunds WHERE GatewayRefundId = {refundId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (savedRefund == null || savedRefund.Mode != refundEvent.Mode)
            {
                await tx.RollbackAsync();
                // Keep event in durable inbox. The API response may have timed out
                // before its refund ID was persisted; reconcile with Razorpay first.
                return StatusCode(503, new
                {
                    message =
                    "CMS refund ID not linked for this mode; reconciliation required."
                });
            }

            var purchase = await _db.SubscriptionPurchases
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == savedRefund.SubscriptionPurchaseId);

            if (purchase == null || purchase.GatewayMode != refundEvent.Mode ||
                purchase.GatewayPaymentId != paymentId ||
                savedRefund.Amount != refundAmountPaise.Value / 100m ||
                (!string.IsNullOrWhiteSpace(savedRefund.GatewayPaymentId) &&
                 savedRefund.GatewayPaymentId != paymentId))
            {
                await tx.RollbackAsync();
                return StatusCode(503, new
                {
                    message =
                    "Refund payment identity or amount does not match its CMS purchase."
                });
            }

            // Never let a delayed failure reverse a completed refund.
            // Likewise, a late success may resolve an earlier failed state.
            var now = DateTime.UtcNow;
            if (eventType == "refund.processed")
            {
                savedRefund.Status = "processed";
                savedRefund.CompletedAt ??= now;
            }
            else if (savedRefund.Status != "processed")
            {
                savedRefund.Status = "failed";
            }

            savedRefund.GatewayPaymentId ??= paymentId;
            refundEvent.GatewayPaymentId = paymentId;
            refundEvent.GatewayOrderId = purchase.GatewayOrderId;
            refundEvent.SubscriptionPurchaseId = purchase.Id;
            refundEvent.ProcessingStatus = "Processed";
            refundEvent.ProcessedAt = now;
            refundEvent.AttemptCount++;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Ok(new
            {
                received = true,
                duplicate,
                eventId,
                processingStatus = "Processed",
                refundId
            });
        }

        if (eventType.StartsWith("payment.dispute.", StringComparison.OrdinalIgnoreCase))
        {
            // Parse the already verified and durably stored payload.
            string? disputeId;
            string? paymentId;
            string? reason;
            string currency;
            decimal? disputeAmount;
            DateTime? due;

            try
            {
                using var disputeDoc = JsonDocument.Parse(alreadyStored.PayloadJson);
                var root = disputeDoc.RootElement;
                if (!root.TryGetProperty("payload", out var payload) ||
                    payload.ValueKind != JsonValueKind.Object ||
                    !payload.TryGetProperty("dispute", out var wrapper) ||
                    wrapper.ValueKind != JsonValueKind.Object ||
                    !wrapper.TryGetProperty("entity", out var entity) ||
                    entity.ValueKind != JsonValueKind.Object)
                    return BadRequest(new { message = "Dispute entity is missing from webhook payload." });

                disputeId = ReadJsonString(entity, "id");
                paymentId = ReadJsonString(entity, "payment_id");
                reason = ReadJsonString(entity, "reason_code");
                currency = ReadJsonString(entity, "currency") ?? "INR";
                disputeAmount = entity.TryGetProperty("amount", out var amountElement) &&
                    amountElement.ValueKind == JsonValueKind.Number &&
                    amountElement.TryGetInt64(out var paise) && paise >= 0
                        ? paise / 100m
                        : null;

                due = null;
                if (TryReadUnixDate(entity, "respond_by", out var responseDue))
                    due = responseDue;
                else if (TryReadUnixDate(entity, "evidence_due_by", out var evidenceDue))
                    due = evidenceDue;
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Invalid dispute webhook JSON." });
            }

            if (string.IsNullOrWhiteSpace(disputeId) ||
                string.IsNullOrWhiteSpace(paymentId))
                return BadRequest(new { message = "Dispute or payment ID is missing." });

            // The event lock ensures repeated deliveries of the same event
            // cannot apply its changes concurrently.
            await using var disputeTx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

            var disputeEvent = await _db.CmsPaymentWebhookEvents
                .FromSqlInterpolated(
                    $"SELECT * FROM CmsPaymentWebhookEvents WHERE Id = {recordId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (disputeEvent == null)
            {
                await disputeTx.RollbackAsync();
                return StatusCode(503, new { message = "Dispute webhook record missing." });
            }

            if (disputeEvent.ProcessingStatus == "Processed")
            {
                await disputeTx.CommitAsync();
                return Ok(new { received = true, duplicate = true, eventId, processingStatus = "Processed" });
            }

            var purchase = await _db.SubscriptionPurchases
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.GatewayPaymentId == paymentId &&
                    x.GatewayMode == disputeEvent.Mode &&
                    x.IsGatewayPaymentVerified);

            if (purchase == null)
            {
                await disputeTx.RollbackAsync();
                return StatusCode(503, new { message = "Matching CMS subscription purchase not found for dispute webhook." });
            }

            // Serializable isolation and an update lock protect an existing
            // dispute row against concurrent updates from different events.
            var saved = await _db.CmsSubscriptionDisputes
                .FromSqlInterpolated(
                    $"SELECT * FROM CmsSubscriptionDisputes WHERE GatewayDisputeId = {disputeId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (saved != null &&
                (saved.SubscriptionPurchaseId != purchase.Id ||
                 saved.GatewayPaymentId != paymentId ||
                 saved.Mode != disputeEvent.Mode))
            {
                await disputeTx.RollbackAsync();
                return StatusCode(503, new { message = "Dispute identity conflicts with an existing record." });
            }

            var status = eventType.ToLowerInvariant() switch
            {
                "payment.dispute.created" => "Created",
                "payment.dispute.under_review" => "UnderReview",
                "payment.dispute.action_required" => "ActionRequired",
                "payment.dispute.won" => "Won",
                "payment.dispute.lost" => "Lost",
                "payment.dispute.closed" when saved?.Status is "Won" or "Lost" => saved.Status,
                "payment.dispute.closed" => "Closed",
                _ => "Unknown"
            };

            var now = DateTime.UtcNow;
            if (saved == null)
            {
                saved = new CmsSubscriptionDispute
                {
                    SubscriptionPurchaseId = purchase.Id,
                    GatewayDisputeId = disputeId,
                    GatewayPaymentId = paymentId,
                    Mode = disputeEvent.Mode,
                    Amount = disputeAmount ?? purchase.Amount,
                    Currency = currency,
                    Reason = reason,
                    Status = status,
                    EvidenceDueBy = due,
                    PayloadJson = disputeEvent.PayloadJson
                };
                _db.CmsSubscriptionDisputes.Add(saved);
            }
            else if (saved.Status is not ("Won" or "Lost" or "Closed"))
            {
                // Do not let a late intermediate event reverse a terminal outcome.
                saved.Status = status;
                saved.Reason = reason ?? saved.Reason;
                saved.EvidenceDueBy = due ?? saved.EvidenceDueBy;
                saved.PayloadJson = disputeEvent.PayloadJson;
                saved.UpdatedAt = now;
            }

            if (saved.Status is "Won" or "Lost" or "Closed")
                saved.ClosedAt ??= now;

            disputeEvent.ProcessingStatus = "Processed";
            disputeEvent.ProcessedAt = now;
            disputeEvent.AttemptCount++;

            await _db.SaveChangesAsync();
            await disputeTx.CommitAsync();
            return Ok(new { received = true, duplicate, eventId, processingStatus = "Processed", disputeId, status = saved.Status });
        }

        if (eventType != "payment.captured")
        {
            return Ok(new
            {
                received = true,
                duplicate,
                eventId,
                eventType,
                processingStatus = "Received",
                message =
                    "Webhook stored. Event is not eligible for capture reconciliation."
            });
        }

        // =================================================
        // PAYMENTS - CHECK IF ALREADY PROCESSED
        // =================================================

        var stored = await _db.CmsPaymentWebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == recordId);

        if (stored == null)
        {
            return StatusCode(503, new
            {
                received = false,
                message =
                    "Webhook record could not be retrieved."
            });
        }

        if (stored.ProcessingStatus == "Processed")
        {
            return Ok(new
            {
                received = true,
                duplicate = true,
                eventId,
                processingStatus = "Processed",
                message =
                    "Webhook payment was already reconciled."
            });
        }

        // =================================================
        // PAYMENTS - VERIFY AND RECONCILE PAYMENT
        //
        // The reconciliation service:
        // - Extracts Razorpay payment/order IDs.
        // - Checks the matching subscription purchase.
        // - Independently verifies captured payment.
        // - Checks amount and gateway mode.
        // - Updates verified payment information.
        // =====================================================

        string result;

        try
        {
            result = await _reconciliation.ProcessAsync(
                recordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CMS Razorpay reconciliation failed. EventId={EventId}, RecordId={RecordId}",
                eventId,
                recordId);

            // Return 503 so the gateway can retry.
            // Do not report success on an exception.

            return StatusCode(503, new
            {
                received = true,
                eventId,
                processingStatus =
                    "ReconciliationPending",
                message =
                    "Webhook saved but payment reconciliation failed."
            });
        }

        // =================================================
        // PAYMENTS - SUCCESSFUL RECONCILIATION
        // =====================================================

        if (result == "Processed" ||
            result == "AlreadyProcessed")
        {
            return Ok(new
            {
                received = true,
                duplicate,
                eventId,
                processingStatus = "Processed",
                reconciliation = result,
                message =
                    "Razorpay captured payment verified and recorded."
            });
        }

        // =================================================
        // PAYMENTS - INCOMPLETE RECONCILIATION
        //
        // Do not mark the webhook as processed.
        // Do not mark the subscription as activated.
        // =====================================================

        _logger.LogWarning(
            "CMS Razorpay webhook reconciliation incomplete. EventId={EventId}, Result={Result}",
            eventId,
            result);

        return StatusCode(503, new
        {
            received = true,
            duplicate,
            eventId,
            processingStatus =
                "ReconciliationPending",
            reconciliation = result,
            message =
                "Webhook stored but payment reconciliation is incomplete."
        });
    }
    private static bool TryReadUnixDate(JsonElement entity, string property, out DateTime date)
    {
        date = default;
        if (!entity.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var seconds) || seconds <= 0)
            return false;

        try
        {
            date = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string? ReadJsonString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

}
