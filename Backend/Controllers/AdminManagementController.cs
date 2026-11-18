using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/admins")]
[Authorize(Roles = "SuperAdmin")]
public class AdminManagementController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;
    private readonly ILogger<AdminManagementController> _logger;

    public AdminManagementController(
        AppDbContext context,
        EmailHelper emailHelper,
        ILogger<AdminManagementController> logger)
    {
        _context = context;
        _emailHelper = emailHelper;
        _logger = logger;
    }

    // =====================================================
    // CREATE ADMIN
    // Accepts both MobileNumber and Phone from frontend.
    // MobileNumber takes priority if both are supplied.
    // =====================================================
    [HttpPost]
    public async Task<IActionResult> CreateAdmin(
        CreateAdminDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new
            {
                message = "Email is required"
            });
        }

        if (!dto.Email.EndsWith(
            "@gmail.com",
            StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only Gmail addresses are allowed."
            });
        }

        // SuperAdmin creates the Admin account; the Admin creates their own clinic.
        // Ignore any clinic ID supplied during account creation.
        int? hospitalId = null;

        var name = !string.IsNullOrWhiteSpace(dto.Name) ? dto.Name : (dto.FullName ?? string.Empty);
        var phone = !string.IsNullOrWhiteSpace(dto.MobileNumber) ? dto.MobileNumber : dto.Phone;

        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Admin name is required" });

        var email = dto.Email.Trim();

        var exists = await _context.Users
            .AnyAsync(x => x.Email == email);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Email already exists"
            });
        }

        var temporaryPassword =
            "Admin@" +
            Guid.NewGuid()
                .ToString("N")
                .Substring(0, 6);

        var admin = new User
        {
            Name = name.Trim(),

            MobileNumber =
                phone?.Trim() ?? string.Empty,

            Email = email,

            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    temporaryPassword),

            MustChangePassword = true,

            Role = "Admin",

            HospitalId = hospitalId,

            IsActive = true
        };

        _context.Users.Add(admin);

        await _context.SaveChangesAsync();

        // Always attempt to send the welcome email. Previously this depended on
        // SendWelcomeEmail, which may be omitted or false in the frontend payload.
        // Account creation has already committed, so report delivery failures
        // separately instead of returning an ambiguous 500 that prompts retries.
        bool emailSent = false;
        try
        {
            await _emailHelper.SendAdminCredentials(email, temporaryPassword);
            emailSent = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Admin account {AdminId} created, but welcome email to {Email} failed.",
                admin.Id, email);
        }

        return Ok(new
        {
            message = emailSent
                ? "Admin created successfully. Login details email sent."
                : "Admin created successfully, but login details email could not be sent. Check SMTP settings and server logs. The Admin can use Forgot Password to set a new password.",
            emailSent,
            // Retain the original response field for frontend compatibility.
            // Do not display or log this password; remove it once the frontend
            // uses the email/OTP onboarding flow exclusively.
            temporaryPassword,
            id = admin.Id,
            adminId = admin.Id
        });
    }

    // =====================================================
    // GET ALL ADMINS
    // =====================================================
    [HttpGet]
    public async Task<IActionResult> GetAllAdmins()
    {
        var admins = await _context.Users
            .Include(x => x.Hospital)
            .Where(x => x.Role == "Admin")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,

                name = x.Name,

                fullName = x.Name,

                email = x.Email,

                mobileNumber = x.MobileNumber,

                phone = x.MobileNumber,

                role = x.Role,

                roleName = x.Role,

                hospitalId = x.HospitalId,

                clinicId = x.HospitalId,

                clinicName = x.Hospital != null
                    ? x.Hospital.Name
                    : string.Empty,

                clinic = x.Hospital != null
                    ? x.Hospital.Name
                    : string.Empty,

                branchCount = x.HospitalId.HasValue ? _context.Branches.Count(b => b.HospitalId == x.HospitalId.Value && b.IsActive) : 0,
                branchNames = x.HospitalId.HasValue ? _context.Branches.Where(b => b.HospitalId == x.HospitalId.Value && b.IsActive).OrderBy(b => b.Name).Select(b => b.Name).ToList() : new List<string>(),

                isActive = x.IsActive,

                status =
                    x.IsActive
                        ? "active"
                        : "inactive",

                forcePasswordChange =
                    x.MustChangePassword,

                mustChangePassword =
                    x.MustChangePassword
            })
            .ToListAsync();

        return Ok(admins);
    }

    // =====================================================
    // GET ADMIN BY ID
    // =====================================================
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAdmin(
        int id)
    {
        var admin = await _context.Users
            .Include(x => x.Hospital)
            .Where(x =>
                x.Id == id &&
                x.Role == "Admin")
            .Select(x => new
            {
                id = x.Id,

                name = x.Name,

                fullName = x.Name,

                email = x.Email,

                mobileNumber = x.MobileNumber,

                phone = x.MobileNumber,

                role = x.Role,

                roleName = x.Role,

                hospitalId = x.HospitalId,

                clinicId = x.HospitalId,

                clinicName = x.Hospital != null
                    ? x.Hospital.Name
                    : string.Empty,

                clinic = x.Hospital != null
                    ? x.Hospital.Name
                    : string.Empty,

                branchCount = x.HospitalId.HasValue ? _context.Branches.Count(b => b.HospitalId == x.HospitalId.Value && b.IsActive) : 0,
                branchNames = x.HospitalId.HasValue ? _context.Branches.Where(b => b.HospitalId == x.HospitalId.Value && b.IsActive).OrderBy(b => b.Name).Select(b => b.Name).ToList() : new List<string>(),

                isActive = x.IsActive,

                status =
                    x.IsActive
                        ? "active"
                        : "inactive",

                forcePasswordChange =
                    x.MustChangePassword,

                mustChangePassword =
                    x.MustChangePassword
            })
            .FirstOrDefaultAsync();

        if (admin == null)
        {
            return NotFound(new
            {
                message = "Admin not found"
            });
        }

        return Ok(admin);
    }

    // =====================================================
    // UPDATE ADMIN
    // Accepts both MobileNumber and Phone from frontend.
    // =====================================================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAdmin(
        int id,
        CreateAdminDto dto)
    {
        var admin = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "Admin");

        if (admin == null)
        {
            return NotFound(new
            {
                message = "Admin not found"
            });
        }

        // Clinic ownership is managed by the Admin onboarding flow, not by SuperAdmin.

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            admin.Name = dto.Name.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            admin.Name = dto.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim();

            if (!email.EndsWith(
                "@gmail.com",
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Only Gmail addresses are allowed."
                });
            }

            var emailExists = await _context.Users
                .AnyAsync(x =>
                    x.Id != id &&
                    x.Email == email);

            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "Email already exists"
                });
            }

            admin.Email = email;
        }

        var phone =
            !string.IsNullOrWhiteSpace(dto.MobileNumber)
                ? dto.MobileNumber
                : dto.Phone;

        if (!string.IsNullOrWhiteSpace(phone))
        {
            admin.MobileNumber =
                phone.Trim();
        }


        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Admin updated successfully"
        });
    }

    // =====================================================
    // DELETE ADMIN
    // =====================================================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAdmin(
        int id)
    {
        var admin = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "Admin");

        if (admin == null)
        {
            return NotFound(new
            {
                message = "Admin not found"
            });
        }

        _context.Users.Remove(admin);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Admin deleted successfully"
        });
    }

    // =====================================================
    // UPDATE ADMIN STATUS
    // =====================================================
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromQuery] string status)
    {
        var admin = await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "Admin");

        if (admin == null)
        {
            return NotFound(new
            {
                message = "Admin not found"
            });
        }

        if (!string.Equals(
                status,
                "Active",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                status,
                "Inactive",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Status must be Active or Inactive"
            });
        }

        admin.IsActive =
            string.Equals(
                status,
                "Active",
                StringComparison.OrdinalIgnoreCase);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                $"Admin {(admin.IsActive ? "active" : "inactive")} successfully"
        });
    }
}
