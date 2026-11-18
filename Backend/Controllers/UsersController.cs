using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthDemo.Controllers;

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Clinic { get; set; }
    public int? HospitalId { get; set; }
    public int? ClinicId { get; set; }
    // Nullable so legacy clients sending only Type are handled correctly.
    public string? Type { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public string? Phone { get; set; }
    public string? MobileNumber { get; set; }
    public string? Password { get; set; }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) => _context = context;

    // Resolve the tenant from the authenticated database user, never from request JSON.
    private async Task<int?> CurrentAdminHospitalIdAsync()
    {

        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("userId");
        if (int.TryParse(idText, out var userId))
        {
            var byId = await _context.Users.AsNoTracking()
                .Where(x => x.Id == userId && x.Role == "Admin" && x.IsActive)
                .Select(x => x.HospitalId)
                .FirstOrDefaultAsync();
            if (byId.HasValue && byId.Value > 0) return byId.Value;
        }

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            var byEmail = await _context.Users.AsNoTracking()
                .Where(x => x.Email.ToLower() == normalized && x.Role == "Admin" && x.IsActive)
                .Select(x => x.HospitalId)
                .FirstOrDefaultAsync();
            if (byEmail.HasValue && byEmail.Value > 0) return byEmail.Value;
        }
        return null;
    }

    private async Task<bool> CanAccessStaffAsync(User target)
    {
        if (target.Role == "Admin" || target.Role == "SuperAdmin") return false;
        var hospitalId = await CurrentAdminHospitalIdAsync();
        return hospitalId.HasValue && target.HospitalId == hospitalId.Value;
    }

    private static readonly string[] AllowedStaffRoles =
    {
        "Patient", "Doctor", "Receptionist", "LabTechnician", "Pharmacist", "Staff"
    };

    private static bool IsRestrictedRole(string? role) =>
        string.Equals(role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role?.Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        if (string.Equals(role.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)) return "Admin";
        if (string.Equals(role.Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase)) return "SuperAdmin";
        return AllowedStaffRoles.FirstOrDefault(x =>
            string.Equals(x, role.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? RequestedRole(CreateUserRequest dto, string? currentRole = null)
    {
        // Explicit Role wins, otherwise Type, otherwise retain existing role.
        var supplied = !string.IsNullOrWhiteSpace(dto.Role) ? dto.Role :
                       !string.IsNullOrWhiteSpace(dto.Type) ? dto.Type : currentRole ?? "Patient";
        return NormalizeRole(supplied);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var hospitalId = await CurrentAdminHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        var users = await _context.Users.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.Role != "Admin" && x.Role != "SuperAdmin")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                phone = x.MobileNumber,
                mobileNumber = x.MobileNumber,
                type = x.Role,
                role = x.Role,
                hospitalId = x.HospitalId,
                clinicId = x.HospitalId,
                clinic = x.Hospital == null ? "" : x.Hospital.Name,
                clinicName = x.Hospital == null ? "" : x.Hospital.Name,
                x.IsActive,
                status = x.IsActive ? "active" : "inactive",
                x.CreatedAt
            }).ToListAsync();
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var hospitalId = await CurrentAdminHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();

        var user = await _context.Users.AsNoTracking()
            .Where(x => x.Id == id && x.HospitalId == hospitalId && x.Role != "Admin" && x.Role != "SuperAdmin")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                phone = x.MobileNumber,
                mobileNumber = x.MobileNumber,
                type = x.Role,
                role = x.Role,
                hospitalId = x.HospitalId,
                clinicId = x.HospitalId,
                clinic = x.Hospital == null ? "" : x.Hospital.Name,
                clinicName = x.Hospital == null ? "" : x.Hospital.Name,
                x.IsActive,
                status = x.IsActive ? "active" : "inactive"
            }).FirstOrDefaultAsync();
        return user == null ? NotFound(new { message = "User not found" }) : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest dto)
    {
        if (dto == null) return BadRequest(new { message = "Request body is required." });

        // Check BOTH supplied fields: neither may smuggle a privileged role.
        if (IsRestrictedRole(dto.Role) || IsRestrictedRole(dto.Type))
            return StatusCode(403, new
            {
                message = "Admin accounts must be created through paid-subscription onboarding. SuperAdmin accounts cannot be created here."
            });

        var role = RequestedRole(dto);
        if (role == null || IsRestrictedRole(role))
            return BadRequest(new { message = "Invalid user role." });

        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(dto.Email) || dto.Email.Trim().Length > 200 ||
            !new EmailAddressAttribute().IsValid(dto.Email.Trim()))
            return BadRequest(new { message = "Valid name (max 150) and email (max 200) are required." });

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(x => x.Email.ToLower() == email))
            return Conflict(new { message = "Email already exists" });

        if (dto.HospitalId.HasValue && dto.ClinicId.HasValue && dto.HospitalId != dto.ClinicId)
            return BadRequest(new { message = "HospitalId and ClinicId must match." });

        var ownHospitalId = await CurrentAdminHospitalIdAsync();
        if (!ownHospitalId.HasValue) return Forbid();
        var requestedHospitalId = dto.HospitalId ?? dto.ClinicId;
        if (requestedHospitalId.HasValue && requestedHospitalId.Value != ownHospitalId.Value)
            return StatusCode(403, new { message = "Cannot create users in another clinic." });
        var hospitalId = ownHospitalId;
        if (!hospitalId.HasValue || hospitalId.Value <= 0 ||
            !await _context.Hospitals.AnyAsync(x => x.Id == hospitalId.Value && x.IsActive))
            return BadRequest(new { message = "Clinic not found" });

        // A generated password without an email-delivery mechanism would leave the
        // user unable to sign in. Require a supplied password for this legacy API.
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            return BadRequest(new { message = "Password of at least 8 characters is required for this endpoint." });

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            MobileNumber = !string.IsNullOrWhiteSpace(dto.MobileNumber)
                ? dto.MobileNumber.Trim() : dto.Phone?.Trim() ?? string.Empty,
            Role = role,
            HospitalId = hospitalId.Value,
            IsActive = !string.Equals(dto.Status, "inactive", StringComparison.OrdinalIgnoreCase),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            MustChangePassword = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "User created successfully",
            id = user.Id,
            passwordSetupRequired = true,
            temporaryPassword = (string?)null
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateUserRequest dto)
    {
        if (dto == null) return BadRequest(new { message = "Request body is required." });

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role != "SuperAdmin");
        if (user == null) return NotFound(new { message = "User not found" });
        if (!await CanAccessStaffAsync(user))
            return StatusCode(403, new { message = "You cannot manage this user's clinic or role." });

        if (IsRestrictedRole(dto.Role) && !string.Equals(dto.Role?.Trim(), user.Role, StringComparison.OrdinalIgnoreCase) ||
            IsRestrictedRole(dto.Type) && !string.Equals(dto.Type?.Trim(), user.Role, StringComparison.OrdinalIgnoreCase))
            return StatusCode(403, new { message = "Admin and SuperAdmin role changes are not permitted here." });

        var role = RequestedRole(dto, user.Role);
        if (role == null)
            return BadRequest(new { message = "Invalid user role." });

        // Existing Admins may update contact details, but not their role or clinic.
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (role != "Admin")
                return StatusCode(403, new { message = "An Admin role cannot be changed here." });
        }
        else if (IsRestrictedRole(role))
            return StatusCode(403, new { message = "Cannot promote a user to Admin or SuperAdmin here." });

        if (dto.HospitalId.HasValue && dto.ClinicId.HasValue && dto.HospitalId != dto.ClinicId)
            return BadRequest(new { message = "HospitalId and ClinicId must match." });

        var hospitalId = dto.HospitalId ?? dto.ClinicId ?? user.HospitalId;
        if (hospitalId != user.HospitalId)
            return StatusCode(403, new { message = "Cannot transfer users to another clinic." });
        if (!hospitalId.HasValue || hospitalId.Value <= 0 ||
            !await _context.Hospitals.AnyAsync(x => x.Id == hospitalId.Value && x.IsActive))
            return BadRequest(new { message = "Clinic not found" });

        if (user.Role == "Admin" && hospitalId != user.HospitalId)
            return StatusCode(403, new { message = "Admin clinic assignment cannot be changed here." });

        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(dto.Email) || dto.Email.Trim().Length > 200 ||
            !new EmailAddressAttribute().IsValid(dto.Email.Trim()))
            return BadRequest(new { message = "Valid name (max 150) and email (max 200) are required." });

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(x => x.Id != id && x.Email.ToLower() == email))
            return Conflict(new { message = "Email already exists" });

        user.Name = dto.Name.Trim();
        user.Email = email;
        user.MobileNumber = !string.IsNullOrWhiteSpace(dto.MobileNumber)
            ? dto.MobileNumber.Trim() : dto.Phone?.Trim() ?? user.MobileNumber;
        user.Role = role;
        user.HospitalId = hospitalId.Value;
        user.IsActive = !string.Equals(dto.Status, "inactive", StringComparison.OrdinalIgnoreCase);

        await _context.SaveChangesAsync();
        return Ok(new { message = "User updated successfully" });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] CreateUserRequest dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Status) ||
            !(string.Equals(dto.Status, "active", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(dto.Status, "inactive", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Status must be active or inactive." });

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role != "SuperAdmin");
        if (user == null) return NotFound(new { message = "User not found" });
        if (!await CanAccessStaffAsync(user))
            return StatusCode(403, new { message = "You cannot manage this user's clinic or role." });

        user.IsActive = string.Equals(dto.Status, "active", StringComparison.OrdinalIgnoreCase);
        await _context.SaveChangesAsync();
        return Ok(new { message = "User status updated successfully", user.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role != "SuperAdmin");
        if (user == null) return NotFound(new { message = "User not found" });
        if (!await CanAccessStaffAsync(user))
            return StatusCode(403, new { message = "You cannot manage this user's clinic or role." });

        user.IsActive = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "User deactivated successfully" });
    }
}

