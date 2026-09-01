using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicalHistoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public MedicalHistoryController(
        AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET HOSPITAL ID FROM TOKEN
    // =====================================================

    private int GetHospitalId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type == "HospitalId");

        if (claim == null)
        {
            return 0;
        }

        return int.TryParse(
            claim.Value,
            out var hospitalId)
                ? hospitalId
                : 0;
    }

    // =====================================================
    // GET BRANCH ID FROM TOKEN
    // =====================================================

    private int? GetBranchId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type == "BranchId");

        if (claim == null)
        {
            return null;
        }

        return int.TryParse(
            claim.Value,
            out var branchId)
                ? branchId
                : null;
    }

    // =====================================================
    // CHECK IF CURRENT USER IS NURSE
    // =====================================================

    private bool IsNurse()
    {
        return User.IsInRole("Nurse");
    }

    // =====================================================
    // VALIDATE NURSE BRANCH
    //
    // Nurse must have HospitalId + BranchId in JWT.
    // =====================================================

    private IActionResult? ValidateNurseScope()
    {
        if (!IsNurse())
        {
            return null;
        }

        var hospitalId =
            GetHospitalId();

        var branchId =
            GetBranchId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from nurse token."
            });
        }

        if (!branchId.HasValue ||
            branchId.Value <= 0)
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
    // CHECK WHETHER NURSE CAN ACCESS PATIENT
    //
    // Nurse can access only when patient has an appointment
    // in the nurse's assigned hospital + branch.
    // =====================================================

    private async Task<bool> NurseCanAccessPatient(
        int patientId)
    {
        if (!IsNurse())
        {
            return true;
        }

        var hospitalId =
            GetHospitalId();

        var branchId =
            GetBranchId();

        if (hospitalId <= 0 ||
            !branchId.HasValue ||
            branchId.Value <= 0)
        {
            return false;
        }

        return await _context.Appointments
            .AnyAsync(x =>
                x.PatientId == patientId &&
                x.HospitalId == hospitalId &&
                x.BranchId == branchId.Value);
    }

    // =====================================================
    // CREATE MEDICAL HISTORY
    //
    // Admin
    // Receptionist
    // Nurse - only own branch patient
    //
    // POST /api/MedicalHistory
    // =====================================================

    [Authorize(Roles = "Admin,Receptionist,Nurse")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMedicalHistoryDto dto)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (dto.PatientId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid PatientId is required."
            });
        }

        // =================================================
        // CHECK PATIENT EXISTS IN SAME HOSPITAL
        // =================================================

        var patient =
            await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.PatientId &&
                    x.HospitalId == hospitalId);

        if (patient == null)
        {
            return NotFound(new
            {
                message =
                    "Patient not found."
            });
        }

        // =================================================
        // NURSE BRANCH VALIDATION
        // =================================================

        var nurseScopeError =
            ValidateNurseScope();

        if (nurseScopeError != null)
        {
            return nurseScopeError;
        }

        if (IsNurse())
        {
            var canAccess =
                await NurseCanAccessPatient(
                    dto.PatientId);

            if (!canAccess)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "This patient does not belong to the nurse's assigned branch."
                    });
            }
        }

        // =================================================
        // CREATE MEDICAL HISTORY
        // =================================================

        var history =
            new MedicalHistory
            {
                PatientId =
                    dto.PatientId,

                Allergies =
                    dto.Allergies,

                ChronicDiseases =
                    dto.ChronicDiseases,

                CurrentMedications =
                    dto.CurrentMedications,

                Surgeries =
                    dto.Surgeries,

                HospitalId =
                    hospitalId,

                CreatedAt =
                    DateTime.UtcNow
            };

        _context.MedicalHistories
            .Add(history);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Medical history created successfully.",

            historyId =
                history.Id,

            patientId =
                history.PatientId
        });
    }

    // =====================================================
    // GET LATEST MEDICAL HISTORY
    //
    // Nurse can view only patient from own branch.
    //
    // GET /api/MedicalHistory/{patientId}
    // =====================================================

    [HttpGet("{patientId:int}")]
    public async Task<IActionResult> GetLatest(
        int patientId)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (patientId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid patientId is required."
            });
        }

        // =================================================
        // NURSE BRANCH VALIDATION
        // =================================================

        var nurseScopeError =
            ValidateNurseScope();

        if (nurseScopeError != null)
        {
            return nurseScopeError;
        }

        if (IsNurse())
        {
            var canAccess =
                await NurseCanAccessPatient(
                    patientId);

            if (!canAccess)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "This patient does not belong to the nurse's assigned branch."
                    });
            }
        }

        var history =
            await _context.MedicalHistories
                .AsNoTracking()
                .Where(x =>
                    x.PatientId == patientId &&
                    x.HospitalId == hospitalId)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x => new
                {
                    x.Id,

                    x.PatientId,

                    x.Allergies,

                    x.ChronicDiseases,

                    x.CurrentMedications,

                    x.Surgeries,

                    createdAt =
                        x.CreatedAt
                            .ToString(
                                "dd MMM yyyy hh:mm tt")
                })
                .FirstOrDefaultAsync();

        if (history == null)
        {
            return NotFound(new
            {
                message =
                    "No medical history found."
            });
        }

        return Ok(history);
    }

    // =====================================================
    // GET ALL MEDICAL HISTORY
    //
    // Nurse can view only patient from own branch.
    //
    // GET /api/MedicalHistory/all/{patientId}
    // =====================================================

    [HttpGet("all/{patientId:int}")]
    public async Task<IActionResult> GetAll(
        int patientId)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (patientId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid patientId is required."
            });
        }

        // =================================================
        // NURSE BRANCH VALIDATION
        // =================================================

        var nurseScopeError =
            ValidateNurseScope();

        if (nurseScopeError != null)
        {
            return nurseScopeError;
        }

        if (IsNurse())
        {
            var canAccess =
                await NurseCanAccessPatient(
                    patientId);

            if (!canAccess)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "This patient does not belong to the nurse's assigned branch."
                    });
            }
        }

        var histories =
            await _context.MedicalHistories
                .AsNoTracking()
                .Where(x =>
                    x.PatientId == patientId &&
                    x.HospitalId == hospitalId)
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x => new
                {
                    x.Id,

                    x.PatientId,

                    x.Allergies,

                    x.ChronicDiseases,

                    x.CurrentMedications,

                    x.Surgeries,

                    createdAt =
                        x.CreatedAt
                            .ToString(
                                "dd MMM yyyy hh:mm tt")
                })
                .ToListAsync();

        return Ok(histories);
    }

    // =====================================================
    // UPDATE MEDICAL HISTORY
    //
    // Admin
    // Receptionist
    // Nurse - only own branch patient
    //
    // PUT /api/MedicalHistory/{id}
    // =====================================================

    [Authorize(Roles = "Admin,Receptionist,Nurse")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] CreateMedicalHistoryDto dto)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid medical history id is required."
            });
        }

        var history =
            await _context.MedicalHistories
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.HospitalId == hospitalId);

        if (history == null)
        {
            return NotFound(new
            {
                message =
                    "Medical history not found."
            });
        }

        // =================================================
        // NURSE BRANCH VALIDATION
        // =================================================

        var nurseScopeError =
            ValidateNurseScope();

        if (nurseScopeError != null)
        {
            return nurseScopeError;
        }

        if (IsNurse())
        {
            var canAccess =
                await NurseCanAccessPatient(
                    history.PatientId);

            if (!canAccess)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message =
                            "This patient's medical history does not belong to the nurse's assigned branch."
                    });
            }
        }

        // =================================================
        // DO NOT CHANGE PATIENT ID DURING UPDATE
        //
        // We update only medical-history fields.
        // =================================================

        history.Allergies =
            dto.Allergies;

        history.ChronicDiseases =
            dto.ChronicDiseases;

        history.CurrentMedications =
            dto.CurrentMedications;

        history.Surgeries =
            dto.Surgeries;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Medical history updated successfully.",

            historyId =
                history.Id,

            patientId =
                history.PatientId
        });
    }

    // =====================================================
    // DELETE MEDICAL HISTORY
    //
    // ADMIN ONLY
    //
    // DELETE /api/MedicalHistory/{id}
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var hospitalId =
            GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "Hospital information is missing from token."
            });
        }

        if (id <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid medical history id is required."
            });
        }

        var history =
            await _context.MedicalHistories
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.HospitalId == hospitalId);

        if (history == null)
        {
            return NotFound(new
            {
                message =
                    "Medical history not found."
            });
        }

        _context.MedicalHistories
            .Remove(history);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Medical history deleted successfully.",

            historyId =
                id
        });
    }
}