
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Authorization;
using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/hospital-payments/recovery")]
[Authorize(Roles = "Admin")]
[RequirePermission("Billing")]
public sealed class HospitalPaymentRecoveryMonitoringController
    : ControllerBase
{
    private readonly AppDbContext _db;

    public HospitalPaymentRecoveryMonitoringController(
        AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(
            User.FindFirst("HospitalId")?.Value,
            out var hospitalId) || hospitalId <= 0)
        {
            return Forbid();
        }

        var attempts = _db.HospitalPaymentRecoveryAttempts
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId);

        var summary = await attempts
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        var retryReady = await attempts
            .CountAsync(x =>
                x.Status == "Failed" &&
                x.NextRetryAtUtc != null &&
                x.NextRetryAtUtc <= now,
                cancellationToken);

        var manualReview = await attempts
            .Where(x => x.Status == "ManualReview")
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.PaymentSource,
                x.AttemptNumber,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            hospitalId,
            generatedAtUtc = now,
            totalAttempts = await attempts.CountAsync(
                cancellationToken),
            byStatus = summary,
            retryReady,
            manualReview,
            note = "Recovery attempt monitoring only. " +
                   "No payments were changed."
        });
    }
}
