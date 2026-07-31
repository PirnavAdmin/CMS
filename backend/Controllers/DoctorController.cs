using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/Doctor")]
[Authorize]
public class DoctorController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;

    public DoctorController(
        AppDbContext context,
        EmailHelper emailHelper)
    {
        _context = context;
        _emailHelper = emailHelper;
    }

    // =====================================================
    // GET HOSPITAL ID
    // =====================================================

    private int GetHospitalId()
    {
        var claim = User.Claims
            .FirstOrDefault(x => x.Type == "HospitalId");

        return claim == null
            ? 0
            : int.Parse(claim.Value);
    }

    // =====================================================
    // GET DOCTOR ID
    // =====================================================

    private int GetDoctorId()
    {
        var claim = User.Claims
            .FirstOrDefault(x => x.Type == "DoctorId");

        return claim == null
            ? 0
            : int.Parse(claim.Value);
    }

    // =====================================================
    // PARSE EXPERIENCE
    // =====================================================

    private static int ParseExperience(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var digits = new string(
            value.Where(char.IsDigit).ToArray());

        return int.TryParse(digits, out var years)
            ? years
            : 0;
    }

    // =====================================================
    // DOCTOR DASHBOARD
    // =====================================================

    [Authorize(Roles = "Doctor")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var doctorId = GetDoctorId();
        var hospitalId = GetHospitalId();
        var today = DateTime.Today;

        var appointments = await _context.Appointments
            .Include(x => x.Patient)
            .Where(x =>
                x.DoctorId == doctorId &&
                x.HospitalId == hospitalId &&
                x.Date.Date == today)
            .OrderBy(x => x.StartTime)
            .ToListAsync();

        var totalAppointments = appointments.Count;

        var waiting = appointments.Count(
            x => x.Status == "Waiting");

        var inProgress = appointments.Count(
            x => x.Status == "InProgress");

        var completed = appointments.Count(
            x => x.Status == "Completed");

        var todayQueue = appointments.Select(x => new
        {
            appointmentId = x.Id,
            patientId = x.PatientId,
            tokenNumber = x.TokenNumber,
            patientName = x.Patient.Name,
            age = x.Patient.Age,
            gender = x.Patient.Gender,
            phone = x.Patient.Phone,
            bloodGroup = x.Patient.BloodGroup,
            chiefComplaints = x.ChiefComplaints,
            bloodPressure = x.BloodPressure,
            sugarLevel = x.SugarLevel,
            temperature = x.Temperature,
            weight = x.Weight,
            pulseRate = x.PulseRate,
            respiratoryRate = x.RespiratoryRate,
            time = x.Date.Date
    .Add(x.StartTime)
    .ToString("hh:mm tt"),
            status = x.Status
        });

        return Ok(new
        {
            totalAppointments,
            waiting,
            inProgress,
            completed,
            todayQueue
        });
    }

    // =====================================================
    // GET DOCTOR PATIENTS
    // =====================================================

    [Authorize(Roles = "Doctor")]
    [HttpGet("patients")]
    public async Task<IActionResult> GetPatients()
    {
        var doctorId = GetDoctorId();
        var hospitalId = GetHospitalId();

        var patients = await _context.Appointments
            .Include(x => x.Patient)
            .Where(x =>
                x.DoctorId == doctorId &&
                x.HospitalId == hospitalId)
            .GroupBy(x => x.PatientId)
            .Select(g => new
            {
                patientId = g.First().Patient.Id,
                patientCode = g.First().Patient.PatientCode,
                patientName = g.First().Patient.Name,
                phone = g.First().Patient.Phone,
                age = g.First().Patient.Age,
                gender = g.First().Patient.Gender,
                bloodGroup = g.First().Patient.BloodGroup,
                totalAppointments = g.Count()
            })
            .ToListAsync();

        return Ok(patients);
    }

    // =====================================================
    // GET ALL DOCTORS
    // =====================================================

    [Authorize(Roles = "Admin,SuperAdmin,Receptionist")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var hospitalId = GetHospitalId();

        var query = _context.Doctors
            .AsQueryable();

        if (!User.IsInRole("SuperAdmin"))
        {
            query = query.Where(
                x => x.HospitalId == hospitalId);
        }

        var doctors = await query
            .Include(x => x.Branch)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                specialization = x.Specialization,
                areaofExpertise = x.AreaofExpertise,
                experience = x.Experience,
                qualification = x.Qualification,
                fees = x.Fees,
                consultationFee = x.Fees,
                email = x.Email,
                phone = x.Phone,
                image = x.Image,
                isActive = x.IsActive,
                status = x.IsActive
                    ? "active"
                    : "inactive",
                hospitalId = x.HospitalId,
                branchId = x.BranchId,
                branchName = x.Branch != null
                    ? x.Branch.Name
                    : null,
                createdAt = x.CreatedAt
                    .ToString("dd MMM yyyy")
            })
            .ToListAsync();

        return Ok(doctors);
    }

    // =====================================================
    // GET DOCTOR BY ID
    // =====================================================

    [Authorize(Roles = "Admin,SuperAdmin,Receptionist")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var hospitalId = GetHospitalId();

        var query = _context.Doctors
            .Where(x => x.Id == id);

        if (!User.IsInRole("SuperAdmin"))
        {
            query = query.Where(
                x => x.HospitalId == hospitalId);
        }

        var doctor = await query
            .Include(x => x.Branch)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                specialization = x.Specialization,
                areaofExpertise = x.AreaofExpertise,
                experience = x.Experience,
                qualification = x.Qualification,
                fees = x.Fees,
                consultationFee = x.Fees,
                email = x.Email,
                phone = x.Phone,
                image = x.Image,
                isActive = x.IsActive,
                status = x.IsActive
                    ? "active"
                    : "inactive",
                hospitalId = x.HospitalId,
                branchId = x.BranchId,
                branchName = x.Branch != null
                    ? x.Branch.Name
                    : null,
                createdAt = x.CreatedAt
                    .ToString("dd MMM yyyy")
            })
            .FirstOrDefaultAsync();

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found"
            });
        }

        return Ok(doctor);
    }

    // =====================================================
    // CREATE DOCTOR
    // =====================================================

    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromForm] DoctorCreateDto dto)
    {
        if (!dto.ConsultationFee.HasValue ||
            dto.ConsultationFee.Value <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Consultation fee must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Email) ||
            !dto.Email.EndsWith(
                "@gmail.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Only Gmail addresses are allowed."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber) ||
            dto.PhoneNumber.Length != 10 ||
            !dto.PhoneNumber.All(char.IsDigit) ||
            !"6789".Contains(dto.PhoneNumber[0]))
        {
            return BadRequest(new
            {
                message =
                    "Enter a valid mobile number."
            });
        }

        var hospitalId = GetHospitalId();

        if (dto.BranchId == null)
        {
            return BadRequest(new
            {
                message =
                    "Please select a branch."
            });
        }

        var branchExists = await _context.Branches
            .AnyAsync(x =>
                x.Id == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new
            {
                message =
                    "Invalid branch."
            });
        }

        var exists =
            await _context.Doctors
                .AnyAsync(x => x.Email == dto.Email) ||
            await _context.Users
                .AnyAsync(x => x.Email == dto.Email);

        if (exists)
        {
            return BadRequest(new
            {
                message =
                    "Doctor email already exists"
            });
        }

        var temporaryPassword =
            "Doctor@" +
            Guid.NewGuid()
                .ToString("N")[..6];

        string? imagePath = null;

        if (dto.Image != null &&
            dto.Image.Length > 0)
        {
            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            var extension = Path
                .GetExtension(dto.Image.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message =
                        "Only JPG, JPEG, PNG, and WEBP images are allowed."
                });
            }

            const long maximumFileSize =
                5 * 1024 * 1024;

            if (dto.Image.Length > maximumFileSize)
            {
                return BadRequest(new
                {
                    message =
                        "Image size must not exceed 5 MB."
                });
            }

            var folder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "doctors");

            Directory.CreateDirectory(folder);

            var fileName =
                $"{Guid.NewGuid()}{extension}";

            var fullPath = Path.Combine(
                folder,
                fileName);

            await using var stream =
                new FileStream(
                    fullPath,
                    FileMode.Create);

            await dto.Image.CopyToAsync(stream);

            imagePath =
                "/images/doctors/" + fileName;
        }

        var doctor = new Doctor
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.PhoneNumber,
            Specialization = dto.Specialization,
            Qualification = dto.Qualification,
            Experience =
                ParseExperience(dto.Experience),
            Fees =
                dto.ConsultationFee.Value,
            Image = imagePath,
            IsActive = true,
            HospitalId = hospitalId,
            BranchId = dto.BranchId,
            AreaofExpertise =
                dto.AreaofExpertise,
            CreatedAt = DateTime.UtcNow
        };

        _context.Doctors.Add(doctor);

        var specializationExists =
            await _context.DoctorSpecializations
                .AnyAsync(x =>
                    x.Name == dto.Specialization);

        if (!specializationExists)
        {
            _context.DoctorSpecializations.Add(
                new DoctorSpecialization
                {
                    Name = dto.Specialization
                });
        }

        await _context.SaveChangesAsync();

        var doctorUser = new User
        {
            Name = doctor.Name,
            MobileNumber = doctor.Phone,
            Email = doctor.Email,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    temporaryPassword),
            Role = "Doctor",
            DoctorId = doctor.Id,
            HospitalId = hospitalId,
            BranchId = doctor.BranchId,
            IsActive = true,
            MustChangePassword = true
        };

        _context.Users.Add(doctorUser);

        await _context.SaveChangesAsync();

        await _emailHelper.SendAdminCredentials(
            dto.Email,
            temporaryPassword);

        return Ok(new
        {
            message =
                "Doctor created successfully",
            temporaryPassword,
            id = doctor.Id,
            doctorId = doctor.Id,
            image = doctor.Image
        });
    }

    // =====================================================
    // UPDATE DOCTOR
    // =====================================================

    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] DoctorCreateDto dto)
    {
        if (!dto.ConsultationFee.HasValue ||
            dto.ConsultationFee.Value <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Consultation fee must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.Email) ||
            !dto.Email.EndsWith(
                "@gmail.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Only Gmail addresses are allowed."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber) ||
            dto.PhoneNumber.Length != 10 ||
            !dto.PhoneNumber.All(char.IsDigit) ||
            !"6789".Contains(dto.PhoneNumber[0]))
        {
            return BadRequest(new
            {
                message =
                    "Enter a valid mobile number."
            });
        }

        var hospitalId = GetHospitalId();

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == hospitalId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found"
            });
        }

        if (dto.BranchId == null)
        {
            return BadRequest(new
            {
                message =
                    "Please select a branch."
            });
        }

        var branchExists = await _context.Branches
            .AnyAsync(x =>
                x.Id == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new
            {
                message =
                    "Invalid branch."
            });
        }

        var duplicateDoctorEmail =
            await _context.Doctors
                .AnyAsync(x =>
                    x.Id != id &&
                    x.Email == dto.Email);

        var duplicateUserEmail =
            await _context.Users
                .AnyAsync(x =>
                    x.DoctorId != doctor.Id &&
                    x.Email == dto.Email);

        if (duplicateDoctorEmail ||
            duplicateUserEmail)
        {
            return BadRequest(new
            {
                message =
                    "Another account already uses this email."
            });
        }

        doctor.BranchId = dto.BranchId;
        doctor.Name = dto.Name;
        doctor.Email = dto.Email;
        doctor.Phone = dto.PhoneNumber;
        doctor.Specialization =
            dto.Specialization;
        doctor.Qualification =
            dto.Qualification;
        doctor.AreaofExpertise =
            dto.AreaofExpertise;
        doctor.Experience =
            ParseExperience(dto.Experience);
        doctor.Fees =
            dto.ConsultationFee.Value;
        doctor.IsActive =
            dto.IsActive;

        if (dto.Image != null &&
            dto.Image.Length > 0)
        {
            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

            var extension = Path
                .GetExtension(dto.Image.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    message =
                        "Only JPG, JPEG, PNG, and WEBP images are allowed."
                });
            }

            const long maximumFileSize =
                5 * 1024 * 1024;

            if (dto.Image.Length > maximumFileSize)
            {
                return BadRequest(new
                {
                    message =
                        "Image size must not exceed 5 MB."
                });
            }

            var folder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "images",
                "doctors");

            Directory.CreateDirectory(folder);

            var fileName =
                $"{Guid.NewGuid()}{extension}";

            var fullPath = Path.Combine(
                folder,
                fileName);

            await using (var stream =
                new FileStream(
                    fullPath,
                    FileMode.Create))
            {
                await dto.Image
                    .CopyToAsync(stream);
            }

            if (!string.IsNullOrWhiteSpace(
                    doctor.Image))
            {
                var oldRelativePath =
                    doctor.Image
                        .TrimStart('/')
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar);

                var oldFullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    oldRelativePath);

                if (System.IO.File.Exists(
                        oldFullPath))
                {
                    System.IO.File.Delete(
                        oldFullPath);
                }
            }

            doctor.Image =
                "/images/doctors/" + fileName;
        }

        var doctorUser =
            await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.DoctorId == doctor.Id &&
                    x.Role == "Doctor");

        if (doctorUser != null)
        {
            doctorUser.Name =
                doctor.Name;

            doctorUser.Email =
                doctor.Email;

            doctorUser.MobileNumber =
                doctor.Phone;

            doctorUser.IsActive =
                doctor.IsActive;

            doctorUser.BranchId =
                doctor.BranchId;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Doctor updated successfully",
            doctorId = doctor.Id,
            image = doctor.Image
        });
    }

    // =====================================================
    // DELETE DOCTOR
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var hospitalId = GetHospitalId();

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == hospitalId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found"
            });
        }

        var hasAppointments =
            await _context.Appointments
                .AnyAsync(x =>
                    x.DoctorId == id);

        if (hasAppointments)
        {
            return BadRequest(new
            {
                message =
                    "Doctor cannot be deleted because appointments are associated with this doctor."
            });
        }

        if (!string.IsNullOrWhiteSpace(
                doctor.Image))
        {
            var oldRelativePath =
                doctor.Image
                    .TrimStart('/')
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar);

            var oldFullPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                oldRelativePath);

            if (System.IO.File.Exists(
                    oldFullPath))
            {
                System.IO.File.Delete(
                    oldFullPath);
            }
        }

        var doctorUser =
            await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.DoctorId == doctor.Id &&
                    x.Role == "Doctor");

        if (doctorUser != null)
        {
            _context.Users.Remove(
                doctorUser);
        }

        _context.Doctors.Remove(doctor);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Doctor deleted successfully"
        });
    }

    // =====================================================
    // GET SPECIALIZATIONS
    // =====================================================

    [HttpGet("specializations")]
    public async Task<IActionResult>
        GetSpecializations()
    {
        var defaults = new List<string>
        {
            "Cardiology",
            "Dermatology",
            "ENT",
            "General Medicine",
            "Gynecology",
            "Neurology",
            "Orthopedics",
            "Pediatrics",
            "Psychiatry",
            "Radiology",
            "Other"
        };

        var masterData =
            await _context.DoctorSpecializations
                .Select(x => x.Name)
                .ToListAsync();

        var doctorData =
            await _context.Doctors
                .Where(x =>
                    !string.IsNullOrEmpty(
                        x.Specialization))
                .Select(x =>
                    x.Specialization!)
                .Distinct()
                .ToListAsync();

        var result = defaults
            .Union(masterData)
            .Union(doctorData)
            .OrderBy(x => x)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // GET QUALIFICATIONS
    // =====================================================

    [HttpGet("qualifications")]
    public async Task<IActionResult>
        GetQualifications()
    {
        var defaults = new List<string>
        {
            "Bachelor of Medicine and Bachelor of Surgery (MBBS)",
            "Bachelor of Dental Surgery (BDS)",
            "Bachelor of Ayurvedic Medicine and Surgery (BAMS)",
            "Bachelor of Homeopathic Medicine and Surgery (BHMS)",
            "Doctor of Medicine (MD)",
            "Master of Surgery (MS)",
            "Diplomate of National Board (DNB)",
            "Doctorate of Medicine (DM)",
            "Master of Chirurgiae (MCh)"
        };

        var masterData =
            await _context.DoctorQualifications
                .Select(x => x.Name)
                .ToListAsync();

        var doctorData =
            await _context.Doctors
                .Where(x =>
                    !string.IsNullOrEmpty(
                        x.Qualification))
                .Select(x =>
                    x.Qualification!)
                .Distinct()
                .ToListAsync();

        var result = defaults
            .Union(masterData)
            .Union(doctorData)
            .OrderBy(x => x)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // UPDATE DOCTOR STATUS
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateDoctorStatusDto dto)
    {
        var hospitalId = GetHospitalId();

        if (dto.Status != "Active" &&
            dto.Status != "Inactive")
        {
            return BadRequest(new
            {
                message =
                    "Status must be Active or Inactive"
            });
        }

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == hospitalId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found"
            });
        }

        doctor.IsActive =
            dto.Status == "Active";

        var user = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.DoctorId == doctor.Id &&
                x.Role == "Doctor");

        if (user != null)
        {
            user.IsActive =
                doctor.IsActive;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                $"Doctor status updated to {dto.Status}"
        });
    }

    // =====================================================
    // GET DOCTORS BY BRANCH
    // =====================================================

    [Authorize(
        Roles = "Admin,SuperAdmin,Receptionist")]
    [HttpGet("branch/{branchId}")]
    public async Task<IActionResult>
        GetDoctorsByBranch(int branchId)
    {
        var hospitalId = GetHospitalId();

        var query = _context.Doctors
            .Include(x => x.Branch)
            .AsQueryable();

        if (!User.IsInRole("SuperAdmin"))
        {
            query = query.Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId);
        }
        else
        {
            query = query.Where(
                x => x.BranchId == branchId);
        }

        var doctors = await query
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                specialization =
                    x.Specialization,
                qualification =
                    x.Qualification,
                experience =
                    x.Experience,
                fees = x.Fees,
                email = x.Email,
                phone = x.Phone,
                image = x.Image,
                hospitalId =
                    x.HospitalId,
                branchId =
                    x.BranchId,
                branchName =
                    x.Branch != null
                        ? x.Branch.Name
                        : null,
                isActive =
                    x.IsActive,
                status =
                    x.IsActive
                        ? "Active"
                        : "Inactive"
            })
            .ToListAsync();

        return Ok(doctors);
    }
    [Authorize(Roles = "Admin")]
    [HttpPut("{doctorId:int}/branches")]
    public async Task<IActionResult> AssignBranches(int doctorId, AssignDoctorBranchesDto dto)
    {
        var hospitalClaim = User.Claims.FirstOrDefault(x => x.Type == "HospitalId")?.Value;
        if (!int.TryParse(hospitalClaim, out var hospitalId)) return Unauthorized();
        var doctor = await _context.Doctors.FirstOrDefaultAsync(x => x.Id == doctorId && x.HospitalId == hospitalId);
        if (doctor == null) return NotFound(new { message = "Doctor not found" });
        if (dto.BranchIds == null || dto.BranchIds.Count == 0)
            return BadRequest(new { message = "At least one branch must be selected" });

        var ids = dto.BranchIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { message = "At least one valid branch must be selected" });

        var validCount = await _context.Branches.CountAsync(x => ids.Contains((int)x.Id) && x.HospitalId == hospitalId && x.IsActive);
        if (validCount != ids.Count) return BadRequest(new { message = "One or more branches are invalid" });
        var old = await _context.DoctorBranches.Where(x => x.DoctorId == doctorId).ToListAsync();
        _context.DoctorBranches.RemoveRange(old);
        foreach (var branchId in ids) _context.DoctorBranches.Add(new DoctorBranch { DoctorId = doctorId, BranchId = branchId, HospitalId = hospitalId });
        doctor.BranchId = ids[0];
        await _context.SaveChangesAsync();
        return Ok(new { message = "Doctor branches updated successfully", doctorId, branchIds = ids });
    }

    [Authorize(Roles = "Admin,Doctor,Receptionist,Nurse")]
    [HttpGet("{doctorId:int}/branches")]
    public async Task<IActionResult> GetDoctorBranches(int doctorId)
    {
        var hospitalClaim = User.Claims.FirstOrDefault(x => x.Type == "HospitalId")?.Value;
        if (!int.TryParse(hospitalClaim, out var hospitalId)) return Unauthorized();
        var data = await _context.DoctorBranches.AsNoTracking().Include(x => x.Branch)
            .Where(x => x.DoctorId == doctorId && x.HospitalId == hospitalId && x.IsActive)
            .Select(x => new { x.BranchId, branchName = x.Branch.Name }).ToListAsync();
        return Ok(data);
    }

}