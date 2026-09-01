using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/admins")]
[Authorize(Roles = "SuperAdmin")]
public class AdminManagementController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;

    public AdminManagementController(
        AppDbContext context,
        EmailHelper emailHelper)
    {
        _context = context;
        _emailHelper = emailHelper;
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

        var hospitalId = dto.HospitalId;

        var name =
            !string.IsNullOrWhiteSpace(dto.Name)
                ? dto.Name
                : (dto.FullName ?? string.Empty);

        var phone =
            !string.IsNullOrWhiteSpace(dto.MobileNumber)
                ? dto.MobileNumber
                : dto.Phone;

        if (hospitalId <= 0)
        {
            return BadRequest(new
            {
                message = "Clinic is required"
            });
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "Admin name is required"
            });
        }

        var hospital = await _context.Hospitals
            .FirstOrDefaultAsync(x =>
                x.Id == hospitalId &&
                x.IsActive);

        if (hospital == null)
        {
            return BadRequest(new
            {
                message = "Clinic not found"
            });
        }

        // =================================================
        // CHECK ADMIN ALREADY EXISTS FOR THIS HOSPITAL
        // =================================================
        var adminExists = await _context.Users
            .AnyAsync(x =>
                x.Role == "Admin" &&
                x.HospitalId == hospitalId);

        if (adminExists)
        {
            return BadRequest(new
            {
                message = "An Admin already exists for this clinic."
            });
        }

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

        if (dto.SendWelcomeEmail)
        {
            await _emailHelper.SendAdminCredentials(
                email,
                temporaryPassword);
        }

        return Ok(new
        {
            message = "Admin created successfully",

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

        var hospitalId = dto.HospitalId;

        if (hospitalId <= 0)
        {
            return BadRequest(new
            {
                message = "Clinic is required"
            });
        }

        var hospitalExists = await _context.Hospitals
            .AnyAsync(x =>
                x.Id == hospitalId &&
                x.IsActive);

        if (!hospitalExists)
        {
            return BadRequest(new
            {
                message = "Clinic not found"
            });
        }

        // =================================================
        // PREVENT MOVING ADMIN TO A CLINIC THAT ALREADY
        // HAS ANOTHER ADMIN
        // =================================================
        var anotherAdmin = await _context.Users
            .AnyAsync(x =>
                x.Role == "Admin" &&
                x.HospitalId == hospitalId &&
                x.Id != id);

        if (anotherAdmin)
        {
            return BadRequest(new
            {
                message = "This clinic already has an Admin."
            });
        }

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

        admin.HospitalId =
            hospitalId;

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
