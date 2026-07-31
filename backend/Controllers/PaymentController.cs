using System.Security.Claims;
using AuthDemo.DTOs;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AuthDemo.Controllers;


[ApiController]
[Route("api/payment")]
[Authorize]
public class PaymentController : ControllerBase
{

    private readonly IPaymentService _paymentService;



    public PaymentController(
        IPaymentService paymentService)
    {
        _paymentService = paymentService;
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