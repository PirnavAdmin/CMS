using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AuthDemo.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;


    public AppointmentService(
        AppDbContext context,
        EmailHelper emailHelper)
    {
        _context = context;
        _emailHelper = emailHelper;
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
                x.HospitalId == hospitalId &&
                x.BranchId == dto.BranchId);


        if (doctor == null)
            throw new Exception("Doctor not found");


        if (!doctor.IsActive)
            throw new Exception("Doctor is inactive");



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



        var todayCount =
            await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == dto.BranchId &&
                x.DoctorId == dto.DoctorId &&
                x.Date.Date == dto.Date.Date);


        // =================================================
        // GENERATE DAILY TOKEN NUMBER
        // =================================================

        var todayTokenCount = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.Date.Date == dto.Date.Date);

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


        _context.Appointments.Add(appointment);


        await _context.SaveChangesAsync();



        var hospital =
            await _context.Hospitals
            .FirstOrDefaultAsync(x =>
                x.Id == hospitalId);



        if (!string.IsNullOrWhiteSpace(patient.Email))
        {

            await _emailHelper.SendAppointmentConfirmation(

                patient.Email,

                patient.Name,

                hospital?.Name ?? "Clinic",

                doctor.Name,

                appointment.TokenNumber,

                appointment.Date,

                appointment.StartTime
            );
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