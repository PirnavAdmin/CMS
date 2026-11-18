using System.Security.Claims;
using AuthDemo.DTOs;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace AuthDemo.Controllers;


[ApiController]
[Route("api/payment")]
[Authorize]
public class PaymentController : ControllerBase
{

    private readonly IPaymentService _paymentService;
    private readonly AuthDemo.Data.AppDbContext _db;

    public PaymentController(IPaymentService paymentService, AuthDemo.Data.AppDbContext db)
    {
        _paymentService = paymentService;
        _db = db;
    }
    // =====================================================
    // GET HOSPITAL ID FROM JWT
    // =====================================================

    private int GetHospitalId()
    {
        var claim = User.Claims.FirstOrDefault(x => x.Type == "HospitalId");

        if (claim == null)
        {
            throw new Exception("HospitalId not found in token.");
        }

        return int.Parse(claim.Value);
    }

    // =====================================================
    // CREATE PAYMENT
    // =====================================================

    [HttpPost("create")]
    public async Task<IActionResult> CreatePayment(CreatePaymentDto dto)
    {
        try
        {
            int hospitalId = GetHospitalId();
            if (!string.Equals(dto.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Online appointment payments must first be generated as a Billing invoice and paid through /api/hospital-payments/orders. This legacy endpoint supports Cash only." });

            var paymentId = await _paymentService.CreatePaymentAsync(
                dto,
                hospitalId);

            return Ok(new
            {
                paymentId,
                paymentStatus = "Pending",
                message = "Please complete the consultation fee payment to confirm your appointment."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }




    // =====================================================
    // PAYMENT SUCCESS
    // =====================================================

    [HttpPost("success")]
    public async Task<IActionResult> PaymentSuccess(
        PaymentSuccessDto dto)
    {
        try
        {
            int hospitalId = GetHospitalId();
            var pendingPayment = await _db.Payments.FirstOrDefaultAsync(x =>
                x.Id == dto.PaymentId && x.HospitalId == hospitalId);
            if (pendingPayment == null)
                return NotFound(new { message = "Payment not found." });

            // This legacy endpoint has no gateway signature verification. It is
            // retained for cash confirmation only; online payments must use the
            // hospital-scoped Razorpay order/verify/webhook endpoints.
            if (!string.Equals(pendingPayment.PaymentMode, "Cash", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = "Client-submitted success is not accepted for online payments. Create and verify a hospital payment order instead." });

            await _paymentService.CompletePaymentAsync(
                dto,
                hospitalId);

            return Ok(new
            {
                message = "Payment completed successfully.",
                paymentStatus = "Paid",
                appointmentStatus = "Waiting"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}