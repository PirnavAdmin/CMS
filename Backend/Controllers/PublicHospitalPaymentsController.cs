
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/public/hospital-payments")]
[AllowAnonymous]
public sealed class PublicHospitalPaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicHospitalPaymentsController(AppDbContext db)
    {
        _db = db;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes)
            .ToLowerInvariant();
    }

    private async Task<IActionResult> GetPaymentDetails(
        string token,
        CancellationToken cancellationToken)
    {
        // Tokens are generated as 64-character hexadecimal strings.
        // Reject malformed tokens before database lookup.
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length != 64 ||
            !token.All(Uri.IsHexDigit))
        {
            return NotFound(new
            {
                message = "Payment link not found."
            });
        }

        var tokenHash = HashToken(token.ToLowerInvariant());
        var now = DateTime.UtcNow;

        // Resolve the payment link using its hash.
        // Never trust a hospital ID supplied by the customer.
        var link = await _db.HospitalPaymentLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (link == null ||
            link.RevokedAtUtc != null ||
            link.ExpiresAtUtc <= now)
        {
            return NotFound(new
            {
                message = "Payment link is invalid or expired."
            });
        }

        // Load only the invoice belonging to this link's hospital.
        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == link.BillingId &&
                     x.HospitalId == link.HospitalId,
                cancellationToken);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Invoice not found."
            });
        }

        // Same net-payment approach as existing
        // HospitalPaymentsController:
        // paid transactions minus processed refunds.
        var paidTransactions =
            await _db.BillingPaymentTransactions
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == link.HospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "Paid")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var processedRefunds =
            await _db.HospitalPaymentRefunds
                .AsNoTracking()
                .Where(x =>
                    x.HospitalId == link.HospitalId &&
                    x.BillingId == bill.Id &&
                    x.Status == "processed")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var paidAmount = Math.Max(
            0m,
            paidTransactions - processedRefunds);

        var remainingAmount = Math.Max(
            0m,
            bill.TotalAmount - paidAmount);

        var paymentStatus =
            remainingAmount <= 0m
                ? "Paid"
                : paidAmount > 0m
                    ? "PartiallyPaid"
                    : "Pending";

        return Ok(new
        {
            invoiceNumber = bill.InvoiceNumber,
            billingType = bill.BillingType,
            currency = "INR",
            totalAmount = bill.TotalAmount,
            paidAmount,
            remainingAmount,
            paymentStatus,
            canPay = remainingAmount > 0m,
            expiresAtUtc = link.ExpiresAtUtc,
            checkedAtUtc = DateTime.UtcNow
        });
    }

    // Customer opens payment link.
    [HttpGet("{token}")]
    public Task<IActionResult> GetInvoice(
        string token,
        CancellationToken cancellationToken)
    {
        return GetPaymentDetails(token, cancellationToken);
    }

    // Customer page polls this endpoint
    // to refresh payment status.
    [HttpGet("{token}/status")]
    public Task<IActionResult> GetStatus(
        string token,
        CancellationToken cancellationToken)
    {
        return GetPaymentDetails(token, cancellationToken);
    }
}
