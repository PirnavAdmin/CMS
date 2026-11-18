
using System.Security.Claims;
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
    private readonly AppDbContext _context;

    public UserPermissionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("eligible-users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEligibleUsers([FromQuery] string? role = null)
    {
        var adminHospitalId = GetHospitalId();

        if (adminHospitalId == null)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token"
            });
        }

        var query = _context.Users
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == adminHospitalId &&
                x.IsActive &&
                (
                    x.Role == "Doctor" ||
                    x.Role == "Receptionist" ||
                    x.Role == "Nurse" ||
                    x.Role == "LabTechnician" ||
                    x.Role == "Pharmacist"
                ));

        if (!string.IsNullOrWhiteSpace(role))
        {
            var selectedRole = role.Trim();

            query = query.Where(x =>
                x.Role == selectedRole);
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
                assignedPermissionCount =
                    _context.UserPermissions.Count(p => p.UserId == x.Id)
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
        {
            return NotFound(new
            {
                message = "Doctor, Receptionist, Nurse, LabTechnician or Pharmacist not found in your hospital"
            });
        }

        var permissions = await _context.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Module)
            .Select(x => new
            {
                module = x.Module,
                canView = x.CanView,
                canCreate = x.CanCreate,
                canEdit = x.CanEdit,
                canDelete = x.CanDelete
            })
            .ToListAsync();

        return Ok(new
        {
            user = new
            {
                target.Id,
                target.Name,
                target.Email,
                target.Role,
                target.HospitalId,
                target.BranchId
            },
            permissions
        });
    }

    [HttpPut("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignPermissions(
        int userId,
        AssignUserPermissionsDto dto)
    {
        var target = await GetManagedUser(userId);

        if (target == null)
        {
            return NotFound(new
            {
                message = "Doctor, Receptionist, Nurse, LabTechnician or Pharmacist not found in your hospital"
            });
        }

        var adminId = GetUserId();

        if (adminId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user token"
            });
        }

        var existing = await _context.UserPermissions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _context.UserPermissions.RemoveRange(existing);

        if (dto.Permissions != null)
        {
            foreach (var item in dto.Permissions)
            {
                _context.UserPermissions.Add(
                    new UserPermission
                    {
                        UserId = userId,
                        HospitalId = target.HospitalId!.Value,
                        Module = item.Module?.Trim() ?? string.Empty,
                        CanView = item.CanView,
                        CanCreate = item.CanCreate,
                        CanEdit = item.CanEdit,
                        CanDelete = item.CanDelete,
                        AssignedByUserId = adminId.Value,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Permissions assigned successfully to {target.Role}",
            userId
        });
    }

    [HttpDelete("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ClearPermissions(int userId)
    {
        var target = await GetManagedUser(userId);

        if (target == null)
        {
            return NotFound(new
            {
                message = "Doctor, Receptionist, Nurse, LabTechnician or Pharmacist not found in your hospital"
            });
        }

        var permissions = await _context.UserPermissions
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _context.UserPermissions.RemoveRange(permissions);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "All user permissions removed successfully"
        });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Admin,Doctor,Receptionist,Nurse,LabTechnician,Pharmacist")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user token"
            });
        }

        var role =
            User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? string.Empty;

        // =================================================
        // ADMIN
        // Read SuperAdmin-assigned Admin permissions from
        // RolePermissions using logged-in Admin UserId +
        // HospitalId. Admin can read only own permissions.
        // =================================================
        if (string.Equals(
            role,
            "Admin",
            StringComparison.OrdinalIgnoreCase))
        {
            var hospitalId = GetHospitalId();

            if (hospitalId == null)
            {
                return Unauthorized(new
                {
                    message = "Hospital information is missing from token"
                });
            }

            var adminExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == userId.Value &&
                    x.Role == "Admin" &&
                    x.HospitalId == hospitalId.Value &&
                    x.IsActive);

            if (!adminExists)
            {
                return Unauthorized(new
                {
                    message = "Admin account does not match the hospital in token"
                });
            }

            var adminPermissions = await _context.RolePermissions
                .AsNoTracking()
                .Where(x =>
                    x.AdminUserId == userId.Value &&
                    x.HospitalId == hospitalId.Value &&
                    x.RoleName != null &&
                    x.RoleName.ToLower() == "admin")
                .OrderBy(x => x.Module)
                .Select(x => new
                {
                    id = x.Id,
                    hospitalId = x.HospitalId,
                    adminUserId = x.AdminUserId,
                    roleName = x.RoleName,
                    module = x.Module,
                    canView = x.CanView,
                    canCreate = x.CanCreate,
                    canEdit = x.CanEdit,
                    canDelete = x.CanDelete
                })
                .ToListAsync();

            return Ok(adminPermissions);
        }

        // =================================================
        // STAFF
        // Doctor / Receptionist / Nurse / LabTechnician / Pharmacist
        // Read Admin-assigned permissions from UserPermissions.
        // =================================================
        var staffHospitalId = GetHospitalId();
        if (staffHospitalId == null)
        {
            return Unauthorized(new { message = "Hospital information is missing from token" });
        }

        var staffExists = await _context.Users.AsNoTracking().AnyAsync(x =>
            x.Id == userId.Value &&
            x.HospitalId == staffHospitalId.Value &&
            x.IsActive &&
            x.Role == role);

        if (!staffExists)
        {
            return Unauthorized(new { message = "Staff account does not match the hospital in token" });
        }

        var staffPermissions = await _context.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value && x.HospitalId == staffHospitalId.Value)
            .OrderBy(x => x.Module)
            .Select(x => new
            {
                id = x.Id,
                hospitalId = x.HospitalId,
                userId = x.UserId,
                roleName = role,
                module = x.Module,
                canView = x.CanView,
                canCreate = x.CanCreate,
                canEdit = x.CanEdit,
                canDelete = x.CanDelete
            })
            .ToListAsync();

        return Ok(staffPermissions);
    }

    private int? GetUserId()
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
                ? id
                : null;
    }

    private int? GetHospitalId()
    {
        return int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id)
                ? id
                : null;
    }

    private async Task<User?> GetManagedUser(int userId)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId == null)
        {
            return null;
        }

        return await _context.Users
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.HospitalId == hospitalId &&
                x.IsActive &&
                (
                    x.Role == "Doctor" ||
                    x.Role == "Receptionist" ||
                    x.Role == "Nurse" ||
                    x.Role == "LabTechnician" ||
                    x.Role == "Pharmacist"
                ));
    }
}
