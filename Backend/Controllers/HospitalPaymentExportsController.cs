
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers
{
    [ApiController]
    [Route("api/hospital-payments/reports")]
    [Authorize(Roles = "Admin")]
    public sealed class HospitalPaymentExportsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public HospitalPaymentExportsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            // 1. Identify logged-in hospital
            var hospitalClaim =
                User.FindFirstValue("HospitalId");

            if (!int.TryParse(
                    hospitalClaim,
                    out var hospitalId) ||
                hospitalId <= 0)
            {
                return Forbid();
            }

            // 2. Set default date range
            // Default: last 30 days
            // Custom: any historical period
            var now = DateTime.UtcNow;

            var start = fromUtc ?? now.AddDays(-30);
            var end = toUtc ?? now;

            // 3. Validate date range
            if (start > end)
            {
                return BadRequest(new
                {
                    message =
                        "Start date cannot be after end date."
                });
            }

            if (start > now.AddMinutes(5) ||
                end > now.AddMinutes(5))
            {
                return BadRequest(new
                {
                    message =
                        "Future dates are not allowed."
                });
            }

            // 4. Fetch hospital-specific transactions
            // Limit: 50,000 records per export
            var transactions =
                await _db.BillingPaymentTransactions
                    .AsNoTracking()
                    .Where(t =>
                        t.HospitalId == hospitalId &&
                        t.CreatedAt >= start &&
                        t.CreatedAt <= end)
                    .OrderBy(t => t.CreatedAt)
                    .ThenBy(t => t.Id)
                    .Take(50001)
                    .Select(t => new
                    {
                        t.Id,
                        t.BillingId,
                        InvoiceNumber =
                            t.Billing.InvoiceNumber,
                        t.CreatedAt,
                        t.PaidAt,
                        t.Amount,
                        t.PaymentMethod,
                        t.ReferenceNumber,
                        t.Status
                    })
                    .ToListAsync(cancellationToken);

            // 5. Prevent excessive exports
            if (transactions.Count > 50000)
            {
                return StatusCode(413, new
                {
                    message =
                        "More than 50,000 payment records found. " +
                        "Please select a smaller date range."
                });
            }

            // 6. CSV formatting helper
            // Protects against CSV formula injection
            static string CsvCell(object? value)
            {
                var raw = Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture) ?? "";

                if (raw.Length > 0 &&
                    (raw[0] is '=' or '+' or '-' or '@' ||
                     raw[0] == '\t' ||
                     raw[0] == '\r' ||
                     raw[0] == '\n'))
                {
                    raw = "'" + raw;
                }

                return "\"" +
                       raw.Replace("\"", "\"\"") +
                       "\"";
            }

            // 7. Generate CSV
            var csv = new StringBuilder();

            csv.AppendLine(
                "TransactionId,BillingId,InvoiceNumber," +
                "CreatedAtUtc,PaidAtUtc,AmountINR," +
                "PaymentMethod,ReferenceNumber,Status");

            foreach (var t in transactions)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    CsvCell(t.Id),
                    CsvCell(t.BillingId),
                    CsvCell(t.InvoiceNumber),
                    CsvCell(t.CreatedAt.ToString("O")),
                    CsvCell(t.PaidAt?.ToString("O")),
                    CsvCell(t.Amount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)),
                    CsvCell(t.PaymentMethod),
                    CsvCell(t.ReferenceNumber),
                    CsvCell(t.Status)
                }));
            }

            // 8. UTF-8 BOM for Excel compatibility
            var bytes = new UTF8Encoding(true)
                .GetPreamble()
                .Concat(
                    Encoding.UTF8.GetBytes(
                        csv.ToString()))
                .ToArray();

            // 9. Return downloadable CSV
            var fileName =
                $"hospital-{hospitalId}-payments-" +
                $"{now:yyyyMMdd-HHmmss}.csv";

            return File(
                bytes,
                "text/csv; charset=utf-8",
                fileName);
        }
    }
}
