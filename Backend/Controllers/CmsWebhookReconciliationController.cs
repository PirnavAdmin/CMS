
using System;
using System.Threading.Tasks;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Controllers;

// =====================================================
// PAYMENTS - CMS WEBHOOK RECONCILIATION API
// =====================================================

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/cms/webhook-reconciliation")]
public sealed class CmsWebhookReconciliationController
    : ControllerBase
{
    private readonly CmsRazorpayReconciliationService _service;
    private readonly ILogger<CmsWebhookReconciliationController> _logger;

    public CmsWebhookReconciliationController(
        CmsRazorpayReconciliationService service,
        ILogger<CmsWebhookReconciliationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // =================================================
    // PAYMENTS - MANUALLY RETRY A CMS WEBHOOK EVENT
    // =================================================

    [HttpPost("{eventId:long}/process")]
    public async Task<IActionResult> Process(long eventId)
    {
        if (eventId <= 0)
        {
            return BadRequest(new
            {
                eventId,
                status = "InvalidEventId"
            });
        }

        try
        {
            var result = await _service.ProcessAsync(eventId);

            switch (result)
            {
                case "Processed":
                case "AlreadyProcessed":
                    return Ok(new
                    {
                        eventId,
                        status = result
                    });

                case "EventNotFound":
                    return NotFound(new
                    {
                        eventId,
                        status = result
                    });

                case "InvalidPayload":
                case "InvalidPaymentIdentity":
                case "InvalidMode":
                case "UnsupportedEvent":
                    return UnprocessableEntity(new
                    {
                        eventId,
                        status = result
                    });

                case "PaymentNotCapturedOrMismatch":
                    return Conflict(new
                    {
                        eventId,
                        status = result,
                        message = "Razorpay payment verification did not succeed."
                    });

                case "PurchaseNotFound":
                case "GatewayModeMismatch":
                case "PurchaseChanged":
                case "DifferentPaymentAlreadyRecorded":
                case "ActivatedPurchaseConflict":
                    return Conflict(new
                    {
                        eventId,
                        status = result
                    });

                default:
                    _logger.LogWarning(
                        "Unexpected CMS webhook reconciliation result. EventId={EventId}, Status={Status}",
                        eventId,
                        result);

                    return Conflict(new
                    {
                        eventId,
                        status = result
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CMS webhook reconciliation failed for EventId={EventId}",
                eventId);

            return StatusCode(503, new
            {
                eventId,
                status = "ReconciliationUnavailable",
                message = "Webhook reconciliation could not be completed. Retry later."
            });
        }
    }
}
