using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/cms/payments/reports")]
[Authorize(Roles = "SuperAdmin")]
public sealed class CmsPaymentRevenueReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CmsSubscriptionRazorpayService _gateway;

    public CmsPaymentRevenueReportsController(
        AppDbContext db,
        CmsSubscriptionRazorpayService gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    // CMS settlement request DTOs
    // These are specific to this controller.
    // Payment DTOs such as ContestPaymentDisputeDto remain in PaymentDto.cs.

    public sealed class CmsSettlementImportDto
    {
        public List<CmsSettlementRowDto> Rows { get; set; } = new();
    }

    public sealed class CmsSettlementRowDto
    {
        public long PurchaseId { get; set; }
        public string GatewayPaymentId { get; set; } = "";
        public string Mode { get; set; } = "Test";
        public string SettlementId { get; set; } = "";
        public DateTime SettledAt { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal FeeAmount { get; set; }
        public decimal TaxAmount { get; set; }
    }

    private void AddCmsPaymentAudit(string action, string detail)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
        {
            return;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? "SuperAdmin",
            Role = "SuperAdmin",
            Action = action,
            SystemAction = detail,
            IsLoginActivity = false,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Browser = Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow,
            LoginTime = DateTime.UtcNow
        });
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> SubscriptionRevenue(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string mode = "Live")
    {
        if (mode is not ("Test" or "Live" or "All"))
        {
            return BadRequest(new
            {
                message = "Mode must be Test, Live or All."
            });
        }

        var start = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var end = (to ?? DateTime.UtcNow.Date.AddDays(1)).Date.AddDays(1);

        if (end <= start || (end - start).TotalDays > 366)
        {
            return BadRequest(new
            {
                message = "Invalid date range; maximum is 366 days."
            });
        }

        var query = _db.SubscriptionPurchases
            .AsNoTracking()
            .Where(x =>
                x.IsGatewayPaymentVerified
                && x.PaymentStatus == "Paid"
                && x.PaidAt >= start
                && x.PaidAt < end
                && (mode == "All" || x.GatewayMode == mode));

        var gross = await query.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var count = await query.CountAsync();

        var byType = await query
            .GroupBy(x => x.PurchaseType)
            .Select(g => new
            {
                purchaseType = g.Key,
                count = g.Count(),
                amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        var refundQuery = _db.CmsSubscriptionRefunds
            .AsNoTracking()
            .Where(x =>
                x.CompletedAt >= start
                && x.CompletedAt < end
                && x.Status == "processed");

        if (mode != "All")
            refundQuery = refundQuery.Where(x => x.Mode == mode);

        var refunds = await refundQuery
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var settlementQuery = _db.CmsSubscriptionSettlementRecords
            .AsNoTracking()
            .Where(x => x.SettledAt >= start && x.SettledAt < end);

        if (mode != "All")
            settlementQuery = settlementQuery.Where(x => x.Mode == mode);

        var settlements =
            await settlementQuery.SumAsync(x => (decimal?)x.NetAmount) ?? 0m;

        var settlementGross =
            await settlementQuery.SumAsync(x => (decimal?)x.GrossAmount) ?? 0m;

        var settlementFees =
            await settlementQuery.SumAsync(x => (decimal?)x.FeeAmount) ?? 0m;

        var settlementTax =
            await settlementQuery.SumAsync(x => (decimal?)x.TaxAmount) ?? 0m;

        var pendingRefundQuery = _db.CmsSubscriptionRefunds
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt >= start
                && x.CreatedAt < end
                && (x.Status == "requested" || x.Status == "pending"));

        if (mode != "All")
            pendingRefundQuery = pendingRefundQuery.Where(x => x.Mode == mode);

        var pendingRefunds = await pendingRefundQuery.CountAsync();

        var disputeQuery = _db.CmsSubscriptionDisputes
            .AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end);

        if (mode != "All")
            disputeQuery = disputeQuery.Where(x => x.Mode == mode);

        var openDisputes = await disputeQuery.CountAsync(
            x => x.Status != "Won"
                && x.Status != "Lost"
                && x.Status != "Closed");

        var lostDisputeQuery = _db.CmsSubscriptionDisputes
            .AsNoTracking()
            .Where(x =>
                x.Status == "Lost"
                && x.ClosedAt >= start
                && x.ClosedAt < end
                && (mode == "All" || x.Mode == mode));

        var lostDisputeAmount =
            await lostDisputeQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var payoutQuery = _db.CmsGatewaySettlements
            .AsNoTracking()
            .Where(x =>
                x.SettledAt >= start
                && x.SettledAt < end
                && x.Status == "processed");

        if (mode != "All")
            payoutQuery = payoutQuery.Where(x => x.Mode == mode);

        var providerSettlementPayouts =
            await payoutQuery.SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var providerSettlementFees =
            await payoutQuery.SumAsync(x => (decimal?)x.FeeAmount) ?? 0m;

        var providerSettlementTax =
            await payoutQuery.SumAsync(x => (decimal?)x.TaxAmount) ?? 0m;

        return Ok(new
        {
            paymentDomain = "CMS_SUBSCRIPTIONS",
            mode,
            from = start,
            to = end.AddDays(-1),
            verifiedGrossCollections = gross,
            refunds,
            netCollections = gross - refunds - lostDisputeAmount,
            settledGrossAmount = settlementGross,
            providerFees = settlementFees,
            providerFeeTax = settlementTax,
            settledNetAmount = settlements,
            unsettledGatewayAmount = gross - settlementGross,
            providerSettlementPayouts,
            providerSettlementFees,
            providerSettlementTax,
            verifiedPurchaseCount = count,
            byPurchaseType = byType,
            pendingRefunds,
            openDisputes,
            lostDisputeAmount,
            note = "Hospital patient collections are intentionally excluded."
        });
    }

    [HttpPost("settlements/reconcile")]
    public async Task<IActionResult> ReconcileCmsSettlements(
        [FromBody] CmsSettlementImportDto dto)
    {
        if (dto.Rows == null || dto.Rows.Count == 0 || dto.Rows.Count > 1000)
        {
            return BadRequest(new
            {
                message = "Provide between 1 and 1000 settlement rows."
            });
        }

        var matched = 0;
        var exceptions = new List<object>();

        foreach (var row in dto.Rows)
        {
            var purchase = await _db.SubscriptionPurchases.FirstOrDefaultAsync(
                x => x.Id == row.PurchaseId
                    && x.GatewayPaymentId == row.GatewayPaymentId
                    && x.GatewayMode == row.Mode
                    && x.IsGatewayPaymentVerified);

            if (purchase == null)
            {
                exceptions.Add(new
                {
                    row.PurchaseId,
                    reason = "Verified CMS subscription payment not found."
                });

                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SettlementId)
                || row.SettledAt == default
                || row.GrossAmount != purchase.Amount
                || row.FeeAmount < 0
                || row.TaxAmount < 0
                || row.FeeAmount + row.TaxAmount > row.GrossAmount)
            {
                exceptions.Add(new
                {
                    row.PurchaseId,
                    reason = "Settlement ID/date or gross/fee/tax amounts are invalid."
                });

                continue;
            }

            var duplicate = await _db.CmsSubscriptionSettlementRecords.AnyAsync(
                x => x.SubscriptionPurchaseId == purchase.Id
                    && x.SettlementId == row.SettlementId
                    && x.GatewayPaymentId == row.GatewayPaymentId);

            if (!duplicate)
            {
                _db.CmsSubscriptionSettlementRecords.Add(
                    new CmsSubscriptionSettlementRecord
                    {
                        SubscriptionPurchaseId = purchase.Id,
                        GatewayPaymentId = row.GatewayPaymentId,
                        SettlementId = row.SettlementId,
                        Mode = row.Mode,
                        GrossAmount = row.GrossAmount,
                        FeeAmount = row.FeeAmount,
                        TaxAmount = row.TaxAmount,
                        NetAmount = row.GrossAmount
                            - row.FeeAmount
                            - row.TaxAmount,
                        SettledAt = row.SettledAt
                    });
            }

            matched++;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            matched,
            exceptionCount = exceptions.Count,
            exceptions
        });
    }

    [HttpPost("settlements/sync")]
    public async Task<IActionResult> SyncCmsSettlements(
        [FromQuery] string mode = "Test",
        [FromQuery] DateTime from = default,
        [FromQuery] DateTime to = default)
    {
        if (mode is not ("Test" or "Live"))
        {
            return BadRequest(new
            {
                message = "Mode must be Test or Live."
            });
        }

        if (from == default
            || to == default
            || to <= from
            || (to - from).TotalDays > 93)
        {
            return BadRequest(new
            {
                message = "Provide a valid UTC date range of no more than 93 days."
            });
        }

        var cmsMerchant = await _db.CmsOwnerMerchantSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1);

        if (cmsMerchant == null
            || cmsMerchant.KycStatus != "Approved"
            || string.IsNullOrWhiteSpace(cmsMerchant.ProviderMerchantAccountId))
        {
            return Conflict(new
            {
                message = "CMS Owner merchant onboarding is not approved."
            });
        }

        try
        {
            var settlements = await _gateway.FetchSettlementsAsync(
                mode,
                from,
                to);

            var inserted = 0;
            var updated = 0;

            foreach (var item in settlements)
            {
                var row = await _db.CmsGatewaySettlements.FirstOrDefaultAsync(
                    x => x.Mode == mode && x.SettlementId == item.Id);

                if (row == null)
                {
                    row = new CmsGatewaySettlement
                    {
                        Mode = mode,
                        SettlementId = item.Id
                    };

                    _db.CmsGatewaySettlements.Add(row);
                    inserted++;
                }
                else
                {
                    updated++;
                }

                row.Status = item.Status;
                row.Amount = item.Amount;
                row.FeeAmount = item.Fees;
                row.TaxAmount = item.Tax;
                row.Utr = item.Utr;
                row.SettledAt = item.CreatedAt;
                row.SyncedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                paymentDomain = "CMS_SUBSCRIPTIONS",
                mode,
                fetched = settlements.Count,
                inserted,
                updated,
                message = "CMS Owner settlement payout records synced."
            });
        }
        catch
        {
            return StatusCode(502, new
            {
                message = "Unable to sync CMS Owner settlements. Check CMS merchant credentials and API access."
            });
        }
    }

    // Swagger multipart/form-data upload configuration
    [HttpPost("disputes/{disputeId:long}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadCmsDisputeEvidence(
        [FromRoute] long disputeId,
        IFormFile file)
    {
        if (file == null || file.Length <= 0 || file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new
            {
                message = "Upload a file up to 10 MB."
            });
        }

        var allowed = new[]
        {
            "application/pdf",
            "image/jpg",
            "image/jpeg",
            "image/png"
        };

        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only PDF, JPG/JPEG and PNG evidence files are supported."
            });
        }

        var dispute = await _db.CmsSubscriptionDisputes
            .FirstOrDefaultAsync(x => x.Id == disputeId);

        if (dispute == null)
        {
            return NotFound(new
            {
                message = "CMS subscription dispute not found."
            });
        }

        if (dispute.Status is not ("Created" or "ActionRequired"))
        {
            return Conflict(new
            {
                message = "Evidence can only be uploaded while the dispute is open or action is required."
            });
        }

        try
        {
            using var stream = file.OpenReadStream();

            var documentId = await _gateway.UploadDisputeDocumentAsync(
                dispute.Mode,
                stream,
                file.FileName,
                file.ContentType);

            var ids = JsonSerializer.Deserialize<List<string>>(
                dispute.EvidenceDocumentIdsJson) ?? new List<string>();

            if (!ids.Contains(documentId, StringComparer.Ordinal))
                ids.Add(documentId);

            dispute.EvidenceDocumentIdsJson = JsonSerializer.Serialize(ids);
            dispute.UpdatedAt = DateTime.UtcNow;

            AddCmsPaymentAudit(
                "CMS Subscription Dispute Evidence Uploaded",
                $"Evidence document {documentId} uploaded for dispute {dispute.GatewayDisputeId}.");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                documentId,
                uploadedDocumentCount = ids.Count
            });
        }
        catch
        {
            return StatusCode(502, new
            {
                message = "Unable to upload CMS dispute evidence to Razorpay."
            });
        }
    }

    [HttpPost("disputes/{disputeId:long}/contest")]
    public async Task<IActionResult> ContestCmsDispute(
        [FromRoute] long disputeId,
        [FromBody] ContestPaymentDisputeDto dto)
    {
        var dispute = await _db.CmsSubscriptionDisputes
            .FirstOrDefaultAsync(x => x.Id == disputeId);

        if (dispute == null)
        {
            return NotFound(new
            {
                message = "CMS subscription dispute not found."
            });
        }

        if (dispute.Status is not ("Created" or "ActionRequired"))
        {
            return Conflict(new
            {
                message = "Only open disputes or disputes requiring action can be contested."
            });
        }

        var evidenceType = dto.EvidenceType?.Trim().ToLowerInvariant() ?? "";

        var evidenceTypes = new[]
        {
            "shipping_proof",
            "billing_proof",
            "cancellation_proof",
            "customer_communication",
            "proof_of_service",
            "explanation_letter",
            "refund_confirmation",
            "access_activity_log",
            "refund_cancellation_policy",
            "term_and_conditions"
        };

        if (!evidenceTypes.Contains(evidenceType, StringComparer.Ordinal))
        {
            return BadRequest(new
            {
                message = "Unsupported evidence type."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Summary) || dto.Summary.Length > 1000)
        {
            return BadRequest(new
            {
                message = "Summary is required and cannot exceed 1000 characters."
            });
        }

        var uploaded = JsonSerializer.Deserialize<List<string>>(
            dispute.EvidenceDocumentIdsJson) ?? new List<string>();

        var ids = dto.DocumentIds?.Distinct(StringComparer.Ordinal).ToList()
            ?? new List<string>();

        if (ids.Count == 0
            || ids.Any(x => !uploaded.Contains(x, StringComparer.Ordinal)))
        {
            return BadRequest(new
            {
                message = "Provide at least one document ID previously uploaded for this dispute."
            });
        }

        var amount = dto.Amount ?? dispute.Amount;

        if (amount <= 0
            || amount > dispute.Amount
            || decimal.Round(amount, 2) != amount)
        {
            return BadRequest(new
            {
                message = "Contested amount must be positive and cannot exceed the dispute amount."
            });
        }

        try
        {
            var result = await _gateway.ContestDisputeAsync(
                dispute.Mode,
                dispute.GatewayDisputeId,
                amount,
                dto.Summary.Trim(),
                evidenceType,
                ids);

            dispute.Status = result.Status.Equals(
                "under_review",
                StringComparison.OrdinalIgnoreCase)
                    ? "UnderReview"
                    : result.Status;

            dispute.PayloadJson = result.PayloadJson;
            dispute.UpdatedAt = DateTime.UtcNow;

            AddCmsPaymentAudit(
                "CMS Subscription Dispute Contested",
                $"Dispute {dispute.GatewayDisputeId} submitted for {amount:0.00} with {ids.Count} evidence document(s).");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                dispute.GatewayDisputeId,
                dispute.Status,
                submittedAmount = amount,
                documentIds = ids
            });
        }
        catch
        {
            return StatusCode(502, new
            {
                message = "Unable to submit CMS dispute evidence to Razorpay."
            });
        }
    }

    [HttpPost("disputes/{disputeId:long}/accept")]
    public async Task<IActionResult> AcceptCmsDispute(
        [FromRoute] long disputeId)
    {
        var dispute = await _db.CmsSubscriptionDisputes
            .FirstOrDefaultAsync(x => x.Id == disputeId);

        if (dispute == null)
        {
            return NotFound(new
            {
                message = "CMS subscription dispute not found."
            });
        }

        if (dispute.Status is not ("Created" or "ActionRequired"))
        {
            return Conflict(new
            {
                message = "Only open disputes can be accepted."
            });
        }

        try
        {
            var result = await _gateway.AcceptDisputeAsync(
                dispute.Mode,
                dispute.GatewayDisputeId);

            dispute.Status = "Lost";
            dispute.ClosedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;
            dispute.PayloadJson = result.PayloadJson;

            AddCmsPaymentAudit(
                "CMS Subscription Dispute Accepted",
                $"Dispute {dispute.GatewayDisputeId} accepted; provider status {result.Status}.");

            await _db.SaveChangesAsync();

            return Ok(new
            {
                dispute.Id,
                dispute.GatewayDisputeId,
                dispute.Status
            });
        }
        catch
        {
            return StatusCode(502, new
            {
                message = "Unable to accept CMS dispute at Razorpay."
            });
        }
    }

    [HttpGet("disputes")]
    public async Task<IActionResult> CmsDisputes(
        [FromQuery] string mode = "All",
        [FromQuery] string? status = null)
    {
        if (mode is not ("Test" or "Live" or "All"))
        {
            return BadRequest(new
            {
                message = "Mode must be Test, Live or All."
            });
        }

        var query = _db.CmsSubscriptionDisputes.AsNoTracking();

        if (mode != "All")
            query = query.Where(x => x.Mode == mode);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.SubscriptionPurchaseId,
                x.GatewayDisputeId,
                x.GatewayPaymentId,
                x.Amount,
                x.Currency,
                x.Reason,
                x.Status,
                x.EvidenceDueBy,
                x.EvidenceDocumentIdsJson,
                x.Mode,
                x.CreatedAt,
                x.UpdatedAt,
                x.ClosedAt
            })
            .ToListAsync();

        return Ok(new
        {
            paymentDomain = "CMS_SUBSCRIPTIONS",
            disputes = rows
        });
    }
}