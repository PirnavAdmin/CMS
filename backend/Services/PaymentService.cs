using System.Data;
using System.Globalization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;


namespace AuthDemo.Services;


public class PaymentService : IPaymentService
{

    private readonly AppDbContext _context;

    private readonly IAppointmentService _appointmentService;



    public PaymentService(
        AppDbContext context,
        IAppointmentService appointmentService)
    {
        _context = context;

        _appointmentService =
            appointmentService;
    }



    // =====================================================
    // CREATE PAYMENT
    // =====================================================

    public async Task<int> CreatePaymentAsync(
        CreatePaymentDto dto,
        int hospitalId)
    {

        // =================================================// VALIDATE 12-HOUR START TIME// =================================================
        TimeSpan appointmentTime =
             Parse12HourTime(dto.StartTime);



        // =================================================
        // CHECK PATIENT
        // =================================================

        var patient =
            await _context.Patients
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.PatientId &&
                    x.HospitalId == hospitalId
                );


        if (patient == null)
        {
            throw new Exception(
                "Patient not found"
            );
        }



        // =================================================
        // CREATE PAYMENT
        // =================================================
        var payment = new Payment
        {
            AppointmentId = dto.AppointmentId,
            PatientId = dto.PatientId,

            DoctorId = dto.DoctorId,

            BranchId = dto.BranchId,

            HospitalId = hospitalId,

            AppointmentDate = dto.Date.Date,

            AppointmentTime = appointmentTime,

            Amount = dto.Amount,

            PaymentMode = dto.PaymentMode,

            Status = "Pending"
        };


        _context.Payments.Add(payment);


        await _context.SaveChangesAsync();


        return payment.Id;

    }





    // =====================================================
    // COMPLETE PAYMENT
    // =====================================================

    public async Task<bool> CompletePaymentAsync(
        PaymentSuccessDto dto,
        int hospitalId)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.PaymentId &&
                    x.HospitalId == hospitalId);

            if (payment == null)
            {
                throw new Exception("Payment not found");
            }

            if (payment.Status == "Paid")
            {
                throw new Exception("Payment already completed");
            }

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(x =>
                    x.Id == payment.AppointmentId &&
                    x.HospitalId == hospitalId &&
                    x.PatientId == payment.PatientId);

            if (appointment == null)
            {
                throw new Exception("Appointment not found");
            }

            payment.Status = "Paid";
            payment.TransactionId = dto.TransactionId;
            payment.PaymentDate = DateTime.UtcNow;

            appointment.PaymentStatus = "Paid";
            appointment.PaymentMode = payment.PaymentMode;
            appointment.TransactionId = payment.TransactionId;
            appointment.PaymentDate = payment.PaymentDate;
            appointment.ConsultationFee = payment.Amount;
            appointment.Status = "Waiting";

            // Generate a daily queue token separately for each doctor and branch.
            // Example: TKN 001, TKN 002, TKN 003.
            appointment.TokenNumber = await GenerateNextQueueTokenAsync(
                appointment.DoctorId,
                appointment.BranchId,
                appointment.Date.Date);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GenerateNextQueueTokenAsync(
        int doctorId,
        int? branchId,
        DateTime appointmentDate)
    {
        var existingTokens = await _context.Appointments
            .Where(x =>
                x.DoctorId == doctorId &&
                x.BranchId == branchId &&
                x.Date.Date == appointmentDate.Date &&
                x.TokenNumber.StartsWith("TKN"))
            .Select(x => x.TokenNumber)
            .ToListAsync();

        var highestSequence = existingTokens
            .Select(ParseQueueTokenSequence)
            .DefaultIfEmpty(0)
            .Max();

        return $"TKN {highestSequence + 1:000}";
    }

    private static int ParseQueueTokenSequence(string? tokenNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenNumber))
        {
            return 0;
        }

        var digits = new string(tokenNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var sequence) ? sequence : 0;
    }

    // =====================================================// PARSE 12-HOUR TIME// =====================================================
    private static TimeSpan Parse12HourTime(
    string? input)
 {
     if (string.IsNullOrWhiteSpace(input))
     {
         throw new Exception(
             "Start time is required. " +
             "Use 12-hour format such as 09:30 AM or 02:30 PM.");
     }

     string[] acceptedFormats =
     {
         "hh:mm tt",
         "h:mm tt",
         "hh:mm:ss tt",
         "h:mm:ss tt"     };

    bool isValid = DateTime.TryParseExact(
        input.Trim(),
        acceptedFormats,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AllowWhiteSpaces,
        out DateTime parsedTime);

     if (!isValid)
     {
         throw new Exception(
             "Invalid start time. " +
             "Use 12-hour format such as 09:30 AM or 02:30 PM.");
     }

     return parsedTime.TimeOfDay;
 }
 

}