using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Data;

namespace AuthDemo.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;
    private readonly ILogger<AppointmentService> _logger;


    public AppointmentService(
        AppDbContext context,
        EmailHelper emailHelper,
        ILogger<AppointmentService> logger)
    {
        _context = context;
        _emailHelper = emailHelper;
        _logger = logger;
    }


    // =====================================================
    // PARSE 12-HOUR TIME (09:00 AM / 08:00 PM)
    // =====================================================

    private static bool TryParse12HourTime(
        string? input,
        out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var acceptedFormats = new[]
        {
            "hh:mm tt",
            "h:mm tt"
        };

        var success = DateTime.TryParseExact(
            input.Trim(),
            acceptedFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsedDateTime);

        if (!success)
        {
            return false;
        }

        time = parsedDateTime.TimeOfDay;
        return true;
    }



    // =====================================================
    // CREATE APPOINTMENT
    // =====================================================

    public async Task CreateAsync(
        BookSlotDto dto,
        int hospitalId)
    {
        if (!TryParse12HourTime(
                dto.StartTime,
                out var appointmentStartTime))
        {
            throw new Exception(
                "Invalid StartTime. Use 12-hour format like 09:00 AM or 08:00 PM.");
        }

        var doctor =
            await _context.Doctors
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.DoctorId &&
                    x.HospitalId == hospitalId);

        if (doctor == null)
        {
            throw new Exception("Doctor not found");
        }

        if (!doctor.IsActive)
        {
            throw new Exception("Doctor is inactive");
        }

        // A doctor may have one primary branch in Doctors.BranchId and
        // additional active branch assignments in DoctorBranches.
        var doctorAssignedToBranch =
            doctor.BranchId == dto.BranchId ||
            await _context.DoctorBranches.AnyAsync(x =>
                x.DoctorId == doctor.Id &&
                x.BranchId == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (!doctorAssignedToBranch)
        {
            throw new Exception(
                "Doctor is not assigned to the selected branch");
        }



        // =========================================
        // PAYMENT VALIDATION
        // =========================================

        if (dto.PaymentStatus != "Paid")
        {
            throw new Exception("Consultation fee must be paid before booking appointment.");
        }

        // Transaction Id required only for Online payment

        if (dto.PaymentMode == "Online" &&
            string.IsNullOrWhiteSpace(dto.TransactionId))
        {
            throw new Exception("Transaction Id is required for online payment.");
        }


        var patient =
            await _context.Patients
            .FirstOrDefaultAsync(x =>
                x.Id == dto.PatientId &&
                x.HospitalId == hospitalId);



        if (patient == null)
            throw new Exception("Patient not found");



        var branch =
            await _context.Branches
            .FirstOrDefaultAsync(x =>
                x.Id == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.IsActive);



        if (branch == null)
            throw new Exception("Invalid branch");

        // Serialize bookings for the entire clinic, across all branches.
        // Every appointment-writing path must acquire this same Hospital row lock
        // to guarantee the daily limit under concurrent requests.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var lockedHospitalId = await _context.Hospitals
            .FromSqlInterpolated($"SELECT * FROM [Hospitals] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {hospitalId}")
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync();
        if (lockedHospitalId == 0)
            throw new Exception("Clinic not found.");

        var nowUtc = DateTime.UtcNow;
        var subscription = await _context.ClinicSubscriptions
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId &&
                        x.Status == "Active" &&
                        x.StartDate <= nowUtc && x.EndDate > nowUtc &&
                        x.SupersededAt == null)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (subscription == null)
            throw new Exception("An active clinic subscription is required to book appointments.");

        if (subscription.PurchasedDailyAppointmentLimit <= 0)
            throw new Exception("Clinic subscription has no valid daily appointment limit.");

        // Appointment.Date is the scheduled date, not the date of booking.
        // Include all operational statuses (Waiting, Called, InProgress,
        // InConsultation, PrescriptionAdded, Completed and NoShow).
        // Only cancelled appointments release a daily quota slot.
        var targetDate = dto.Date.Date;
        var nextDate = targetDate.AddDays(1);
        var clinicDayCount = await _context.Appointments.CountAsync(x =>
            x.HospitalId == hospitalId &&
            x.Date >= targetDate && x.Date < nextDate &&
            x.Status != "Cancelled");

        if (clinicDayCount >= subscription.PurchasedDailyAppointmentLimit)
            throw new Exception(
                $"Clinic daily appointment limit reached for {targetDate:yyyy-MM-dd}. " +
                $"Limit: {subscription.PurchasedDailyAppointmentLimit}, booked: {clinicDayCount}.");

        // =================================================
        // EFFECTIVE SCHEDULE VALIDATION
        // Base schedule + leave/time-change/branch-shift overrides.
        // This prevents receptionist/admin from booking a slot after the
        // doctor has moved to another branch or marked leave.
        // =================================================
        var scheduleSetting = await _context.ScheduleSettings
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var slotDuration = scheduleSetting?.SlotDuration ?? 30;
        if (slotDuration <= 0) slotDuration = 30;

        var availability = await ScheduleAvailabilityHelper.ValidateBookableSlotAsync(
            _context,
            hospitalId,
            dto.DoctorId,
            (int)dto.BranchId,
            dto.Date.Date,
            appointmentStartTime,
            slotDuration);

        if (!availability.ok)
            throw new Exception(availability.message);


        var alreadyBooked =
            await _context.Appointments
            .AnyAsync(x =>
                x.HospitalId == hospitalId &&
                x.DoctorId == dto.DoctorId &&
                x.Date.Date == dto.Date.Date &&
                x.StartTime == appointmentStartTime &&
                x.Status != "Cancelled");



        if (alreadyBooked)
            throw new Exception("Slot already booked");



        // =================================================
        // GENERATE DAILY TOKEN NUMBER
        // =================================================

        var todayTokenCount = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.Date >= targetDate && x.Date < nextDate);

        var tokenNumber = $"TKN{(todayTokenCount + 1):D3}";



        var appointment = new Appointment
        {
            HospitalId = hospitalId,

            BranchId = dto.BranchId,

            DoctorId = dto.DoctorId,

            PatientId = dto.PatientId,


            Date = dto.Date,

            StartTime = appointmentStartTime,


            TokenNumber = tokenNumber,
            BookingType = "Offline",

            ConsultationFee = dto.PaidAmount,

            PaymentMode = dto.PaymentMode,

            PaymentStatus = dto.PaymentStatus,

            PaymentDate = DateTime.UtcNow,

            TransactionId =
    dto.PaymentMode == "Cash"
        ? null
        : dto.TransactionId,


            ChiefComplaints = dto.ChiefComplaints,

            BloodPressure = dto.BloodPressure,

            SugarLevel = dto.SugarLevel,

            Temperature = dto.Temperature,

            Weight = dto.Weight,

            PulseRate = dto.PulseRate,

            RespiratoryRate = dto.RespiratoryRate,


            Status = "Waiting"
        };


        // Appointment, OP billing and quota check share one transaction.
        // Billings remain the source of truth for OP revenue.

        try
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Offline booking already requires the consultation fee to be paid.
            // Persist that payment as a real OP bill instead of only storing it
            // on the Appointment row.
            var opAmount = dto.PaidAmount > 0
                ? dto.PaidAmount
                : doctor.Fees;

            if (opAmount <= 0)
            {
                throw new Exception("Consultation fee must be greater than zero.");
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
                PaymentMode = string.IsNullOrWhiteSpace(dto.PaymentMode)
                    ? "Cash"
                    : dto.PaymentMode.Trim(),
                Status = "Paid",
                CreatedAt = DateTime.UtcNow
            };

            _context.Billings.Add(opBill);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }


        // The appointment and OP bill have already been committed.
        // Email is a best-effort notification: an SMTP failure must not
        // report a failed booking or encourage a duplicate retry.
        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            try
            {
                var hospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == hospitalId);

                await _emailHelper.SendAppointmentConfirmation(
                    patient.Email,
                    patient.Name,
                    hospital?.Name ?? "Clinic",
                    doctor.Name,
                    appointment.TokenNumber,
                    appointment.Date,
                    appointment.StartTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Appointment {AppointmentId} was saved but confirmation email failed for hospital {HospitalId}.",
                    appointment.Id, hospitalId);
            }
        }

    }




    // =====================================================
    // GET ALL APPOINTMENTS
    // =====================================================

    public async Task<List<AppointmentResponseDto>>
        GetAllAsync(int hospitalId)
    {

        return await _context.Appointments

        .Where(x =>
            x.HospitalId == hospitalId)

        .OrderByDescending(x =>
            x.CreatedAt)


        .Select(x => new AppointmentResponseDto
        {

            Id = x.Id,


            DoctorId = x.DoctorId,

            PatientId = x.PatientId,


            TokenNumber = x.TokenNumber,


            Date = x.Date,


            Time =
               x.Date.Date
              .Add(x.StartTime)
              .ToString("hh:mm tt"),
            BookingType = x.BookingType,



            Status = x.Status,



            PatientName =
                x.Patient.Name,


            PatientCode =
                x.Patient.PatientCode,


            Age =
                x.Patient.Age,


            Gender =
                x.Patient.Gender,


            Phone =
                x.Patient.Phone,


            BloodGroup =
                x.Patient.BloodGroup,



            DoctorName =
                x.Doctor.Name,


            DoctorSpecialization =
                x.Doctor.Specialization,



            BranchId =
                x.BranchId,


            BranchName =
                x.Branch != null
                ? x.Branch.Name
                : null,


            HospitalId =
                x.HospitalId,



            ConsultationFee =
                x.ConsultationFee,


            PaymentMode =
                x.PaymentMode,


            PaymentStatus =
                x.PaymentStatus,


            ChiefComplaints =
                x.ChiefComplaints,


            BloodPressure =
                x.BloodPressure,


            SugarLevel =
                x.SugarLevel,


            Temperature =
                x.Temperature,


            Weight =
                x.Weight,


            PulseRate =
                x.PulseRate,


            RespiratoryRate =
                x.RespiratoryRate


        })

        .ToListAsync();

    }





    // =====================================================
    // GET APPOINTMENTS BY BRANCH AND DOCTOR
    // =====================================================

    public async Task<List<AppointmentResponseDto>>
        GetByBranchAsync(
            int branchId,
            int doctorId)
    {

        return await _context.Appointments

            .Where(x =>
                x.BranchId == branchId &&
                x.DoctorId == doctorId)

            .OrderBy(x =>
                x.Date)

            .Select(x => new AppointmentResponseDto
            {

                Id = x.Id,


                BranchId = x.BranchId,

                HospitalId = x.HospitalId,


                DoctorId = x.DoctorId,

                PatientId = x.PatientId,


                TokenNumber =
                    x.TokenNumber,


                Date =
                    x.Date,


                Time =
                    x.Date.Date
                   .Add(x.StartTime)
                   .ToString("hh:mm tt"),


                Status =
                    x.Status,


                PatientName =
                    x.Patient.Name,


                DoctorName =
                    x.Doctor.Name,


                ConsultationFee =
                    x.ConsultationFee,


                PaymentMode =
                    x.PaymentMode,


                PaymentStatus =
                    x.PaymentStatus


            })

            .ToListAsync();

    }





    // =====================================================
    // UPDATE STATUS
    // =====================================================

    public async Task<bool>
        UpdateStatusAsync(
            int id,
            string status,
            int hospitalId)
    {

        var appointment =
            await _context.Appointments
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == hospitalId);



        if (appointment == null)
            return false;



        var allowedStatus =
            new[]
            {
                "Waiting",
                "Called",
                "InProgress",
                "InConsultation",
                "PrescriptionAdded",
                "Completed",
                "Cancelled",
                "NoShow"
            };



        if (!allowedStatus.Contains(status))
            throw new Exception("Invalid appointment status");



        appointment.Status = status;


        await _context.SaveChangesAsync();


        return true;

    }

}