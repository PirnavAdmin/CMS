using AuthDemo.Authorization;
using AuthDemo.Helpers;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission("Appointments")]
public class ConsultationController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConsultationController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET HOSPITAL ID
    // =====================================================
    private int GetHospitalId()
    {
        var claim = User.Claims.FirstOrDefault(x => x.Type == "HospitalId");

        if (claim == null)
        {
            return 0;
        }

        return int.TryParse(claim.Value, out var hospitalId)
            ? hospitalId
            : 0;
    }

    // =====================================================
    // GET DOCTOR ID
    // =====================================================
    private int GetDoctorId()
    {
        var claim = User.Claims.FirstOrDefault(x => x.Type == "DoctorId");

        if (claim == null)
        {
            return 0;
        }

        return int.TryParse(claim.Value, out var doctorId)
            ? doctorId
            : 0;
    }

    // =====================================================
    // CREATE / UPDATE CONSULTATION
    // ALSO CREATES LAB ORDERS FOR SELECTED DIAGNOSTIC TESTS
    // =====================================================
    [Authorize(Roles = "Doctor")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateConsultationDto dto)
    {
        var hospitalId = GetHospitalId();
        var doctorId = GetDoctorId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (doctorId <= 0)
        {
            return Unauthorized(new
            {
                message = "Doctor information is missing from token."
            });
        }

        if (dto.AppointmentId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid AppointmentId is required."
            });
        }

        if (dto.PatientId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid PatientId is required."
            });
        }

        // =================================================
        // CHECK APPOINTMENT
        // =================================================
        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(x =>
                x.Id == dto.AppointmentId &&
                x.DoctorId == doctorId &&
                x.HospitalId == hospitalId);

        if (appointment == null)
        {
            return NotFound(new
            {
                message = "Appointment not found"
            });
        }

        if (appointment.PatientId != dto.PatientId)
        {
            return BadRequest(new
            {
                message = "PatientId does not match the selected appointment."
            });
        }

        // =================================================
        // CHECK APPOINTMENT DATE & TIME
        // =================================================
        var appointmentDateTime =
            appointment.Date.Date.Add(appointment.StartTime);

        if (DateTime.Now < appointmentDateTime)
        {
            return BadRequest(new
            {
                message = "Consultation can be started only on the scheduled appointment date and time."
            });
        }

        // =================================================
        // VALIDATE DIAGNOSTIC TESTS BEFORE SAVING
        // =================================================
        var selectedDiagnosticTests = (dto.DiagnosisTests ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedLabTests = new List<LabTestMaster>();

        foreach (var selectedTest in selectedDiagnosticTests)
        {
            var normalizedTest = selectedTest.ToLower();

            var master = await _context.LabTestMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.HospitalId == hospitalId &&
                    x.IsActive &&
                    (x.BranchId == null || x.BranchId == appointment.BranchId) &&
                    (
                        x.TestName.ToLower() == normalizedTest ||
                        x.TestCode.ToLower() == normalizedTest
                    ));

            if (master == null)
            {
                return BadRequest(new
                {
                    message = $"Diagnostic test '{selectedTest}' was not found in Lab Test Master or is inactive."
                });
            }

            if (master.Price <= 0)
            {
                return BadRequest(new
                {
                    message = $"Diagnostic test '{master.TestName}' does not have a valid price in Lab Test Master."
                });
            }

            resolvedLabTests.Add(master);
        }

        // =================================================
        // SAVE CLINICAL NOTE TEMPLATE
        // =================================================
        if (!string.IsNullOrWhiteSpace(dto.ClinicalNotes))
        {
            var exists = await _context.ClinicalNoteTemplates
                .AnyAsync(x => x.Notes == dto.ClinicalNotes);

            if (!exists)
            {
                _context.ClinicalNoteTemplates.Add(
                    new ClinicalNoteTemplate
                    {
                        Name = dto.Diagnosis ?? "General",
                        Notes = dto.ClinicalNotes
                    });
            }
        }

        // =================================================
        // SAVE DIAGNOSIS DROPDOWN DATA
        // =================================================
        if (!string.IsNullOrWhiteSpace(dto.Diagnosis))
        {
            var diagnosisExists = await _context.DoctorDiagnoses
                .AnyAsync(x =>
                    x.Name == dto.Diagnosis &&
                    x.DoctorId == doctorId &&
                    x.HospitalId == hospitalId);

            if (!diagnosisExists)
            {
                _context.DoctorDiagnoses.Add(
                    new DoctorDiagnosis
                    {
                        Name = dto.Diagnosis,
                        DoctorId = doctorId,
                        HospitalId = hospitalId
                    });
            }
        }

        // =================================================
        // CHECK EXISTING CONSULTATION
        // =================================================
        var consultation = await _context.Consultations
            .FirstOrDefaultAsync(x =>
                x.AppointmentId == dto.AppointmentId &&
                x.HospitalId == hospitalId);

        // =================================================
        // UPDATE CONSULTATION
        // =================================================
        if (consultation != null)
        {
            consultation.Diagnosis = dto.Diagnosis;
            consultation.ClinicalNotes = dto.ClinicalNotes;
        }
        // =================================================
        // CREATE CONSULTATION
        // =================================================
        else
        {
            consultation = new Consultation
            {
                AppointmentId = dto.AppointmentId,
                PatientId = dto.PatientId,
                Diagnosis = dto.Diagnosis,
                ClinicalNotes = dto.ClinicalNotes,
                HospitalId = hospitalId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Consultations.Add(consultation);
        }

        // =================================================
        // CREATE LAB ORDERS FOR SELECTED DIAGNOSTIC TESTS
        // =================================================
        var labOrdersCreated = 0;

        foreach (var master in resolvedLabTests)
        {
            var alreadyExists = await _context.LabOrders
                .AnyAsync(x =>
                    x.AppointmentId == appointment.Id &&
                    x.HospitalId == hospitalId &&
                    x.TestCode == master.TestCode &&
                    x.Status != "Cancelled");

            if (alreadyExists)
            {
                continue;
            }

            _context.LabOrders.Add(
                new LabOrder
                {
                    AppointmentId = appointment.Id,
                    PatientId = appointment.PatientId,
                    DoctorId = appointment.DoctorId,
                    HospitalId = hospitalId,
                    BranchId = appointment.BranchId,
                    TestCode = master.TestCode,
                    TestName = master.TestName,
                    Category = master.Category,
                    SampleType = master.SampleType,
                    Price = master.Price,
                    Priority = "Routine",
                    Status = "Ordered",
                    OrderedAt = DateTime.UtcNow
                });

            labOrdersCreated++;
        }

        // =================================================
        // UPDATE APPOINTMENT STATUS
        // =================================================
        appointment.Status = "InProgress";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Consultation updated successfully",
            appointmentStatus = "InProgress",
            consultationId = consultation.Id,
            diagnosisTests = resolvedLabTests.Select(x => new
            {
                x.Id,
                x.TestCode,
                x.TestName,
                x.Category,
                x.SampleType,
                x.Price
            }).ToList(),
            labOrdersCreated
        });
    }

    // =====================================================
    // DIAGNOSIS DROPDOWN
    // =====================================================
    [Authorize(Roles = "Doctor")]
    [HttpGet("diagnosis-dropdown")]
    public async Task<IActionResult> DiagnosisDropdown()
    {
        var hospitalId = GetHospitalId();
        var doctorId = GetDoctorId();

        var defaults = new List<string>
        {
            "Allergic rhinitis",
            "Back pain",
            "Diabetes follow-up",
            "Fever",
            "Gastritis",
            "Hypertension",
            "Migraine",
            "Nerve sensitivity",
            "Psoriasis",
            "Upper respiratory infection"
        };

        var diagnosisData = await _context.DoctorDiagnoses
            .Where(x =>
                x.DoctorId == doctorId &&
                x.HospitalId == hospitalId)
            .Select(x => x.Name)
            .ToListAsync();

        var result = defaults
            .Union(diagnosisData)
            .OrderBy(x => x)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // GET RECEPTIONIST-RECORDED VITALS FOR DOCTOR CONSULTATION
    // =====================================================
    [Authorize(Roles = "Doctor")]
    [HttpGet("appointment/{appointmentId:int}/vitals")]
    public async Task<IActionResult> GetAppointmentVitals(int appointmentId)
    {
        var hospitalId = GetHospitalId();
        var doctorId = GetDoctorId();

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.HospitalId == hospitalId &&
                x.DoctorId == doctorId);

        if (appointment == null)
        {
            return NotFound(new
            {
                message = "Appointment not found or it is not assigned to this doctor"
            });
        }

        var savedVitals = await _context.PatientVitals
            .AsNoTracking()
            .Where(x =>
                x.AppointmentId == appointmentId &&
                x.HospitalId == hospitalId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName = appointment.Patient.Name,
            patientCode = appointment.Patient.PatientCode,
            chiefComplaints = appointment.ChiefComplaints,
            symptoms = savedVitals?.Symptoms ?? appointment.ChiefComplaints,
            bloodPressure = savedVitals?.BloodPressure ?? appointment.BloodPressure,
            sugarLevel = savedVitals?.SugarLevel ?? appointment.SugarLevel,
            temperature = savedVitals?.Temperature ?? appointment.Temperature,
            weight = savedVitals?.Weight ?? appointment.Weight,
            pulseRate = savedVitals?.PulseRate ?? appointment.PulseRate,
            respiratoryRate = savedVitals?.RespiratoryRate ?? appointment.RespiratoryRate,
            recordedAt = savedVitals?.CreatedAt,
            hasVitals = savedVitals != null ||
                        !string.IsNullOrWhiteSpace(appointment.BloodPressure) ||
                        !string.IsNullOrWhiteSpace(appointment.SugarLevel) ||
                        !string.IsNullOrWhiteSpace(appointment.Temperature) ||
                        !string.IsNullOrWhiteSpace(appointment.Weight) ||
                        !string.IsNullOrWhiteSpace(appointment.PulseRate) ||
                        !string.IsNullOrWhiteSpace(appointment.RespiratoryRate)
        });
    }

    // =====================================================
    // GET CONSULTATION BY APPOINTMENT
    // =====================================================
    [HttpGet("appointment/{appointmentId}")]
    public async Task<IActionResult> GetByAppointment(int appointmentId)
    {
        var hospitalId = GetHospitalId();

        var consultation = await _context.Consultations
            .Where(x =>
                x.AppointmentId == appointmentId &&
                x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.PatientId,
                x.Diagnosis,
                x.ClinicalNotes,
                diagnosisTests = _context.LabOrders
                    .Where(o =>
                        o.AppointmentId == x.AppointmentId &&
                        o.HospitalId == hospitalId &&
                        o.Status != "Cancelled")
                    .OrderBy(o => o.TestName)
                    .Select(o => o.TestName)
                    .ToList(),
                createdAt = x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (consultation == null)
        {
            return NotFound(new
            {
                message = "Consultation not found"
            });
        }

        return Ok(consultation);
    }

    // =====================================================
    // GET CONSULTATIONS
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var hospitalId = GetHospitalId();

        var role = User.Claims.FirstOrDefault(
            x => x.Type == "role")?.Value;

        var doctorId = GetDoctorId();

        var query = _context.Consultations
            .Where(x => x.HospitalId == hospitalId);

        // =================================================
        // DOCTOR FILTER
        // =================================================
        if (role == "Doctor")
        {
            query = query.Where(x =>
                x.Appointment.DoctorId == doctorId);
        }

        var consultations = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.PatientId,
                patientName = x.Patient.Name,
                diagnosis = x.Diagnosis,
                clinicalNotes = x.ClinicalNotes,
                appointmentStatus = x.Appointment.Status,
                createdAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(consultations);
    }

    // =====================================================
    // CLINICAL NOTE TEMPLATES
    // =====================================================
    [HttpGet("clinical-note-templates")]
    public async Task<IActionResult> GetTemplates()
    {
        return Ok(
            await _context.ClinicalNoteTemplates
                .OrderBy(x => x.Name)
                .ToListAsync());
    }
}
