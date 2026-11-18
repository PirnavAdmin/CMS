
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Services;

/// <summary>
/// Creates hospital-specific Razorpay payment orders.
///
/// The caller must authorize access to the billing record
/// before invoking this service.
///
/// This service does not capture payments, reconcile payments,
/// or allocate money to invoices.
/// </summary>
public sealed class HospitalPaymentOrderService
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;
    private readonly ILogger<HospitalPaymentOrderService> _logger;

    public HospitalPaymentOrderService(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway,
        ILogger<HospitalPaymentOrderService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<IActionResult> CreateAsync(
        int hospitalId,
        Billing bill,
        decimal? amount,
        string? idempotencyKey)
    {
        if (hospitalId <= 0 ||
            bill == null ||
            bill.HospitalId != hospitalId)
        {
            return new ForbidResult();
        }

        if (bill.TotalAmount <= 0)
        {
            return new BadRequestObjectResult(new
            {
                message = "Invoice total must be greater than zero."
            });
        }

        if (decimal.Round(bill.TotalAmount, 2) !=
            bill.TotalAmount)
        {
            return new BadRequestObjectResult(new
            {
                message = "Invoice total has invalid decimal precision."
            });
        }

        idempotencyKey = idempotencyKey?.Trim();

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            idempotencyKey = null;

        if (idempotencyKey?.Length > 100)
        {
            return new BadRequestObjectResult(new
            {
                message = "IdempotencyKey cannot exceed 100 characters."
            });
        }

        if (amount.HasValue &&
            (amount.Value <= 0 ||
             decimal.Round(amount.Value, 2) != amount.Value))
        {
            return new BadRequestObjectResult(new
            {
                message = "Payment amount must be positive " +
                          "with at most two decimal places."
            });
        }

        // Check existing order for the same idempotency key.
        if (idempotencyKey != null)
        {
            var previous = await _db.HospitalPaymentOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.HospitalId == hospitalId &&
                    x.IdempotencyKey == idempotencyKey);

            if (previous != null)
            {
                return ReuseExistingOrder(
                    hospitalId,
                    bill,
                    amount,
                    previous);
            }
        }

        var merchant = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.HospitalId == hospitalId);

        if (merchant == null ||
            !merchant.IsEnabled ||
            merchant.KycStatus != "Approved" ||
            string.IsNullOrWhiteSpace(
                merchant.ProviderMerchantAccountId))
        {
            return new ConflictObjectResult(new
            {
                message =
                    "Hospital Razorpay merchant is not approved or enabled."
            });
        }

        if (merchant.Mode is not ("Test" or "Live"))
        {
            return new ConflictObjectResult(new
            {
                message = "Invalid hospital Razorpay mode."
            });
        }

        HospitalPaymentOrder order;
        decimal charge;
        decimal available;

        // Reserve the invoice amount before contacting Razorpay.
        await using (var reservation =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable))
        {
            // Recheck the idempotency key inside the transaction.
            if (idempotencyKey != null)
            {
                var previous = await _db.HospitalPaymentOrders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.HospitalId == hospitalId &&
                        x.IdempotencyKey == idempotencyKey);

                if (previous != null)
                {
                    return ReuseExistingOrder(
                        hospitalId,
                        bill,
                        amount,
                        previous);
                }
            }

            var paid = await _db.BillingPaymentTransactions
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == hospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "Paid")
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var refunded = await _db.HospitalPaymentRefunds
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == hospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "processed")
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            var alreadyPaid = Math.Max(
                0m,
                paid - refunded);

            var now = DateTime.UtcNow;

            var reserved = await _db.HospitalPaymentOrders
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == hospitalId &&
                    x.BillingId == bill.Id &&
                    (x.Status == "Pending" ||
                     x.Status == "Creating") &&
                    (!x.ExpiresAt.HasValue ||
                     x.ExpiresAt > now))
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;

            available = Math.Round(
                bill.TotalAmount - alreadyPaid - reserved,
                2,
                MidpointRounding.AwayFromZero);

            if (available <= 0)
            {
                return new ConflictObjectResult(new
                {
                    message =
                        "Invoice has no unreserved outstanding balance.",
                    outstandingAmount = Math.Max(
                        0m,
                        bill.TotalAmount - alreadyPaid),
                    reservedAmount = reserved
                });
            }

            charge = amount ?? available;

            if (charge <= 0 ||
                charge > available ||
                decimal.Round(charge, 2) != charge)
            {
                return new BadRequestObjectResult(new
                {
                    message =
                        "Payment amount must be positive and " +
                        "cannot exceed the available balance.",
                    availableAmount = available
                });
            }

            order = new HospitalPaymentOrder
            {
                HospitalId = hospitalId,
                BillingId = bill.Id,
                PatientId = bill.PatientId,
                BranchId = bill.BranchId,
                Amount = charge,
                Currency = "INR",
                Mode = merchant.Mode,
                IdempotencyKey = idempotencyKey,
                Status = "Creating",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };

            _db.HospitalPaymentOrders.Add(order);

            await _db.SaveChangesAsync();
            await reservation.CommitAsync();
        }

        try
        {
            // Pin the selected hospital merchant credential
            // to the newly created Razorpay order.
            var created =
                await _gateway.CreateOrderWithCredentialAsync(
                    hospitalId,
                    merchant.Mode,
                    charge,
                    order.LocalOrderId[..32]);

            order.GatewayOrderId =
                created.GatewayOrderId;

            order.RazorpayCredentialId =
                created.RazorpayCredentialId;

            order.Status = "Pending";
            order.ExpiresAt =
                DateTime.UtcNow.AddHours(24);

            await _db.SaveChangesAsync();

            var publicKey = _gateway.PublicKey(
                hospitalId,
                merchant.Mode,
                order.RazorpayCredentialId);

            _logger.LogInformation(
                "Hospital payment order created. " +
                "HospitalId={HospitalId}, " +
                "BillingId={BillingId}, " +
                "LocalOrderId={LocalOrderId}",
                hospitalId,
                bill.Id,
                order.LocalOrderId);

            return new OkObjectResult(new
            {
                order.LocalOrderId,
                gatewayOrderId = order.GatewayOrderId,
                amountPaise = checked(
                    (long)(charge * 100m)),
                currency = "INR",
                publicKey,
                status = order.Status,
                outstandingAmount = available,
                mode = merchant.Mode,
                reused = false
            });
        }
        catch (Exception ex)
        {
            // The remote Razorpay order might have been created
            // even if the local response or database update failed.
            // Do not automatically create another order.
            _logger.LogError(
                ex,
                "Hospital Razorpay order creation outcome uncertain. " +
                "HospitalId={HospitalId}, " +
                "BillingId={BillingId}, " +
                "LocalOrderId={LocalOrderId}",
                hospitalId,
                bill.Id,
                order.LocalOrderId);

            // Avoid overwriting a successful state written
            // concurrently by another request.
            try
            {
                await _db.HospitalPaymentOrders
                    .Where(x =>
                        x.Id == order.Id &&
                        x.Status == "Creating")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            x => x.Status,
                            "Failed"));
            }
            catch (Exception saveEx)
            {
                _logger.LogError(
                    saveEx,
                    "Unable to update order state for {OrderId}",
                    order.Id);
            }

            return new ObjectResult(new
            {
                message =
                    "Payment order creation could not be confirmed. " +
                    "Check the payment status before retrying."
            })
            {
                StatusCode = 502
            };
        }
    }

    /// <summary>
    /// Reuses an existing pending Razorpay order only
    /// when hospital, invoice and amount match.
    /// </summary>
    private IActionResult ReuseExistingOrder(
        int hospitalId,
        Billing bill,
        decimal? amount,
        HospitalPaymentOrder previous)
    {
        if (previous.BillingId != bill.Id ||
            (amount.HasValue &&
             previous.Amount != amount.Value))
        {
            return new ConflictObjectResult(new
            {
                message =
                    "This idempotency key belongs to a " +
                    "different invoice or amount."
            });
        }

        if (previous.Status != "Pending" ||
            string.IsNullOrWhiteSpace(
                previous.GatewayOrderId) ||
            (previous.ExpiresAt.HasValue &&
             previous.ExpiresAt <= DateTime.UtcNow))
        {
            return new ConflictObjectResult(new
            {
                message =
                    "This payment attempt is no longer pending. " +
                    "Start a new attempt."
            });
        }

        try
        {
            var publicKey = _gateway.PublicKey(
                hospitalId,
                previous.Mode,
                previous.RazorpayCredentialId);

            return new OkObjectResult(new
            {
                previous.LocalOrderId,
                gatewayOrderId =
                    previous.GatewayOrderId,
                amountPaise = checked(
                    (long)(previous.Amount * 100m)),
                currency = previous.Currency,
                publicKey,
                status = previous.Status,
                reused = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to retrieve pinned credentials " +
                "for payment order {OrderId}",
                previous.Id);

            return new ObjectResult(new
            {
                message =
                    "Payment credentials are unavailable."
            })
            {
                StatusCode = 502
            };
        }
    }
}
