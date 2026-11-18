
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Authorize(Roles = "Patient")]
[Route("api/patient/appointments")]
public sealed class PatientAppointmentPaymentsController
    : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HospitalRazorpayGatewayService _gateway;

    public PatientAppointmentPaymentsController(
        AppDbContext db,
        HospitalRazorpayGatewayService gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    private string? PatientEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? User.FindFirstValue("email");

    private async Task<Patient?> GetPatientAsync()
    {
        if (string.IsNullOrWhiteSpace(PatientEmail))
            return null;

        return await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email == PatientEmail);
    }

    [HttpGet("{appointmentId:int}/payment-status")]
    public async Task<IActionResult> GetPaymentStatus(
        int appointmentId)
    {
        var patient = await GetPatientAsync();

        if (patient == null || patient.HospitalId == null)
            return Unauthorized(new
            {
                message = "Patient authentication required."
            });

        var appointment = await _db.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == appointmentId &&
                a.PatientId == patient.Id &&
                a.HospitalId == patient.HospitalId.Value);

        if (appointment == null)
            return NotFound(new
            {
                message = "Appointment not found."
            });

        var attempts = await _db.PatientAppointmentPayments
            .AsNoTracking()
            .Where(p =>
                p.AppointmentId == appointment.Id &&
                p.PatientId == patient.Id &&
                p.HospitalId == patient.HospitalId.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                p.Currency,
                p.Status,
                p.CreatedAt,
                p.ExpiresAt,
                p.CapturedAt
            })
            .ToListAsync();

        return Ok(new
        {
            appointmentId = appointment.Id,
            appointmentStatus = appointment.Status,
            paymentStatus = appointment.PaymentStatus,
            consultationFee = appointment.ConsultationFee,
            paymentAttempts = attempts
        });
    }
}
