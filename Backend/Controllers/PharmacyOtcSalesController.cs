
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Controllers;

[ApiController]
[Authorize(Roles = "Pharmacist")]
[Route("api/pharmacy/otc-sales")]
[Produces("application/json")]
public sealed class PharmacyOtcSalesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyOtcSalesService _service;
    private readonly ILogger<PharmacyOtcSalesController> _logger;

    public PharmacyOtcSalesController(
        AppDbContext db,
        PharmacyOtcSalesService service,
        ILogger<PharmacyOtcSalesController> logger)
    {
        _db = db;
        _service = service;
        _logger = logger;
    }

    private async Task<User?> GetPharmacistAsync(
        CancellationToken ct)
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("userId")?.Value;

        if (!int.TryParse(userIdClaim, out int userId))
        {
            _logger.LogWarning(
                "OTC request rejected: valid pharmacist user ID claim missing.");

            return null;
        }

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Id == userId &&
                     u.Role == "Pharmacist" &&
                     u.IsActive,
                ct);
    }

    // POST: api/pharmacy/otc-sales/draft
    [HttpPost("draft")]
    [ProducesResponseType(
        typeof(PharmacyOtcSaleResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreatePharmacyOtcSaleDto request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "OTC draft endpoint reached. AppointmentId: {AppointmentId}",
            request?.AppointmentId);

        var pharmacist = await GetPharmacistAsync(ct);

        if (pharmacist == null)
        {
            return Unauthorized(new
            {
                message = "Valid active pharmacist authentication is required."
            });
        }

        if (request == null)
        {
            return BadRequest(new
            {
                message = "OTC sale request is required."
            });
        }

        if (request.AppointmentId <= 0 ||
            request.Items == null ||
            request.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "Valid appointment and at least one medicine are required."
            });
        }

        try
        {
            var result = await _service.CreateDraftAsync(
                request,
                pharmacist,
                ct);

            _logger.LogInformation(
                "OTC draft created. SaleId: {SaleId}, AppointmentId: {AppointmentId}",
                result.Id,
                request.AppointmentId);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid OTC draft request for appointment {AppointmentId}",
                request.AppointmentId);

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "OTC draft rejected for appointment {AppointmentId}",
                request.AppointmentId);

            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    // GET: api/pharmacy/otc-sales/1
    [HttpGet("{id:int}")]
    [ProducesResponseType(
        typeof(PharmacyOtcSaleResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        int id,
        CancellationToken ct)
    {
        var pharmacist = await GetPharmacistAsync(ct);

        if (pharmacist == null)
        {
            return Unauthorized(new
            {
                message = "Valid active pharmacist authentication is required."
            });
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid OTC sale ID is required."
            });
        }

        var result = await _service.GetAsync(
            id,
            pharmacist,
            ct);

        if (result == null)
        {
            return NotFound(new
            {
                message = "OTC sale not found in your assigned branch."
            });
        }

        return Ok(result);
    }

    // POST: api/pharmacy/otc-sales/1/confirm
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(
        typeof(PharmacyOtcSaleResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        int id,
        CancellationToken ct)
    {
        var pharmacist = await GetPharmacistAsync(ct);

        if (pharmacist == null)
        {
            return Unauthorized(new
            {
                message = "Valid active pharmacist authentication is required."
            });
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid OTC sale ID is required."
            });
        }

        try
        {
            var result = await _service.ConfirmSaleAsync(
                id,
                pharmacist,
                ct);

            _logger.LogInformation(
                "OTC sale confirmed. SaleId: {SaleId}, AppointmentId: {AppointmentId}",
                result.Id,
                result.AppointmentId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }
}
