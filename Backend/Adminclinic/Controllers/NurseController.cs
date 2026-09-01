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

    // =====================================================
    // TOKEN HELPERS
    // =====================================================

    private int HospitalId =>
        int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id)
            ? id
            : 0;

    private int? BranchId =>
        int.TryParse(
            User.FindFirstValue("BranchId"),
            out var id)
            ? id
            : null;

    private int UserId =>
        int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : 0;

    private bool IsNurse =>
        User.IsInRole("Nurse");

    // =====================================================
    // VALIDATE TOKEN SCOPE
    // =====================================================

    private IActionResult? ValidateScope()
    {
        if (HospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (IsNurse &&
            (!BranchId.HasValue ||
             BranchId.Value <= 0))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "Branch information is missing from nurse token."
                });
        }

        return null;
    }

    // =====================================================
    // SCOPED APPOINTMENTS
    //
    // Nurse:
    // Hospital + assigned Branch only
    //
    // Admin:
    // Entire Hospital
    //
    // Both online/offline appointments are included
    // because there is no booking-source filter here.
    // =====================================================

    private IQueryable<Appointment> ScopedAppointments()
    {
        var query =
            _context.Appointments
                .Where(x =>
                    x.HospitalId == HospitalId);

        if (IsNurse)
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        return query;
    }

    // =====================================================
    // NURSE DASHBOARD
    //
    // GET /api/Nurse/dashboard
    // GET /api/Nurse/dashboard?date=2026-08-10
    //
    // Same main appointment information as Receptionist
    // + Nurse-specific vital information
    // =====================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? date = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var day =
            (date ?? DateTime.Today).Date;

        var query =
            ScopedAppointments()
                .Where(x =>
                    x.Date.Date == day);

        // =================================================
        // APPOINTMENT COUNTS
        // =================================================

        var totalToday =
            await query.CountAsync();

        var waiting =
            await query.CountAsync(x =>
                x.Status == "Waiting");

        var confirmed =
            await query.CountAsync(x =>
                x.Status == "Confirmed");

        var inProgress =
            await query.CountAsync(x =>
                x.Status == "InProgress");

        var completed =
            await query.CountAsync(x =>
                x.Status == "Completed" ||
                x.Status == "PrescriptionAdded");

        // =================================================
        // NURSE VITAL COUNTS
        // =================================================

        var pendingVitals =
            await query.CountAsync(x =>
                !_context.PatientVitals.Any(v =>
                    v.AppointmentId == x.Id));

        var vitalsCompleted =
            await query.CountAsync(x =>
                _context.PatientVitals.Any(v =>
                    v.AppointmentId == x.Id));

        // =================================================
        // TODAY APPOINTMENT/PATIENT DETAILS
        //
        // Includes both online and offline appointments.
        // =================================================

        var appointments =
            await query
                .AsNoTracking()
                .Include(x => x.Patient)
                .Include(x => x.Doctor)
                .Include(x => x.Branch)
                .OrderBy(x => x.StartTime)
                .Select(x => new
                {
                    // =====================================
                    // APPOINTMENT
                    // =====================================

                    appointmentId =
                        x.Id,

                    appointmentNumber =
                        x.TokenNumber,

                    tokenNumber =
                        x.TokenNumber,

                    appointmentDate =
                        x.Date,

                    startTime =
                        x.StartTime,

                    status =
                        x.Status,

                    paymentStatus =
                        x.PaymentStatus,

                    // =====================================
                    // PATIENT
                    // =====================================

                    patientId =
                        x.PatientId,

                    patientName =
                        x.Patient.Name,

                    patientCode =
                        x.Patient.PatientCode,

                    patientPhone =
                        x.Patient.Phone,

                    patientAge =
                        x.Patient.Age,

                    patientGender =
                        x.Patient.Gender,

                    patientEmail =
                        x.Patient.Email,

                    patientAddress =
                        x.Patient.Address,

                    bloodGroup =
                        x.Patient.BloodGroup,

                    dateOfBirth =
                        x.Patient.DateOfBirth,

                    emergencyContactName =
                        x.Patient.EmergencyContactName,

                    emergencyContactPhone =
                        x.Patient.EmergencyContactPhone,

                    // =====================================
                    // DOCTOR
                    // =====================================

                    doctorId =
                        x.DoctorId,

                    doctorName =
                        x.Doctor.Name,

                    consultationFee =
                        x.Doctor.Fees,

                    // =====================================
                    // BRANCH
                    // =====================================

                    hospitalId =
                        x.HospitalId,

                    branchId =
                        x.BranchId,

                    branchName =
                        x.Branch != null
                            ? x.Branch.Name
                            : null,

                    // =====================================
                    // COMPLAINTS + VITALS
                    // =====================================

                    chiefComplaints =
                        x.ChiefComplaints,

                    bloodPressure =
                        x.BloodPressure,

                    sugarLevel =
                        x.SugarLevel,

                    temperature =
                        x.Temperature,

                    weight =
                        x.Weight,

                    pulseRate =
                        x.PulseRate,

                    respiratoryRate =
                        x.RespiratoryRate,

                    // =====================================
                    // VITAL STATUS
                    // =====================================

                    hasVitals =
                        _context.PatientVitals.Any(v =>
                            v.AppointmentId == x.Id)
                })
                .ToListAsync();

        return Ok(new
        {
            date =
                day.ToString("yyyy-MM-dd"),

            hospitalId =
                HospitalId,

            branchId =
                IsNurse
                    ? BranchId
                    : null,

            // Same style as Receptionist
            totalTodayAppointments =
                totalToday,

            waitingAppointments =
                waiting,

            confirmedAppointments =
                confirmed,

            inProgressAppointments =
                inProgress,

            completedAppointments =
                completed,

            // Nurse-specific
            pendingVitals =
                pendingVitals,

            vitalsCompleted =
                vitalsCompleted,

            appointments =
                appointments
        });
    }

    // =====================================================
    // NURSE PATIENT / APPOINTMENT LIST
    //
    // Includes BOTH online + offline appointments
    //
    // GET /api/Nurse/patients
    // GET /api/Nurse/patients?date=2026-08-10
    // GET /api/Nurse/patients?status=Waiting
    // GET /api/Nurse/patients?search=Ravi
    // =====================================================

    [HttpGet("patients")]
    public async Task<IActionResult> GetBranchPatients(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var day =
            (date ?? DateTime.Today).Date;

        var query =
            ScopedAppointments()
                .AsNoTracking()
                .Include(x => x.Patient)
                .Include(x => x.Doctor)
                .Include(x => x.Branch)
                .Where(x =>
                    x.Date.Date == day);

        // =================================================
        // STATUS FILTER
        // =================================================

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim();

            query = query.Where(x =>
                x.Status ==
                normalizedStatus);
        }

        // =================================================
        // SEARCH
        // =================================================

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term =
                search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var appointments =
            await query
                .OrderBy(x => x.StartTime)
                .Select(x => new
                {
                    // =====================================
                    // APPOINTMENT DETAILS
                    // =====================================

                    appointmentId =
                        x.Id,

                    appointmentNumber =
                        x.TokenNumber,

                    tokenNumber =
                        x.TokenNumber,

                    date =
                        x.Date,

                    startTime =
                        x.StartTime,

                    status =
                        x.Status,

                    paymentStatus =
                        x.PaymentStatus,

                    // =====================================
                    // PATIENT DETAILS
                    // =====================================

                    patientId =
                        x.PatientId,

                    patientName =
                        x.Patient.Name,

                    patientCode =
                        x.Patient.PatientCode,

                    patientPhone =
                        x.Patient.Phone,

                    patientAge =
                        x.Patient.Age,

                    patientGender =
                        x.Patient.Gender,

                    patientEmail =
                        x.Patient.Email,

                    patientAddress =
                        x.Patient.Address,

                    bloodGroup =
                        x.Patient.BloodGroup,

                    dateOfBirth =
                        x.Patient.DateOfBirth,

                    emergencyContactName =
                        x.Patient.EmergencyContactName,

                    emergencyContactPhone =
                        x.Patient.EmergencyContactPhone,

                    // =====================================
                    // DOCTOR DETAILS
                    // =====================================

                    doctorId =
                        x.DoctorId,

                    doctorName =
                        x.Doctor.Name,

                    consultationFee =
                        x.Doctor.Fees,

                    // =====================================
                    // HOSPITAL + BRANCH
                    // =====================================

                    hospitalId =
                        x.HospitalId,

                    branchId =
                        x.BranchId,

                    branchName =
                        x.Branch != null
                            ? x.Branch.Name
                            : null,

                    // =====================================
                    // COMPLAINTS
                    // =====================================

                    chiefComplaints =
                        x.ChiefComplaints,

                    // =====================================
                    // VITALS
                    // =====================================

                    bloodPressure =
                        x.BloodPressure,

                    sugarLevel =
                        x.SugarLevel,

                    temperature =
                        x.Temperature,

                    weight =
                        x.Weight,

                    pulseRate =
                        x.PulseRate,

                    respiratoryRate =
                        x.RespiratoryRate,

                    hasVitals =
                        _context.PatientVitals.Any(v =>
                            v.AppointmentId == x.Id)
                })
                .ToListAsync();

        return Ok(appointments);
    }

    // =====================================================
    // GET INDIVIDUAL PATIENT DETAILS
    //
    // GET /api/Nurse/patients/{patientId}
    // =====================================================

    [HttpGet("patients/{patientId:int}")]
    public async Task<IActionResult> GetPatientDetails(
        int patientId)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        if (!await CanAccessPatient(patientId))
        {
            return NotFound(new
            {
                message =
                    "Patient was not found in the nurse's branch."
            });
        }

        var patient =
            await _context.Patients
                .AsNoTracking()
                .Where(x =>
                    x.Id == patientId &&
                    x.HospitalId == HospitalId)
                .Select(x => new
                {
                    patientId =
                        x.Id,

                    patientCode =
                        x.PatientCode,

                    patientName =
                        x.Name,

                    phone =
                        x.Phone,

                    age =
                        x.Age,

                    gender =
                        x.Gender,

                    email =
                        x.Email,

                    address =
                        x.Address,

                    bloodGroup =
                        x.BloodGroup,

                    dateOfBirth =
                        x.DateOfBirth,

                    emergencyContactName =
                        x.EmergencyContactName,

                    emergencyContactPhone =
                        x.EmergencyContactPhone,

                    // =====================================
                    // APPOINTMENT HISTORY
                    // =====================================

                    appointments =
                        x.Appointments
                            .Where(a =>
                                !IsNurse ||
                                a.BranchId ==
                                BranchId)
                            .OrderByDescending(
                                a => a.Date)
                            .Take(20)
                            .Select(a => new
                            {
                                appointmentId =
                                    a.Id,

                                tokenNumber =
                                    a.TokenNumber,

                                date =
                                    a.Date,

                                startTime =
                                    a.StartTime,

                                status =
                                    a.Status,

                                paymentStatus =
                                    a.PaymentStatus,

                                doctorId =
                                    a.DoctorId,

                                doctorName =
                                    a.Doctor.Name,

                                branchId =
                                    a.BranchId,

                                chiefComplaints =
                                    a.ChiefComplaints,

                                bloodPressure =
                                    a.BloodPressure,

                                sugarLevel =
                                    a.SugarLevel,

                                temperature =
                                    a.Temperature,

                                weight =
                                    a.Weight,

                                pulseRate =
                                    a.PulseRate,

                                respiratoryRate =
                                    a.RespiratoryRate
                            }),

                    // =====================================
                    // VITAL HISTORY
                    // =====================================

                    vitals =
                        _context.PatientVitals
                            .Where(v =>
                                v.PatientId ==
                                x.Id &&

                                v.HospitalId ==
                                HospitalId &&

                                (!IsNurse ||
                                 v.Appointment.BranchId ==
                                 BranchId))
                            .OrderByDescending(
                                v => v.CreatedAt)
                            .Take(20)
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

        if (patient == null)
        {
            return NotFound(new
            {
                message =
                    "Patient not found."
            });
        }

        return Ok(patient);
    }

    // =====================================================
    // UPDATE PATIENT DETAILS
    //
    // PUT /api/Nurse/patients/{patientId}
    // =====================================================

    [HttpPut("patients/{patientId:int}")]
    public async Task<IActionResult> UpdatePatient(
        int patientId,
        [FromBody] UpdateNursePatientDto dto)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        if (!await CanAccessPatient(patientId))
        {
            return NotFound(new
            {
                message =
                    "Patient was not found in the nurse's branch."
            });
        }

        var patient =
            await _context.Patients
                .FirstOrDefaultAsync(x =>
                    x.Id == patientId &&
                    x.HospitalId == HospitalId);

        if (patient == null)
        {
            return NotFound(new
            {
                message =
                    "Patient not found."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new
            {
                message =
                    "Patient name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Phone))
        {
            return BadRequest(new
            {
                message =
                    "Patient phone number is required."
            });
        }

        patient.Name =
            dto.Name.Trim();

        patient.Phone =
            dto.Phone.Trim();

        if (dto.Age.HasValue)
        {
            patient.Age =
                dto.Age.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.Gender))
        {
            patient.Gender =
                dto.Gender.Trim();
        }

        patient.Email =
            dto.Email?.Trim();

        patient.Address =
            dto.Address?.Trim();

        patient.BloodGroup =
            dto.BloodGroup?.Trim();

        patient.DateOfBirth =
            dto.DateOfBirth;

        patient.EmergencyContactName =
            dto.EmergencyContactName?.Trim();

        patient.EmergencyContactPhone =
            dto.EmergencyContactPhone?.Trim();

        AddAudit(
            "Update Patient",
            $"Nurse updated allowed patient fields for patient {patientId}",
            IsNurse
                ? BranchId
                : null);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Patient details updated successfully.",

            patientId
        });
    }

    // =====================================================
    // CREATE OR UPDATE VITALS
    //
    // PUT /api/Nurse/appointments/{appointmentId}/vitals
    // =====================================================

    [HttpPut(
        "appointments/{appointmentId:int}/vitals")]
    public async Task<IActionResult> RecordVitals(
        int appointmentId,
        [FromBody] UpsertPatientVitalsDto dto)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var appointment =
            await ScopedAppointments()
                .Include(x => x.Patient)
                .FirstOrDefaultAsync(x =>
                    x.Id == appointmentId);

        if (appointment == null)
        {
            return NotFound(new
            {
                message =
                    "Appointment not found in the nurse's branch."
            });
        }

        var vitals =
            await _context.PatientVitals
                .FirstOrDefaultAsync(x =>
                    x.AppointmentId ==
                    appointmentId);

        // =================================================
        // CREATE
        // =================================================

        if (vitals == null)
        {
            vitals =
                new PatientVitals
                {
                    AppointmentId =
                        appointment.Id,

                    PatientId =
                        appointment.PatientId,

                    HospitalId =
                        HospitalId,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.PatientVitals.Add(vitals);
        }

        // =================================================
        // UPDATE VITAL DATA
        // =================================================

        vitals.Symptoms =
            dto.Symptoms?.Trim()
            ?? string.Empty;

        vitals.BloodPressure =
            dto.BloodPressure?.Trim()
            ?? string.Empty;

        vitals.SugarLevel =
            dto.SugarLevel?.Trim()
            ?? string.Empty;

        vitals.Temperature =
            dto.Temperature?.Trim()
            ?? string.Empty;

        vitals.Weight =
            dto.Weight?.Trim()
            ?? string.Empty;

        vitals.PulseRate =
            dto.PulseRate?.Trim()
            ?? string.Empty;

        vitals.RespiratoryRate =
            dto.RespiratoryRate?.Trim()
            ?? string.Empty;

        vitals.CreatedAt =
            DateTime.UtcNow;

        // =================================================
        // SYNC APPOINTMENT SNAPSHOT
        // =================================================

        appointment.BloodPressure =
            dto.BloodPressure;

        appointment.SugarLevel =
            dto.SugarLevel;

        appointment.Temperature =
            dto.Temperature;

        appointment.Weight =
            dto.Weight;

        appointment.PulseRate =
            dto.PulseRate;

        appointment.RespiratoryRate =
            dto.RespiratoryRate;

        appointment.ChiefComplaints =
            dto.Symptoms ??
            appointment.ChiefComplaints;

        AddAudit(
            "Record Vitals",
            $"Vitals recorded for appointment {appointmentId}",
            appointment.BranchId);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Vitals recorded successfully.",

            appointmentId,

            patientId =
                appointment.PatientId,

            vitals
        });
    }

    // =====================================================
    // GET PATIENT VITAL HISTORY
    //
    // GET /api/Nurse/patients/{patientId}/vitals
    // =====================================================

    [HttpGet("patients/{patientId:int}/vitals")]
    public async Task<IActionResult> GetVitalsHistory(
        int patientId)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        if (!await CanAccessPatient(patientId))
        {
            return NotFound(new
            {
                message =
                    "Patient was not found in the nurse's branch."
            });
        }

        var data =
            await _context.PatientVitals
                .AsNoTracking()
                .Include(x => x.Appointment)
                .Where(x =>

                    x.PatientId ==
                    patientId &&

                    x.HospitalId ==
                    HospitalId &&

                    (!IsNurse ||
                     x.Appointment.BranchId ==
                     BranchId))
                .OrderByDescending(
                    x => x.CreatedAt)
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
    // PRESCRIPTION PRINT QUEUE
    //
    // GET /api/Nurse/print-queue
    // =====================================================

    [HttpGet("print-queue")]
    public async Task<IActionResult> GetPrintQueue()
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var query =
            _context.Prescriptions
                .AsNoTracking()
                .Include(x => x.Appointment)
                    .ThenInclude(x =>
                        x.Patient)
                .Include(x => x.Appointment)
                    .ThenInclude(x =>
                        x.Doctor)
                .Include(x =>
                    x.Medicines)
                .Include(x =>
                    x.LabTests)
                .Where(x =>
                    x.HospitalId ==
                    HospitalId &&

                    x.Status ==
                    "Completed");

        if (IsNurse)
        {
            query = query.Where(x =>
                x.Appointment.BranchId ==
                BranchId);
        }

        var data =
            await query
                .OrderByDescending(
                    x => x.CreatedAt)
                .Select(x => new
                {
                    prescriptionId =
                        x.Id,

                    appointmentId =
                        x.AppointmentId,

                    patientId =
                        x.Appointment.PatientId,

                    patientName =
                        x.Appointment.Patient.Name,

                    patientCode =
                        x.Appointment.Patient.PatientCode,

                    doctorId =
                        x.Appointment.DoctorId,

                    doctorName =
                        x.Appointment.Doctor.Name,

                    branchId =
                        x.Appointment.BranchId,

                    appointmentDate =
                        x.Appointment.Date,

                    startTime =
                        x.Appointment.StartTime,

                    tokenNumber =
                        x.Appointment.TokenNumber,

                    x.Diagnosis,

                    x.Instructions,

                    x.FollowUpDate,

                    x.IsPrinted,

                    x.PrintedAt,

                    medicines =
                        x.Medicines
                            .Select(m => new
                            {
                                m.MedicineName,

                                m.Dosage,

                                m.Frequency,

                                m.Duration,

                                m.Notes
                            }),

                    labTests =
                        x.LabTests
                            .Select(t => new
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

    // =====================================================
    // MARK PRESCRIPTION PRINTED
    //
    // PATCH /api/Nurse/prescriptions/{id}/printed
    // =====================================================

    [HttpPatch(
        "prescriptions/{prescriptionId:int}/printed")]
    public async Task<IActionResult>
        MarkPrescriptionPrinted(
            int prescriptionId)
    {
        var scopeError =
            ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var prescription =
            await _context.Prescriptions
                .Include(x =>
                    x.Appointment)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    prescriptionId &&

                    x.HospitalId ==
                    HospitalId);

        if (prescription == null)
        {
            return NotFound(new
            {
                message =
                    "Prescription not found."
            });
        }

        if (IsNurse &&
            prescription.Appointment?.BranchId !=
            BranchId)
        {
            return NotFound(new
            {
                message =
                    "Prescription not found in the nurse's branch."
            });
        }

        prescription.IsPrinted =
            true;

        prescription.PrintedAt =
            DateTime.UtcNow;

        prescription.PrintedByUserId =
            UserId;

        AddAudit(
            "Print Prescription",
            $"Prescription {prescriptionId} marked as printed",
            prescription.Appointment?.BranchId);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Prescription marked as printed.",

            prescriptionId,

            prescription.PrintedAt
        });
    }

    // =====================================================
    // CHECK PATIENT ACCESS
    // =====================================================

    private async Task<bool> CanAccessPatient(
        int patientId)
    {
        return await ScopedAppointments()
            .AnyAsync(x =>
                x.PatientId ==
                patientId);
    }

    // =====================================================
    // AUDIT LOG
    // =====================================================

    private void AddAudit(
        string action,
        string systemAction,
        int? branchId)
    {
        _context.AuditLogs.Add(
            new AuditLog
            {
                UserId =
                    UserId,

                UserName =
                    User.Identity?.Name
                    ?? User.FindFirstValue(
                        ClaimTypes.Email)
                    ?? "Nurse",

                Role =
                    User.FindFirstValue(
                        ClaimTypes.Role)
                    ?? "Nurse",

                ClinicId =
                    HospitalId,

                BranchId =
                    branchId,

                Action =
                    action,

                SystemAction =
                    systemAction,

                Timestamp =
                    DateTime.UtcNow,

                LoginTime =
                    DateTime.UtcNow
            });
    }
}