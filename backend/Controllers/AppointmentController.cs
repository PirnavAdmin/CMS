using AuthDemo.Authorization;
using AuthDemo.Helpers;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services;
using AuthDemo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AuthDemo.Controllers;

[ApiController]
[RequirePermission("Appointments")]

[Route("api/[controller]")]
[Authorize]
public class AppointmentController
    : ControllerBase
{
    private readonly IAppointmentService
        _appointmentService;

    private readonly AppDbContext
        _context;

    public AppointmentController(
        IAppointmentService appointmentService,
        AppDbContext context)
    {
        _appointmentService =
            appointmentService;

        _context =
            context;
    }



    // =====================================================
    // GET HOSPITAL ID
    // =====================================================

    private int GetHospitalId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "HospitalId"
            );

        if (claim == null)
        {
            return 0;
        }

        return int.Parse(
            claim.Value
        );
    }

    // =====================================================
    // GET ROLE
    // =====================================================

    private string GetRole()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "role"
            );

        if (claim == null)
        {
            return "";
        }

        return claim.Value;
    }

    // =====================================================
    // GET DOCTOR ID
    // =====================================================

    private int GetDoctorId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "DoctorId"
            );

        if (claim == null)
        {
            return 0;
        }

        return int.Parse(
            claim.Value
        );
    }

    // =====================================================
    // CREATE APPOINTMENT
    // =====================================================

    [Authorize(Roles =
        "Admin,Receptionist")]

    [HttpPost]
    public async Task<IActionResult>
        Create(
            [FromBody] BookSlotDto dto)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid HospitalId in authentication token."
            });
        }

        try
        {
            await _appointmentService
                .CreateAsync(
                    dto,
                    hospitalId
                );

            return Ok(new
            {
                message =
                    "Appointment booked successfully",

                appointmentDate =
                    dto.Date.Date.ToString("yyyy-MM-dd"),

                appointmentTime =
                    dto.StartTime
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
    // GET ALL APPOINTMENTS
    // =====================================================

    [HttpGet]
    public async Task<IActionResult>
        GetAll()
    {
        var hospitalId =
            GetHospitalId();

        var role =
            GetRole();

        var doctorId =
            GetDoctorId();

        var data =
            await _appointmentService
                .GetAllAsync(
                    hospitalId
                );

        // =========================================
        // DOCTOR ONLY HIS APPOINTMENTS
        // =========================================

        if (role == "Doctor")
        {
            data =
                data.Where(x =>
                    x.DoctorId ==
                    doctorId
                )

                .ToList();
        }

        return Ok(data);
    }

    // =====================================================
    // UPDATE STATUS
    // =====================================================


    [HttpPatch("{id}/status")]
    public async Task<IActionResult>
        UpdateStatus(
            int id,

            [FromQuery]
            string status)
    {
        var hospitalId =
            GetHospitalId();

        var updated =
            await _appointmentService
                .UpdateStatusAsync(
                    id,
                    status,
                    hospitalId
                );

        if (!updated)
        {
            return NotFound(
                "Appointment not found"
            );
        }

        return Ok(new
        {
            message =
                "Appointment status updated"
        });
    }
    [HttpPost("{appointmentId}/documents")]
    public async Task<IActionResult>
UploadDocument(
    int appointmentId,
    IFormFile file)
    {
        var hospitalId =
            GetHospitalId();

        var appointment =
            await _context.Appointments
                .FirstOrDefaultAsync(x =>

                    x.Id ==
                    appointmentId &&

                    x.HospitalId ==
                    hospitalId);

        if (appointment == null)
        {
            return NotFound(
                "Appointment not found");
        }

        if (file == null ||
            file.Length == 0)
        {
            return BadRequest(
                "File required");
        }

        var folder =
            Path.Combine(
                "wwwroot",
                "documents");

        Directory.CreateDirectory(
            folder);

        var uniqueName =
            Guid.NewGuid() +
            Path.GetExtension(
                file.FileName);

        var filePath =
            Path.Combine(
                folder,
                uniqueName);

        using var stream =
            new FileStream(
                filePath,
                FileMode.Create);

        await file.CopyToAsync(
            stream);

        var document =
            new AppointmentDocument
            {
                AppointmentId =
                    appointmentId,

                FileName =
                    file.FileName,

                FilePath =
                    "/documents/" +
                    uniqueName,

                HospitalId =
                    hospitalId
            };

        _context.AppointmentDocuments
            .Add(document);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Document uploaded"
        });
    }
    [HttpGet("{appointmentId}/documents")]
    public async Task<IActionResult>
GetDocuments(
    int appointmentId)
    {
        var hospitalId =
            GetHospitalId();

        var data =
            await _context
                .AppointmentDocuments

                .Where(x =>

                    x.AppointmentId ==
                    appointmentId &&

                    x.HospitalId ==
                    hospitalId)

                .ToListAsync();

        return Ok(data);
    }
    [HttpGet("chief-complaints")]
    public async Task<IActionResult> GetChiefComplaints()
    {
        var defaults = new List<string>
    {
        "Fever",
        "Cold and Cough",
        "Headache",
        "Stomach Pain",
        "Body Pains",
        "Diabetes Follow-up",
        "High Blood Pressure",
        "Chest Pain",
        "Breathing Difficulty",
        "General Checkup"
    };

        var dbData =
            await _context.ChiefComplaints
                .Select(x => x.Name)
                .ToListAsync();

        var result =
            defaults
                .Union(dbData)
                .OrderBy(x => x)
                .ToList();

        return Ok(result);
    }
    [HttpGet("branch/{branchId}")]
    public async Task<IActionResult>
    GetByBranch(int branchId)
    {
        var hospitalId = GetHospitalId();

        var data =
            await _appointmentService
                .GetByBranchAsync(
                    hospitalId,
                    branchId
                );

        return Ok(data);
    }
    [Authorize(Roles = "Admin,Receptionist")]

    [HttpGet("online")]

    public async Task<IActionResult> GetOnlineAppointments()

    {
        var hospitalId = GetHospitalId();
        var role = GetRole();
        var branchClaim = User.Claims.FirstOrDefault(x => x.Type == "BranchId")?.Value;
        int? userBranchId = int.TryParse(branchClaim, out var parsedBranchId)
            ? parsedBranchId
            : null;

        var appointments = await _context.Appointments

            .Include(x => x.Patient)

            .Include(x => x.Doctor)

            .Include(x => x.Branch)

            .Where(x =>
                x.BookingType == "Online" &&
                x.HospitalId == hospitalId &&
                (role != "Receptionist" || !userBranchId.HasValue || x.BranchId == userBranchId))

            .OrderByDescending(x => x.Date)

            .ThenBy(x => x.StartTime)

            .Select(x => new AppointmentResponseDto

            {

                Id = x.Id,

                HospitalId = x.HospitalId,

                BranchId = x.BranchId,

                BranchName = x.Branch != null ? x.Branch.Name : null,

                DoctorId = x.DoctorId,

                PatientId = x.PatientId,

                TokenNumber = x.TokenNumber,

                PatientName = x.Patient.Name,

                PatientCode = x.Patient.PatientCode,

                Age = x.Patient.Age,

                Gender = x.Patient.Gender,

                Phone = x.Patient.Phone,

                BloodGroup = x.Patient.BloodGroup,

                DoctorName = x.Doctor.Name,

                DoctorSpecialization = x.Doctor.Specialization,

                Date = x.Date,

                Time = DateTime.Today
                 .Add(x.StartTime)
                 .ToString("hh:mm tt"),

                ChiefComplaints = x.ChiefComplaints,

                BloodPressure = x.BloodPressure,

                SugarLevel = x.SugarLevel,

                Temperature = x.Temperature,

                Weight = x.Weight,

                PulseRate = x.PulseRate,

                RespiratoryRate = x.RespiratoryRate,

                ConsultationFee = x.ConsultationFee,

                PaymentMode = x.PaymentMode,

                PaymentStatus = x.PaymentStatus,

                BookingType = x.BookingType,

                Status = x.Status

            })

            .ToListAsync();

        return Ok(appointments);

    }
    [Authorize(Roles = "Admin,Receptionist")]

    [HttpGet("offline")]

    public async Task<IActionResult> GetOfflineAppointments()

    {
        var hospitalId = GetHospitalId();
        var role = GetRole();
        var branchClaim = User.Claims.FirstOrDefault(x => x.Type == "BranchId")?.Value;
        int? userBranchId = int.TryParse(branchClaim, out var parsedBranchId)
            ? parsedBranchId
            : null;

        var appointments = await _context.Appointments

            .Include(x => x.Patient)

            .Include(x => x.Doctor)

            .Include(x => x.Branch)

            .Where(x =>
                x.BookingType == "Offline" &&
                x.HospitalId == hospitalId &&
                (role != "Receptionist" || !userBranchId.HasValue || x.BranchId == userBranchId))

            .OrderByDescending(x => x.Date)

            .ThenBy(x => x.StartTime)

            .Select(x => new AppointmentResponseDto

            {

                Id = x.Id,

                HospitalId = x.HospitalId,

                BranchId = x.BranchId,

                BranchName = x.Branch != null ? x.Branch.Name : null,

                DoctorId = x.DoctorId,

                PatientId = x.PatientId,

                TokenNumber = x.TokenNumber,

                PatientName = x.Patient.Name,

                PatientCode = x.Patient.PatientCode,

                Age = x.Patient.Age,

                Gender = x.Patient.Gender,

                Phone = x.Patient.Phone,

                BloodGroup = x.Patient.BloodGroup,

                DoctorName = x.Doctor.Name,

                DoctorSpecialization = x.Doctor.Specialization,

                Date = x.Date,

                Time = DateTime.Today
                  .Add(x.StartTime)
                  .ToString("hh:mm tt"),

                ChiefComplaints = x.ChiefComplaints,

                BloodPressure = x.BloodPressure,

                SugarLevel = x.SugarLevel,

                Temperature = x.Temperature,

                Weight = x.Weight,

                PulseRate = x.PulseRate,

                RespiratoryRate = x.RespiratoryRate,

                ConsultationFee = x.ConsultationFee,

                PaymentMode = x.PaymentMode,

                PaymentStatus = x.PaymentStatus,

                BookingType = x.BookingType,

                Status = x.Status

            })

            .ToListAsync();

        return Ok(appointments);

    }



    // =====================================================
    // RECEPTIONIST: ADD OR UPDATE VITALS FOR ONLINE BOOKING
    // =====================================================

    [Authorize(Roles = "Admin,Receptionist")]
    [HttpPut("online/{appointmentId:int}/vitals")]
    public async Task<IActionResult> UpsertOnlineAppointmentVitals(
        int appointmentId,
        [FromBody] UpsertPatientVitalsDto dto)
    {
        var hospitalId = GetHospitalId();
        var role = GetRole();
        var branchClaim = User.Claims.FirstOrDefault(x => x.Type == "BranchId")?.Value;
        int? userBranchId = int.TryParse(branchClaim, out var parsedBranchId)
            ? parsedBranchId
            : null;

        var appointment = await _context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.HospitalId == hospitalId &&
                x.BookingType == "Online");

        if (appointment == null)
        {
            return NotFound(new
            {
                message = "Online appointment not found in your hospital"
            });
        }

        if (role == "Receptionist" &&
            userBranchId.HasValue &&
            appointment.BranchId != userBranchId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can add vitals only for appointments in your assigned branch"
            });
        }

        var vitals = await _context.PatientVitals
            .FirstOrDefaultAsync(x =>
                x.AppointmentId == appointmentId &&
                x.HospitalId == hospitalId);

        if (vitals == null)
        {
            vitals = new PatientVitals
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                HospitalId = hospitalId,
                CreatedAt = DateTime.UtcNow
            };

            _context.PatientVitals.Add(vitals);
        }

        vitals.Symptoms = dto.Symptoms ?? string.Empty;
        vitals.BloodPressure = dto.BloodPressure ?? string.Empty;
        vitals.SugarLevel = dto.SugarLevel ?? string.Empty;
        vitals.Temperature = dto.Temperature ?? string.Empty;
        vitals.Weight = dto.Weight ?? string.Empty;
        vitals.PulseRate = dto.PulseRate ?? string.Empty;
        vitals.RespiratoryRate = dto.RespiratoryRate ?? string.Empty;
        vitals.CreatedAt = DateTime.UtcNow;

        // Keep the appointment response backward-compatible for the current UI.
        appointment.ChiefComplaints = string.IsNullOrWhiteSpace(dto.Symptoms)
            ? appointment.ChiefComplaints
            : dto.Symptoms;
        appointment.BloodPressure = dto.BloodPressure;
        appointment.SugarLevel = dto.SugarLevel;
        appointment.Temperature = dto.Temperature;
        appointment.Weight = dto.Weight;
        appointment.PulseRate = dto.PulseRate;
        appointment.RespiratoryRate = dto.RespiratoryRate;

        var userIdClaim = User.Claims.FirstOrDefault(x =>
            x.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Claims.FirstOrDefault(x =>
            x.Type == System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? role;

        if (int.TryParse(userIdClaim, out var userId))
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                UserName = userName ?? role,
                Role = role,
                ClinicId = hospitalId,
                BranchId = appointment.BranchId,
                Action = "Update Patient Vitals",
                SystemAction = $"Vitals recorded for appointment {appointment.Id} and patient {appointment.Patient.Name}",
                IsLoginActivity = false,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Browser = Request.Headers.UserAgent.ToString(),
                Timestamp = DateTime.UtcNow,
                LoginTime = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Patient vitals saved successfully",
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName = appointment.Patient.Name,
            branchId = appointment.BranchId,
            branchName = appointment.Branch != null ? appointment.Branch.Name : null,
            vitals = new
            {
                vitals.Symptoms,
                vitals.BloodPressure,
                vitals.SugarLevel,
                vitals.Temperature,
                vitals.Weight,
                vitals.PulseRate,
                vitals.RespiratoryRate,
                recordedAt = vitals.CreatedAt
            }
        });
    }

    [Authorize(Roles = "Admin,Receptionist,Doctor")]
    [HttpGet("{appointmentId:int}/vitals")]
    public async Task<IActionResult> GetAppointmentVitals(int appointmentId)
    {
        var hospitalId = GetHospitalId();
        var role = GetRole();
        var doctorId = GetDoctorId();
        var branchClaim = User.Claims.FirstOrDefault(x => x.Type == "BranchId")?.Value;
        int? userBranchId = int.TryParse(branchClaim, out var parsedBranchId)
            ? parsedBranchId
            : null;

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.HospitalId == hospitalId);

        if (appointment == null)
            return NotFound(new { message = "Appointment not found" });

        if (role == "Doctor" && appointment.DoctorId != doctorId)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "This appointment is not assigned to you" });

        if (role == "Receptionist" && userBranchId.HasValue && appointment.BranchId != userBranchId)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "This appointment belongs to another branch" });

        var vitals = await _context.PatientVitals
            .AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId && x.HospitalId == hospitalId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName = appointment.Patient.Name,
            patientCode = appointment.Patient.PatientCode,
            bookingType = appointment.BookingType,
            branchId = appointment.BranchId,
            branchName = appointment.Branch != null ? appointment.Branch.Name : null,
            symptoms = vitals?.Symptoms ?? appointment.ChiefComplaints,
            bloodPressure = vitals?.BloodPressure ?? appointment.BloodPressure,
            sugarLevel = vitals?.SugarLevel ?? appointment.SugarLevel,
            temperature = vitals?.Temperature ?? appointment.Temperature,
            weight = vitals?.Weight ?? appointment.Weight,
            pulseRate = vitals?.PulseRate ?? appointment.PulseRate,
            respiratoryRate = vitals?.RespiratoryRate ?? appointment.RespiratoryRate,
            recordedAt = vitals?.CreatedAt
        });
    }

}

