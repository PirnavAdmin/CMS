
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

/// <summary>
/// Shared hospital payment allocation service.
///
/// IMPORTANT:
/// The caller must verify the captured Razorpay payment
/// using the correct hospital merchant credentials BEFORE
/// calling either allocation method.
///
/// This service does not contact Razorpay.
/// </summary>
public sealed class HospitalPaymentAllocationService
{
    private readonly AppDbContext _db;

    public HospitalPaymentAllocationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Allocates an already-verified Razorpay order payment.
    /// Returns true when successfully allocated or when
    /// an exact, completed allocation already exists.
    /// </summary>
    public async Task<bool> AllocateOrderAsync(
        int hospitalId,
        long orderId,
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0 ||
            orderId <= 0 ||
            string.IsNullOrWhiteSpace(paymentId) ||
            paymentId.Length > 150)
        {
            return false;
        }

        await using var tx =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Lock the payment order for allocation.
        var order = await _db.HospitalPaymentOrders
            .FromSqlInterpolated(
                $"SELECT * FROM [HospitalPaymentOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId} AND [HospitalId] = {hospitalId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (order == null ||
            string.IsNullOrWhiteSpace(order.GatewayOrderId) ||
            order.Amount <= 0m ||
            order.Mode is not ("Test" or "Live"))
        {
            return false;
        }

        // Check both financial ledgers before inserting.
        var existingCapture =
            await _db.HospitalPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.GatewayPaymentId == paymentId,
                    cancellationToken);

        var existingLedger =
            await _db.BillingPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ReferenceNumber == paymentId &&
                         x.PaymentMethod == "Razorpay",
                    cancellationToken);

        if (existingCapture != null || existingLedger != null)
        {
            // Exact completed allocations are idempotent.
            // Partial or conflicting allocations are not.
            return existingCapture != null &&
                   existingLedger != null &&
                   existingCapture.HospitalPaymentOrderId == order.Id &&
                   existingCapture.HospitalPaymentQrId == null &&
                   existingCapture.PaymentSource == "Order" &&
                   existingCapture.HospitalId == hospitalId &&
                   existingCapture.BillingId == order.BillingId &&
                   existingCapture.GatewayOrderId == order.GatewayOrderId &&
                   existingCapture.GatewayPaymentId == paymentId &&
                   existingCapture.Mode == order.Mode &&
                   existingCapture.Status == "Captured" &&
                   existingCapture.Amount == order.Amount &&
                   existingCapture.BillingPaymentTransactionId == existingLedger.Id &&
                   existingLedger.HospitalId == hospitalId &&
                   existingLedger.BillingId == order.BillingId &&
                   existingLedger.Amount == order.Amount &&
                   existingLedger.Status == "Paid" &&
                   order.Status == "Captured" &&
                   order.GatewayPaymentId == paymentId;
        }

        if (order.Status == "Captured" ||
            (!string.IsNullOrWhiteSpace(order.GatewayPaymentId) &&
             order.GatewayPaymentId != paymentId))
        {
            return false;
        }

        // Lock the invoice before calculating the balance.
        var bill = await _db.Billings
            .FromSqlInterpolated(
                $"SELECT * FROM [Billings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {order.BillingId} AND [HospitalId] = {hospitalId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (bill == null)
            return false;

        var paid = await _db.BillingPaymentTransactions
            .Where(x =>
                x.BillingId == bill.Id &&
                x.HospitalId == hospitalId &&
                x.Status == "Paid")
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        var refunded = await _db.HospitalPaymentRefunds
            .Where(x =>
                x.BillingId == bill.Id &&
                x.HospitalId == hospitalId &&
                x.Status == "processed")
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        var due = bill.TotalAmount - paid + refunded;

        if (due <= 0m || order.Amount > due)
        {
            order.Status = "ReviewRequired";

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return false;
        }

        var now = DateTime.UtcNow;

        var ledger = new BillingPaymentTransaction
        {
            BillingId = bill.Id,
            HospitalId = hospitalId,
            BranchId = bill.BranchId,
            Amount = order.Amount,
            PaymentMethod = "Razorpay",
            ReferenceNumber = paymentId,
            Status = "Paid",
            CreatedAt = now,
            PaidAt = now
        };

        _db.BillingPaymentTransactions.Add(ledger);

        await _db.SaveChangesAsync(cancellationToken);

        var hospitalTransaction = new HospitalPaymentTransaction
        {
            HospitalPaymentOrderId = order.Id,
            HospitalPaymentQrId = null,
            PaymentSource = "Order",
            HospitalId = hospitalId,
            BillingId = bill.Id,
            BillingPaymentTransactionId = ledger.Id,
            GatewayPaymentId = paymentId,
            GatewayOrderId = order.GatewayOrderId!,
            Amount = order.Amount,
            Mode = order.Mode,
            Status = "Captured",
            CapturedAt = now
        };

        _db.HospitalPaymentTransactions.Add(hospitalTransaction);

        order.GatewayPaymentId = paymentId;
        order.Status = "Captured";
        order.PaidAt = now;

        var totalPaid = paid + order.Amount - refunded;

        bill.Status = totalPaid >= bill.TotalAmount
            ? "Paid"
            : "PartiallyPaid";

        bill.PaymentMode = "Razorpay";

        if (bill.Status == "Paid" && bill.BillingType == "OP")
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(
                    x => x.Id == bill.AppointmentId &&
                         x.HospitalId == hospitalId,
                    cancellationToken);

            if (appointment != null)
            {
                appointment.PaymentStatus = "Paid";
                appointment.PaymentMode = "Razorpay";

                if (appointment.Status == "Pending")
                    appointment.Status = "Confirmed";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Allocates an already-verified QR payment.
    /// Does not fabricate a webhook or bypass merchant verification.
    /// </summary>
    public async Task<bool> AllocateQrAsync(
        int hospitalId,
        long qrId,
        string gatewayPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (hospitalId <= 0 ||
            qrId <= 0 ||
            string.IsNullOrWhiteSpace(gatewayPaymentId) ||
            gatewayPaymentId.Length > 150)
        {
            return false;
        }

        await using var tx =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // Lock the QR record.
        var qr = await _db.HospitalPaymentQrs
            .FromSqlInterpolated(
                $"SELECT * FROM [HospitalPaymentQrs] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {qrId} AND [HospitalId] = {hospitalId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (qr == null ||
            string.IsNullOrWhiteSpace(qr.GatewayQrId) ||
            qr.Mode is not ("Test" or "Live") ||
            qr.Amount <= 0m ||
            (!string.IsNullOrWhiteSpace(qr.GatewayPaymentId) &&
             qr.GatewayPaymentId != gatewayPaymentId))
        {
            return false;
        }

        var existingCapture =
            await _db.HospitalPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.GatewayPaymentId == gatewayPaymentId,
                    cancellationToken);

        var existingLedger =
            await _db.BillingPaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PaymentMethod == "Razorpay" &&
                         x.ReferenceNumber == gatewayPaymentId,
                    cancellationToken);

        if (existingCapture != null || existingLedger != null)
        {
            return existingCapture != null &&
                   existingLedger != null &&
                   existingCapture.PaymentSource == "Qr" &&
                   existingCapture.HospitalPaymentQrId == qr.Id &&
                   existingCapture.HospitalPaymentOrderId == null &&
                   existingCapture.HospitalId == hospitalId &&
                   existingCapture.BillingId == qr.BillingId &&
                   existingCapture.BillingPaymentTransactionId == existingLedger.Id &&
                   existingCapture.GatewayPaymentId == gatewayPaymentId &&
                   existingCapture.Mode == qr.Mode &&
                   existingCapture.Status == "Captured" &&
                   existingCapture.Amount == qr.Amount &&
                   existingLedger.HospitalId == hospitalId &&
                   existingLedger.BillingId == qr.BillingId &&
                   existingLedger.Amount == qr.Amount &&
                   existingLedger.Status == "Paid" &&
                   qr.Status == "Paid" &&
                   qr.GatewayPaymentId == gatewayPaymentId;
        }

        if (qr.Status is not ("Active" or "Creating"))
            return false;

        // Lock the invoice.
        var bill = await _db.Billings
            .FromSqlInterpolated(
                $"SELECT * FROM [Billings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {qr.BillingId} AND [HospitalId] = {hospitalId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (bill == null)
            return false;

        var paid = await _db.BillingPaymentTransactions
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BillingId == bill.Id &&
                x.Status == "Paid")
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        var refunded = await _db.HospitalPaymentRefunds
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BillingId == bill.Id &&
                x.Status == "processed")
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        var due = bill.TotalAmount - paid + refunded;

        if (due <= 0m || qr.Amount > due)
            return false;

        var now = DateTime.UtcNow;

        var ledger = new BillingPaymentTransaction
        {
            BillingId = bill.Id,
            HospitalId = hospitalId,
            BranchId = bill.BranchId,
            Amount = qr.Amount,
            PaymentMethod = "Razorpay",
            ReferenceNumber = gatewayPaymentId,
            Status = "Paid",
            CreatedAt = now,
            PaidAt = now
        };

        _db.BillingPaymentTransactions.Add(ledger);

        await _db.SaveChangesAsync(cancellationToken);

        var hospitalTransaction = new HospitalPaymentTransaction
        {
            HospitalPaymentQrId = qr.Id,
            HospitalPaymentOrderId = null,
            PaymentSource = "Qr",
            HospitalId = hospitalId,
            BillingId = bill.Id,
            BillingPaymentTransactionId = ledger.Id,
            GatewayPaymentId = gatewayPaymentId,
            GatewayOrderId = null,
            Amount = qr.Amount,
            Mode = qr.Mode,
            Status = "Captured",
            CapturedAt = now
        };

        _db.HospitalPaymentTransactions.Add(hospitalTransaction);

        qr.GatewayPaymentId = gatewayPaymentId;
        qr.Status = "Paid";
        qr.PaidAtUtc = now;

        var totalPaid = paid + qr.Amount - refunded;

        bill.Status = totalPaid >= bill.TotalAmount
            ? "Paid"
            : "PartiallyPaid";

        bill.PaymentMode = "Razorpay";

        if (bill.Status == "Paid" && bill.BillingType == "OP")
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(
                    x => x.Id == bill.AppointmentId &&
                         x.HospitalId == hospitalId,
                    cancellationToken);

            if (appointment != null)
            {
                appointment.PaymentStatus = "Paid";
                appointment.PaymentMode = "Razorpay";

                if (appointment.Status == "Pending")
                    appointment.Status = "Confirmed";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return true;
    }
}
