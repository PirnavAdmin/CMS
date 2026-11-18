using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

// CMS owner subscription payment reconciliation only.
// Razorpay payment evidence is independently verified before database updates.
public sealed class CmsRazorpayReconciliationService
{
    private readonly AppDbContext _db;
    private readonly CmsSubscriptionRazorpayService _gateway;

    public CmsRazorpayReconciliationService(
        AppDbContext db,
        CmsSubscriptionRazorpayService gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task<string> ProcessAsync(long eventId)
    {
        var webhook = await _db.CmsPaymentWebhookEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == eventId);

        if (webhook == null)
            return "EventNotFound";

        if (webhook.ProcessingStatus == "Processed")
            return "AlreadyProcessed";

        if (webhook.EventType != "payment.captured")
            return "UnsupportedEvent";

        if (webhook.Mode is not ("Test" or "Live"))
            return "InvalidMode";

        string? paymentId;
        string? orderId;

        try
        {
            using var json = JsonDocument.Parse(webhook.PayloadJson);
            var payment = json.RootElement
                .GetProperty("payload")
                .GetProperty("payment")
                .GetProperty("entity");

            paymentId = payment.GetProperty("id").GetString();
            orderId = payment.GetProperty("order_id").GetString();
        }
        catch (Exception ex) when (
            ex is JsonException ||
            ex is KeyNotFoundException ||
            ex is InvalidOperationException)
        {
            return "InvalidPayload";
        }

        if (string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(orderId))
            return "InvalidPaymentIdentity";

        var purchase = await _db.SubscriptionPurchases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.GatewayOrderId == orderId);

        if (purchase == null)
            return "PurchaseNotFound";

        if (purchase.GatewayMode != webhook.Mode)
            return "GatewayModeMismatch";

        // Do not trust the webhook's capture claim. Verify against Razorpay
        // using the credential version originally used to create the order.
        var captured = await _gateway.IsCapturedPaymentAsync(
            paymentId,
            orderId,
            purchase.Amount,
            webhook.Mode,
            purchase.RazorpayCredentialId);

        if (!captured)
            return "PaymentNotCapturedOrMismatch";

        await using var tx = await _db.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        // Use the same locking order for every capture reconciliation.
        var lockedPurchase = await _db.SubscriptionPurchases
            .FromSqlInterpolated(
                $"SELECT * FROM SubscriptionPurchases WHERE Id = {purchase.Id} FOR UPDATE")
            .SingleAsync();

        var lockedEvent = await _db.CmsPaymentWebhookEvents
            .FromSqlInterpolated(
                $"SELECT * FROM CmsPaymentWebhookEvents WHERE Id = {eventId} FOR UPDATE")
            .SingleAsync();

        if (lockedEvent.ProcessingStatus == "Processed")
        {
            await tx.CommitAsync();
            return "AlreadyProcessed";
        }

        // The verified evidence must still match the persisted event.
        if (lockedEvent.EventType != "payment.captured" ||
            lockedEvent.Mode != webhook.Mode ||
            lockedEvent.EventId != webhook.EventId ||
            lockedEvent.PayloadJson != webhook.PayloadJson)
        {
            await tx.RollbackAsync();
            return "WebhookChanged";
        }

        if (lockedPurchase.GatewayOrderId != orderId ||
            lockedPurchase.GatewayMode != webhook.Mode ||
            lockedPurchase.Amount != purchase.Amount ||
            lockedPurchase.RazorpayCredentialId != purchase.RazorpayCredentialId)
        {
            await tx.RollbackAsync();
            return "PurchaseChanged";
        }

        // A gateway payment must never be applied to another subscription.
        // Serializable isolation protects the lookup within this transaction;
        // a unique database index is still required for cross-process safety.
        var paymentAssignedElsewhere = await _db.SubscriptionPurchases
            .AsNoTracking()
            .AnyAsync(x => x.Id != lockedPurchase.Id &&
                           x.GatewayPaymentId == paymentId);

        if (paymentAssignedElsewhere)
        {
            await tx.RollbackAsync();
            return "PaymentAlreadyAssignedToAnotherPurchase";
        }

        if (!string.IsNullOrWhiteSpace(lockedPurchase.GatewayPaymentId) &&
            lockedPurchase.GatewayPaymentId != paymentId)
        {
            await tx.RollbackAsync();
            return "DifferentPaymentAlreadyRecorded";
        }

        if (lockedPurchase.ActivatedAt.HasValue &&
            (!lockedPurchase.IsGatewayPaymentVerified ||
             lockedPurchase.GatewayPaymentId != paymentId))
        {
            await tx.RollbackAsync();
            return "ActivatedPurchaseConflict";
        }

        var now = DateTime.UtcNow;

        lockedPurchase.GatewayPaymentId = paymentId;
        lockedPurchase.TransactionReference = paymentId;
        lockedPurchase.IsGatewayPaymentVerified = true;
        lockedPurchase.GatewayVerifiedAt ??= now;
        lockedPurchase.PaymentStatus = "Paid";

        if (lockedPurchase.PurchaseType == "Initial" &&
            lockedPurchase.AdminUserId.HasValue &&
            !lockedPurchase.HospitalId.HasValue)
        {
            lockedPurchase.AdminCreationStatus = "Created";
        }

        lockedPurchase.PaidAt ??= now;
        lockedPurchase.PaymentFailureReason = null;

        lockedEvent.GatewayOrderId = orderId;
        lockedEvent.GatewayPaymentId = paymentId;
        lockedEvent.SubscriptionPurchaseId = lockedPurchase.Id;
        lockedEvent.ProcessingStatus = "Processed";
        lockedEvent.ProcessedAt = now;
        lockedEvent.AttemptCount++;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return "Processed";
    }
}
