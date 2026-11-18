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

            // Persist the successful online consultation payment as an OP bill.
            // Revenue is calculated from Billings, so a paid appointment must have
            // a corresponding paid OP Billing row.
            var existingOpBill = await _context.Billings
                .AnyAsync(x =>
                    x.AppointmentId == appointment.Id &&
                    x.HospitalId == hospitalId &&
                    x.BillingType == "OP");

            if (!existingOpBill)
            {
                var opAmount = payment.Amount;

                if (opAmount <= 0)
                {
                    throw new Exception("Consultation payment amount must be greater than zero");
                }

                var opBill = new Billing
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    HospitalId = hospitalId,
                    BranchId = appointment.BranchId,
                    BillingType = "OP",
                    ConsultationCharge = opAmount,
                    LabCharge = 0,
                    MedicineCharge = 0,
                    SubTotal = opAmount,
                    Discount = 0,
                    GstPercentage = 0,
                    GstAmount = 0,
                    TotalAmount = opAmount,
                    PaymentMode = string.IsNullOrWhiteSpace(payment.PaymentMode)
                        ? "Cash"
                        : payment.PaymentMode.Trim(),
                    Status = "Paid",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Billings.Add(opBill);
                _context.BillingPaymentTransactions.Add(new BillingPaymentTransaction
                {
                    Billing = opBill,
                    HospitalId = hospitalId,
                    BranchId = appointment.BranchId,
                    Amount = opAmount,
                    PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMode) ? "Cash" : payment.PaymentMode.Trim(),
                    ReferenceNumber = payment.TransactionId,
                    Status = "Paid",
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = DateTime.UtcNow
                });
            }
            else
            {
                var existingBill = await _context.Billings.FirstOrDefaultAsync(x =>
                    x.AppointmentId == appointment.Id && x.HospitalId == hospitalId && x.BillingType == "OP");
                if (existingBill != null && !await _context.BillingPaymentTransactions.AnyAsync(x =>
                    x.BillingId == existingBill.Id && x.HospitalId == hospitalId && x.ReferenceNumber == payment.TransactionId))
                {
                    _context.BillingPaymentTransactions.Add(new BillingPaymentTransaction
                    {
                        BillingId = existingBill.Id,
                        HospitalId = hospitalId,
                        BranchId = existingBill.BranchId,
                        Amount = payment.Amount,
                        PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMode) ? "Cash" : payment.PaymentMode.Trim(),
                        ReferenceNumber = payment.TransactionId,
                        Status = "Paid",
                        CreatedAt = DateTime.UtcNow,
                        PaidAt = DateTime.UtcNow
                    });
                    var priorCollected = await _context.BillingPaymentTransactions
                        .Where(x => x.BillingId == existingBill.Id && x.HospitalId == hospitalId && x.Status == "Paid")
                        .SumAsync(x => (decimal?)x.Amount) ?? 0m;
                    if (payment.Amount <= 0 || payment.Amount > existingBill.TotalAmount - priorCollected)
                        throw new InvalidOperationException("Consultation payment exceeds the outstanding OP invoice balance.");
                    existingBill.Status = priorCollected + payment.Amount >= existingBill.TotalAmount ? "Paid" : "PartiallyPaid";
                }
            }

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