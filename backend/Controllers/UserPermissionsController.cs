using System.Security.Claims;
using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/user-permissions")]
[Authorize]
public class UserPermissionsController : ControllerBase
{
    private static readonly string[] AllowedModules = PermissionModules.All;

    private readonly AppDbContext _context;

    public UserPermissionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("modules")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetModules()
    {
        return Ok(AllowedModules.Select(module => new
        {
            module,
            canView = true,
            canCreate = true,
            canEdit = true,
            canDelete = true
        }));
    }

    [HttpGet("eligible-users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEligibleUsers([FromQuery] string? role = null)
    {
        var adminHospitalId = GetHospitalId();
        if (adminHospitalId == null)
            return Unauthorized(new { message = "Hospital information is missing from token" });

        var query = _context.Users
            .Where(x => x.HospitalId == adminHospitalId &&
                        x.IsActive &&
                        (x.Role == "Doctor" || x.Role == "Receptionist" || x.Role == "Nurse"));

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role != "Doctor" && role != "Receptionist" && role != "Nurse")
                return BadRequest(new { message = "Role must be Doctor, Receptionist or Nurse" });

            query = query.Where(x => x.Role == role);
        }

        var users = await query
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Role,
                x.BranchId,
                assignedPermissionCount = _context.UserPermissions.Count(p => p.UserId == x.Id)
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserPermissions(int userId)
    {
        var target = await GetManagedUser(userId);
        if (target == null)
            return NotFound(new { message = "Doctor, Receptionist or Nurse not found in your hospital" });

        var saved = await _context.UserPermissions
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Module, StringComparer.OrdinalIgnoreCase);

        var permissions = AllowedModules.Select(module =>
        {
            saved.TryGetValue(module, out var permission);
            return new
            {
                module,
                // No saved row means the module uses the temporary default: full access.
                // Once Admin saves a row, the saved true/false values are respected.
                canView = permission?.CanView ?? true,
                canCreate = permission?.CanCreate ?? true,
                canEdit = permission?.CanEdit ?? true,
                canDelete = permission?.CanDelete ?? true
            };
        });

        return Ok(new
        {
            user = new { target.Id, target.Name, target.Email, target.Role, target.HospitalId, target.BranchId },
            permissions
        });
    }

    [HttpPut("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignPermissions(int userId, AssignUserPermissionsDto dto)
    {
        var target = await GetManagedUser(userId);
        if (target == null)
            return NotFound(new { message = "Doctor, Receptionist or Nurse not found in your hospital" });

        if (dto.Permissions == null)
            return BadRequest(new { message = "Permissions are required" });

        var duplicateModule = dto.Permissions
            .GroupBy(x => x.Module?.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => string.IsNullOrWhiteSpace(g.Key) || g.Count() > 1);

        if (duplicateModule != null)
            return BadRequest(new { message = "Permission modules must be unique and non-empty" });

        var invalidModules = dto.Permissions
            .Select(x => x.Module.Trim())
            .Where(x => !AllowedModules.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidModules.Length > 0)
            return BadRequest(new { message = "Invalid permission module", invalidModules, allowedModules = AllowedModules });

        var adminId = GetUserId();
        if (adminId == null)
            return Unauthorized(new { message = "Invalid user token" });

        var existing = await _context.UserPermissions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _context.UserPermissions.RemoveRange(existing);

        foreach (var item in dto.Permissions)
        {
            var module = AllowedModules.First(x =>
                string.Equals(x, item.Module.Trim(), StringComparison.OrdinalIgnoreCase));

            _context.UserPermissions.Add(new UserPermission
            {
                UserId = userId,
                HospitalId = target.HospitalId!.Value,
                Module = module,
                CanView = item.CanView,
                CanCreate = item.CanCreate,
                CanEdit = item.CanEdit,
                CanDelete = item.CanDelete,
                AssignedByUserId = adminId.Value,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"Permissions assigned successfully to {target.Role}", userId });
    }

    [HttpDelete("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClearPermissions(int userId)
    {
        var target = await GetManagedUser(userId);
        if (target == null)
            return NotFound(new { message = "Doctor, Receptionist or Nurse not found in your hospital" });

        var permissions = await _context.UserPermissions.Where(x => x.UserId == userId).ToListAsync();
        _context.UserPermissions.RemoveRange(permissions);
        await _context.SaveChangesAsync();

        return Ok(new { message = "All user permissions removed successfully" });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Doctor,Receptionist,Nurse")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new { message = "Invalid user token" });

        var saved = await _context.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Module, StringComparer.OrdinalIgnoreCase);

        var permissions = AllowedModules
            .Select(module =>
            {
                saved.TryGetValue(module, out var permission);

                return new UserPermissionResponseDto
                {
                    Module = module,
                    CanView = permission?.CanView ?? true,
                    CanCreate = permission?.CanCreate ?? true,
                    CanEdit = permission?.CanEdit ?? true,
                    CanDelete = permission?.CanDelete ?? true
                };
            })
            .ToList();

        return Ok(permissions);
    }

    private int? GetUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private int? GetHospitalId()
    {
        return int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : null;
    }

    private async Task<User?> GetManagedUser(int userId)
    {
        var hospitalId = GetHospitalId();
        if (hospitalId == null)
            return null;

        return await _context.Users.FirstOrDefaultAsync(x =>
            x.Id == userId &&
            x.HospitalId == hospitalId &&
            x.IsActive &&
            (x.Role == "Doctor" || x.Role == "Receptionist" || x.Role == "Nurse"));
    }
}
