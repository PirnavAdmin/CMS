
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public sealed class CreateHospitalPaymentLinkDto
{
    public int BillingId { get; set; }
}

[ApiController]
[Route("api/hospital-payments/links")]
[Authorize(Roles = "Receptionist,Admin,Pharmacist")]
[RequirePermission("Billing")]
public sealed class HospitalPaymentLinksController : ControllerBase
{
    private readonly AppDbContext _db;

    public HospitalPaymentLinksController(AppDbContext db)
    {
        _db = db;
    }

    private int HospitalId =>
        int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id) ? id : 0;

    private int? BranchId =>
        int.TryParse(
            User.FindFirstValue("BranchId"),
            out var id) ? id : null;

    private bool CanAccessBill(Billing bill)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (!BranchId.HasValue ||
            bill.BranchId != BranchId.Value)
            return false;

        if (User.IsInRole("Pharmacist"))
        {
            return string.Equals(
                bill.BillingType,
                "Pharmacy",
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToHexString(bytes)
            .ToLowerInvariant();
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes)
            .ToLowerInvariant();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHospitalPaymentLinkDto dto,
        CancellationToken cancellationToken)
    {
        if (HospitalId <= 0)
            return Forbid();

        if (dto.BillingId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid BillingId is required."
            });
        }

        // Find invoice within logged-in hospital
        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == dto.BillingId &&
                     x.HospitalId == HospitalId,
                cancellationToken);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Invoice not found."
            });
        }

        if (!CanAccessBill(bill))
            return Forbid();

        if (bill.TotalAmount <= 0)
        {
            return BadRequest(new
            {
                message = "Invoice amount must be positive."
            });
        }

        // Generate secure token
        var token = GenerateToken();
        var tokenHash = HashToken(token);

        var now = DateTime.UtcNow;
        var expiry = now.AddHours(24);

        int? userId = int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var parsedId)
                ? parsedId
                : null;

        // Save only token hash
        var paymentLink = new HospitalPaymentLink
        {
            HospitalId = HospitalId,
            BillingId = bill.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiry,
            CreatedByUserId = userId
        };

        _db.HospitalPaymentLinks.Add(paymentLink);

        await _db.SaveChangesAsync(cancellationToken);

        // Return token only at creation time
        return Ok(new
        {
            message = "Payment link token generated.",
            paymentLinkId = paymentLink.Id,
            token,
            billingId = bill.Id,
            expiresAtUtc = expiry
        });
    }

    [HttpPost("{id:long}/revoke")]
    public async Task<IActionResult> Revoke(
        long id,
        CancellationToken cancellationToken)
    {
        if (HospitalId <= 0)
            return Forbid();

        var paymentLink = await _db.HospitalPaymentLinks
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     x.HospitalId == HospitalId,
                cancellationToken);

        if (paymentLink == null)
        {
            return NotFound(new
            {
                message = "Payment link not found."
            });
        }

        var bill = await _db.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == paymentLink.BillingId &&
                     x.HospitalId == HospitalId,
                cancellationToken);

        if (bill == null)
            return NotFound();

        if (!CanAccessBill(bill))
            return Forbid();

        if (paymentLink.RevokedAtUtc != null)
        {
            return Ok(new
            {
                message = "Payment link already revoked."
            });
        }

        paymentLink.RevokedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Payment link revoked successfully.",
            paymentLinkId = paymentLink.Id
        });
    }
}
