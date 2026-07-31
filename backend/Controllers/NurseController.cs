using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Nurse,Admin")]
public class NurseController : ControllerBase
{
    private readonly AppDbContext _context;

    public NurseController(AppDbContext context)
    {
        _context = context;
    }

    private int HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;
    private int? BranchId => int.TryParse(User.FindFirstValue("BranchId"), out var id) ? id : null;
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool IsNurse => User.IsInRole("Nurse");

    // =====================================================
    // NURSE DASHBOARD
    // =====================================================
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? date)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var day = (date ?? DateTime.Today).Date;
        var query = ScopedAppointments().Where(x => x.Date.Date == day);

        var total = await query.CountAsync();
        var waiting = await query.CountAsync(x => x.Status == "Waiting" || x.Status == "Confirmed");
        var inProgress = await query.CountAsync(x => x.Status == "InProgress");
        var completed = await query.CountAsync(x => x.Status == "Completed" || x.Status == "PrescriptionAdded");
        var pendingVitals = await query.CountAsync(x => !_context.PatientVitals.Any(v => v.AppointmentId == x.Id));

        var recentPatients = await query
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .OrderBy(x => x.StartTime)
            .Take(10)
            .Select(x => new
            {
                appointmentId = x.Id,
                x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                doctorName = x.Doctor.Name,
                x.StartTime,
                x.Status,
                hasVitals = _context.PatientVitals.Any(v => v.AppointmentId == x.Id)
            })
            .ToListAsync();

        return Ok(new
        {
            date = day,
            hospitalId = HospitalId,
            branchId = IsNurse ? BranchId : null,
            totalAppointments = total,
            waitingPatients = waiting,
            inProgressPatients = inProgress,
            completedPatients = completed,
            pendingVitals,
            recentPatients
        });
    }

    // =====================================================
    // BRANCH PATIENT QUEUE
    // =====================================================
    [HttpGet("patients")]
    public async Task<IActionResult> GetBranchPatients(
        [FromQuery] DateTime? date,
        [FromQuery] string? search,
        [FromQuery] string? status)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var day = (date ?? DateTime.Today).Date;
        var query = ScopedAppointments()
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Branch)
            .Where(x => x.Date.Date == day);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term));
        }

        var data = await query
            .OrderBy(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                phone = x.Patient.Phone,
                doctorName = x.Doctor.Name,
                x.BranchId,
                branchName = x.Branch != null ? x.Branch.Name : null,
                x.Date,
                x.StartTime,
                x.Status,
                x.ChiefComplaints,
                hasVitals = _context.PatientVitals.Any(v => v.AppointmentId == x.Id)
            })
            .ToListAsync();

        return Ok(data);
    }

    // =====================================================
    // PATIENT DETAILS - ONLY WHEN PATIENT BELONGS TO BRANCH
    // =====================================================
    [HttpGet("patients/{patientId:int}")]
    public async Task<IActionResult> GetPatientDetails(int patientId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        if (!await CanAccessPatient(patientId))
            return NotFound(new { message = "Patient was not found in the nurse's branch" });

        var patient = await _context.Patients
            .AsNoTracking()
            .Where(x => x.Id == patientId && x.HospitalId == HospitalId)
            .Select(x => new
            {
                x.Id,
                x.PatientCode,
                x.Name,
                x.Phone,
                x.Age,
                x.Gender,
                x.Email,
                x.Address,
                x.BloodGroup,
                x.DateOfBirth,
                x.EmergencyContactName,
                x.EmergencyContactPhone,
                appointments = x.Appointments
                    .Where(a => !IsNurse || a.BranchId == BranchId)
                    .OrderByDescending(a => a.Date)
                    .Take(10)
                    .Select(a => new
                    {
                        a.Id,
                        a.Date,
                        a.StartTime,
                        a.Status,
                        a.DoctorId,
                        doctorName = a.Doctor.Name,
                        a.BranchId,
                        a.ChiefComplaints
                    }),
                vitals = _context.PatientVitals
                    .Where(v => v.PatientId == x.Id && v.HospitalId == HospitalId &&
                                (!IsNurse || v.Appointment.BranchId == BranchId))
                    .OrderByDescending(v => v.CreatedAt)
                    .Take(10)
                    .Select(v => new
                    {
                        v.Id,
                        v.AppointmentId,
                        v.Symptoms,
                        v.BloodPressure,
                        v.SugarLevel,
                        v.Temperature,
                        v.Weight,
                        v.PulseRate,
                        v.RespiratoryRate,
                        v.CreatedAt
                    })
            })
            .FirstOrDefaultAsync();

        return patient == null
            ? NotFound(new { message = "Patient not found" })
            : Ok(patient);
    }

    // =====================================================
    // UPDATE ALLOWED PATIENT DEMOGRAPHIC/CONTACT FIELDS
    // =====================================================
    [HttpPut("patients/{patientId:int}")]
    public async Task<IActionResult> UpdatePatient(int patientId, UpdateNursePatientDto dto)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        if (!await CanAccessPatient(patientId))
            return NotFound(new { message = "Patient was not found in the nurse's branch" });

        var patient = await _context.Patients.FirstOrDefaultAsync(x =>
            x.Id == patientId && x.HospitalId == HospitalId);

        if (patient == null) return NotFound(new { message = "Patient not found" });

        patient.Name = dto.Name.Trim();
        patient.Phone = dto.Phone.Trim();
        if (dto.Age.HasValue) patient.Age = dto.Age.Value;
        if (!string.IsNullOrWhiteSpace(dto.Gender)) patient.Gender = dto.Gender.Trim();
        patient.Email = dto.Email?.Trim();
        patient.Address = dto.Address?.Trim();
        patient.BloodGroup = dto.BloodGroup?.Trim();
        patient.DateOfBirth = dto.DateOfBirth;
        patient.EmergencyContactName = dto.EmergencyContactName?.Trim();
        patient.EmergencyContactPhone = dto.EmergencyContactPhone?.Trim();

        AddAudit("Update Patient", $"Nurse updated allowed patient fields for patient {patientId}",
            IsNurse ? BranchId : null);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Patient details updated successfully", patientId });
    }

    // =====================================================
    // CREATE OR UPDATE VITALS FOR APPOINTMENT
    // =====================================================
    [HttpPut("appointments/{appointmentId:int}/vitals")]
    public async Task<IActionResult> RecordVitals(int appointmentId, UpsertPatientVitalsDto dto)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var appointment = await ScopedAppointments()
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == appointmentId);

        if (appointment == null)
            return NotFound(new { message = "Appointment not found in the nurse's branch" });

        var vitals = await _context.PatientVitals
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);

        if (vitals == null)
        {
            vitals = new PatientVitals
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                HospitalId = HospitalId
            };
            _context.PatientVitals.Add(vitals);
        }

        vitals.Symptoms = dto.Symptoms?.Trim() ?? string.Empty;
        vitals.BloodPressure = dto.BloodPressure?.Trim() ?? string.Empty;
        vitals.SugarLevel = dto.SugarLevel?.Trim() ?? string.Empty;
        vitals.Temperature = dto.Temperature?.Trim() ?? string.Empty;
        vitals.Weight = dto.Weight?.Trim() ?? string.Empty;
        vitals.PulseRate = dto.PulseRate?.Trim() ?? string.Empty;
        vitals.RespiratoryRate = dto.RespiratoryRate?.Trim() ?? string.Empty;
        vitals.CreatedAt = DateTime.UtcNow;

        // Keep the appointment snapshot synchronized for doctor/receptionist views.
        appointment.BloodPressure = dto.BloodPressure;
        appointment.SugarLevel = dto.SugarLevel;
        appointment.Temperature = dto.Temperature;
        appointment.Weight = dto.Weight;
        appointment.PulseRate = dto.PulseRate;
        appointment.RespiratoryRate = dto.RespiratoryRate;
        appointment.ChiefComplaints = dto.Symptoms ?? appointment.ChiefComplaints;

        AddAudit("Record Vitals", $"Vitals recorded for appointment {appointmentId}", appointment.BranchId);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Vitals recorded successfully", vitals });
    }

    [HttpGet("patients/{patientId:int}/vitals")]
    public async Task<IActionResult> GetVitalsHistory(int patientId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        if (!await CanAccessPatient(patientId))
            return NotFound(new { message = "Patient was not found in the nurse's branch" });

        var data = await _context.PatientVitals
            .AsNoTracking()
            .Include(x => x.Appointment)
            .Where(x => x.PatientId == patientId &&
                        x.HospitalId == HospitalId &&
                        (!IsNurse || x.Appointment.BranchId == BranchId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.Symptoms,
                x.BloodPressure,
                x.SugarLevel,
                x.Temperature,
                x.Weight,
                x.PulseRate,
                x.RespiratoryRate,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(data);
    }

    // =====================================================
    // PRESCRIPTION PRINT QUEUE FOR NURSE'S BRANCH
    // =====================================================
    [HttpGet("print-queue")]
    public async Task<IActionResult> GetPrintQueue()
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var query = _context.Prescriptions
            .AsNoTracking()
            .Include(x => x.Appointment).ThenInclude(x => x.Patient)
            .Include(x => x.Appointment).ThenInclude(x => x.Doctor)
            .Include(x => x.Medicines)
            .Include(x => x.LabTests)
            .Where(x => x.HospitalId == HospitalId && x.Status == "Completed");

        if (IsNurse)
            query = query.Where(x => x.Appointment.BranchId == BranchId);

        var data = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                prescriptionId = x.Id,
                appointmentId = x.AppointmentId,
                patientName = x.Appointment.Patient.Name,
                doctorName = x.Appointment.Doctor.Name,
                x.Diagnosis,
                x.Instructions,
                x.FollowUpDate,
                x.IsPrinted,
                x.PrintedAt,
                medicines = x.Medicines.Select(m => new
                {
                    m.MedicineName,
                    m.Dosage,
                    m.Frequency,
                    m.Duration,
                    m.Notes
                }),
                labTests = x.LabTests.Select(t => new
                {
                    t.Id,
                    t.TestName,
                    t.Instructions,
                    t.Priority,
                    t.Status
                })
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPatch("prescriptions/{prescriptionId:int}/printed")]
    public async Task<IActionResult> MarkPrescriptionPrinted(int prescriptionId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null) return scopeError;

        var prescription = await _context.Prescriptions
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.Id == prescriptionId && x.HospitalId == HospitalId);

        if (prescription == null || (IsNurse && prescription.Appointment?.BranchId != BranchId))
            return NotFound(new { message = "Prescription not found in the nurse's branch" });

        prescription.IsPrinted = true;
        prescription.PrintedAt = DateTime.UtcNow;
        prescription.PrintedByUserId = UserId;
        AddAudit("Print Prescription", $"Prescription {prescriptionId} marked as printed", prescription.Appointment?.BranchId);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Prescription marked as printed", prescriptionId, prescription.PrintedAt });
    }

    private IQueryable<Appointment> ScopedAppointments()
    {
        var query = _context.Appointments.Where(x => x.HospitalId == HospitalId);
        if (IsNurse)
            query = query.Where(x => x.BranchId == BranchId);
        return query;
    }

    private async Task<bool> CanAccessPatient(int patientId)
    {
        return await ScopedAppointments().AnyAsync(x => x.PatientId == patientId);
    }

    private IActionResult? ValidateScope()
    {
        if (HospitalId <= 0)
            return Unauthorized(new { message = "Hospital information is missing from token" });

        if (IsNurse && !BranchId.HasValue)
            return Forbid();

        return null;
    }

    private void AddAudit(string action, string systemAction, int? branchId)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = UserId,
            UserName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Nurse",
            Role = User.FindFirstValue(ClaimTypes.Role) ?? "Nurse",
            ClinicId = HospitalId,
            BranchId = branchId,
            Action = action,
            SystemAction = systemAction,
            Timestamp = DateTime.UtcNow,
            LoginTime = DateTime.UtcNow
        });
    }
}
