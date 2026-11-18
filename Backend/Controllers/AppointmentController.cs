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
using System.Security.Claims;
using Microsoft.Extensions.Logging;

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

    private int GetHospitalId() =>
        int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    // =====================================================
    // GET ROLE
    // =====================================================
    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role) ??
        User.FindFirstValue("role") ?? string.Empty;

    // =====================================================
    // GET DOCTOR ID
    // =====================================================
    private int GetDoctorId() =>
        int.TryParse(User.FindFirstValue("DoctorId"), out var id) ? id : 0;

    private int? GetBranchId() =>
        int.TryParse(User.FindFirstValue("BranchId"), out var id) ? id : null;

    // =====================================================
    // CREATE APPOINTMENT
    // =====================================================

    [Authorize(Roles = "Admin,Receptionist")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookSlotDto dto)
    {
        var hospitalId = GetHospitalId();
        if (hospitalId <= 0)
            return Unauthorized(new { message = "Invalid HospitalId in authentication token." });

        if (dto == null)
            return BadRequest(new { message = "Appointment details are required." });

        if (dto.Date == default)
            return BadRequest(new { message = "A valid appointment date is required." });

        // A receptionist must not book an appointment in another branch.
        // This check supplements, rather than replaces, the service's
        // doctor/branch/slot availability and conflict checks.
        if (User.IsInRole("Receptionist"))
        {
            var assignedBranchId = GetBranchId();
            if (!assignedBranchId.HasValue || assignedBranchId.Value <= 0)
                return StatusCode(403, new { message = "Receptionist branch is missing from the authentication token." });

            // BookSlotDto's branch property must be checked in the service,
            // since its exact declaration was not provided in this upload.
        }

        try
        {
            await _appointmentService.CreateAsync(dto, hospitalId);
            return Ok(new
            {
                message = "Appointment booked successfully",
                appointmentDate = dto.Date.Date.ToString("yyyy-MM-dd"),
                appointmentTime = dto.StartTime
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Booking conflicts and unavailable slots are business-rule errors.
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            // Do not disclose database implementation details to clients.
            return StatusCode(500, new
            {
                message = "Appointment could not be saved. Check server logs for the database error."
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                message = "Unexpected appointment booking error. Check server logs."
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
    // =====================================================
    // UPLOAD APPOINTMENT DOCUMENT
    // =====================================================

    private static readonly HashSet<string> AllowedDocumentExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx"
        };

    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost("{appointmentId:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxDocumentSizeBytes)]
    public async Task<IActionResult> UploadDocument(
        int appointmentId,
        IFormFile file)
    {
        var hospitalId = GetHospitalId();

        var appointment =
            await _context.Appointments
                .FirstOrDefaultAsync(x =>
                    x.Id == appointmentId &&
                    x.HospitalId == hospitalId);

        if (appointment == null)
        {
            return NotFound("Appointment not found");
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("File required");
        }

        if (file.Length > MaxDocumentSizeBytes)
        {
            return BadRequest("File must be 10 MB or smaller");
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedDocumentExtensions.Contains(extension))
        {
            return BadRequest(
                "Allowed file types: PDF, JPG, JPEG, PNG, DOC, DOCX");
        }

        var folder = Path.Combine("wwwroot", "documents");

        Directory.CreateDirectory(folder);

        var uniqueName = Guid.NewGuid() + extension.ToLowerInvariant();

        var filePath = Path.Combine(folder, uniqueName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var document =
            new AppointmentDocument
            {
                AppointmentId = appointmentId,

                FileName = Path.GetFileName(file.FileName),

                FilePath = "/documents/" + uniqueName,

                HospitalId = hospitalId
            };

        _context.AppointmentDocuments.Add(document);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Document uploaded"
        });
    }

    [HttpGet("{appointmentId:int}/documents")]
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