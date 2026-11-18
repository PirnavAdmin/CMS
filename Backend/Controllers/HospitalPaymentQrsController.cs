
using System;
using System.Data;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/hospital-payments/qrs")]
[Authorize(Roles = "Receptionist,Admin,Pharmacist")]
[RequirePermission("Billing")]
public sealed class HospitalPaymentQrsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;
    private readonly ILogger<HospitalPaymentQrsController> _logger;

    public HospitalPaymentQrsController(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway,
        ILogger<HospitalPaymentQrsController> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public sealed class CreateQrRequest
    {
        public int BillingId { get; set; }
        public decimal Amount { get; set; }
    }

    private int HospitalId =>
        int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id)
            ? id : 0;

    private int? BranchId =>
        int.TryParse(
            User.FindFirstValue("BranchId"),
            out var id)
            ? id : null;

    private int? UserId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id : null;

    private bool CanAccess(Billing bill) =>
        (User.IsInRole("Admin") ||
         (BranchId.HasValue &&
          BranchId == bill.BranchId))
        &&
        (!User.IsInRole("Pharmacist") ||
         string.Equals(
             bill.BillingType,
             "Pharmacy",
             StringComparison.OrdinalIgnoreCase));

    // =====================================================
    // POST - CREATE HOSPITAL PAYMENT QR
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateQrRequest request,
        CancellationToken ct)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (request.BillingId <= 0 ||
            request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            return BadRequest(new
            {
                message =
                    "Provide a valid invoice and positive " +
                    "amount with at most two decimals."
            });
        }

        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.BillingId &&
                     x.HospitalId == HospitalId,
                ct);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Invoice not found."
            });
        }

        if (!CanAccess(bill))
            return Forbid();

        // Load the merchant registered for this hospital.
        var merchant = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.HospitalId == HospitalId,
                ct);

        if (merchant == null ||
            !merchant.IsEnabled ||
            merchant.KycStatus != "Approved" ||
            string.IsNullOrWhiteSpace(
                merchant.ProviderMerchantAccountId) ||
            merchant.Mode is not ("Test" or "Live"))
        {
            return Conflict(new
            {
                message =
                    "Hospital Razorpay merchant is not " +
                    "approved and enabled."
            });
        }

        if (string.IsNullOrWhiteSpace(merchant.MerchantName))
        {
            return Conflict(new
            {
                message =
                    "Hospital merchant name is not configured."
            });
        }

        // Reserve the requested invoice balance.
        // A pending QR is NOT a successful payment.
        HospitalPaymentQr qr;

        await using (
            var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct))
        {
            var paid = await _db.BillingPaymentTransactions
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == HospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "Paid")
                .SumAsync(
                    x => (decimal?)x.Amount, ct) ?? 0m;

            var refunds = await _db.HospitalPaymentRefunds
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == HospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "processed")
                .SumAsync(
                    x => (decimal?)x.Amount, ct) ?? 0m;

            var reservedOrders = await _db.HospitalPaymentOrders
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == HospitalId &&
                    x.BillingId == bill.Id &&
                    (x.Status == "Pending" ||
                     x.Status == "Creating") &&
                    (!x.ExpiresAt.HasValue ||
                     x.ExpiresAt > DateTime.UtcNow))
                .SumAsync(
                    x => (decimal?)x.Amount, ct) ?? 0m;

            var reservedQrs = await _db.HospitalPaymentQrs
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == HospitalId &&
                    x.BillingId == bill.Id &&
                    (x.Status == "Active" ||
                     x.Status == "Creating") &&
                    x.ExpiresAtUtc > DateTime.UtcNow)
                .SumAsync(
                    x => (decimal?)x.Amount, ct) ?? 0m;

            var available = decimal.Round(
                bill.TotalAmount -
                Math.Max(0m, paid - refunds) -
                reservedOrders -
                reservedQrs,
                2);

            if (request.Amount > available)
            {
                return Conflict(new
                {
                    message =
                        "Amount exceeds the unreserved " +
                        "invoice balance.",
                    availableAmount =
                        Math.Max(0m, available)
                });
            }

            qr = new HospitalPaymentQr
            {
                HospitalId = HospitalId,
                BillingId = bill.Id,
                BranchId = bill.BranchId,
                Amount = request.Amount,
                Mode = merchant.Mode,
                Status = "Creating",
                ExpiresAtUtc =
                    DateTime.UtcNow.AddMinutes(15),
                CreatedByUserId = UserId
            };

            _db.HospitalPaymentQrs.Add(qr);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        try
        {
            var gatewayQr =
                await _gateway.CreateFixedAmountQrAsync(
                    HospitalId,
                    qr.Mode,
                    qr.Amount,
                    qr.LocalReference,
                    qr.ExpiresAtUtc);

            qr.GatewayQrId = gatewayQr.QrId;
            qr.ImageUrl = gatewayQr.ImageUrl;
            qr.RazorpayCredentialId =
                gatewayQr.CredentialId;

            qr.Status = gatewayQr.Status.Equals(
                "closed",
                StringComparison.OrdinalIgnoreCase)
                ? "Closed"
                : "Active";

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                qr.Id,
                qr.LocalReference,
                qr.BillingId,
                qr.Amount,
                qr.Currency,
                qr.GatewayQrId,
                qr.ImageUrl,
                qr.Status,
                qr.ExpiresAtUtc,

                // NEW: Merchant information for frontend
                merchantDetails = new
                {
                    merchantName = merchant.MerchantName,
                    merchantAccountId =
                        merchant.ProviderMerchantAccountId,
                    provider = merchant.Provider,
                    mode = qr.Mode,
                    hospitalId = HospitalId
                },

                paymentDetails = new
                {
                    invoiceId = bill.Id,
                    billingType = bill.BillingType,
                    amount = qr.Amount,
                    currency = qr.Currency,
                    purpose = "Hospital invoice payment"
                },

                message =
                    "Display merchant details to the " +
                    "payer before payment confirmation."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "QR creation failed for hospital {HospitalId}, " +
                "local QR {QrId}",
                HospitalId,
                qr.Id);

            // If the gateway created a QR but saving failed,
            // do not blindly issue another payment request.
            if (string.IsNullOrWhiteSpace(
                qr.GatewayQrId))
            {
                try
                {
                    qr.Status = "Failed";

                    await _db.SaveChangesAsync(
                        CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(
                        saveEx,
                        "Unable to mark QR {QrId} as failed",
                        qr.Id);
                }
            }

            return StatusCode(502, new
            {
                message =
                    "QR could not be confirmed. " +
                    "Check payment attempts before retrying."
            });
        }
    }

    // =====================================================
    // GET - VIEW HOSPITAL QR AND MERCHANT DETAILS
    // =====================================================

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(
        long id,
        CancellationToken ct)
    {
        if (HospitalId <= 0)
            return Forbid();

        var qr = await _db.HospitalPaymentQrs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     x.HospitalId == HospitalId,
                ct);

        if (qr == null)
            return NotFound();

        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == qr.BillingId &&
                     x.HospitalId == HospitalId,
                ct);

        if (bill == null || !CanAccess(bill))
            return Forbid();

        var merchant = await _db.HospitalMerchantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.HospitalId == HospitalId,
                ct);

        return Ok(new
        {
            qr.Id,
            qr.LocalReference,
            qr.BillingId,
            qr.Amount,
            qr.Currency,
            qr.GatewayQrId,
            qr.ImageUrl,
            qr.Status,
            qr.ExpiresAtUtc,
            qr.PaidAtUtc,

            merchantDetails = merchant == null
                ? null
                : new
                {
                    merchantName = merchant.MerchantName,
                    merchantAccountId =
                        merchant.ProviderMerchantAccountId,
                    provider = merchant.Provider,
                    mode = qr.Mode,
                    hospitalId = HospitalId
                },

            paymentDetails = new
            {
                invoiceId = bill.Id,
                billingType = bill.BillingType,
                amount = qr.Amount,
                currency = qr.Currency,
                purpose = "Hospital invoice payment"
            }
        });
    }
}
