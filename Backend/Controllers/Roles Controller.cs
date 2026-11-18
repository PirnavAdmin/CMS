using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public class RoleRequest
{
    public int HospitalId { get; set; }

    public int AdminUserId { get; set; }

    public string? RoleName { get; set; } = "Admin";

    // Frontend can send any module name
    public string? Module { get; set; }

    public bool? CanView { get; set; }

    public bool? CanCreate { get; set; }

    public bool? CanEdit { get; set; }

    public bool? CanDelete { get; set; }
}

[ApiController]
[Route("api/roles")]
[Authorize(Roles = "SuperAdmin")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RolesController(AppDbContext context)
    {
        _context = context;
    }

    private static object ToResponse(
        RolePermission role,
        int assignedUsers = 0)
    {
        return new
        {
            id = role.Id,
            hospitalId = role.HospitalId,
            adminUserId = role.AdminUserId,
            roleName = role.RoleName,
            module = role.Module,
            canView = role.CanView,
            canCreate = role.CanCreate,
            canEdit = role.CanEdit,
            canDelete = role.CanDelete,
            assignedUsers
        };
    }

    // =====================================================
    // GET ADMINS FOR A CLINIC
    // Example:
    // GET /api/roles/admins?hospitalId=1
    // =====================================================
    [HttpGet("admins")]
    public async Task<IActionResult> GetClinicAdmins(
        [FromQuery] int hospitalId)
    {
        if (hospitalId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid hospitalId is required."
            });
        }

        var hospitalExists = await _context.Hospitals
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == hospitalId &&
                x.IsActive);

        if (!hospitalExists)
        {
            return NotFound(new
            {
                message = "Clinic not found or inactive."
            });
        }

        var admins = await _context.Users
            .AsNoTracking()
            .Where(x =>
                x.Role == "Admin" &&
                x.HospitalId == hospitalId &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                email = x.Email,
                mobileNumber = x.MobileNumber,
                hospitalId = x.HospitalId,
                branchId = x.BranchId,
                role = x.Role
            })
            .ToListAsync();

        return Ok(admins);
    }

    // =====================================================
    // GET ROLE MODULE PERMISSIONS
    //
    // Examples:
    // GET /api/roles
    // GET /api/roles?hospitalId=1
    // GET /api/roles?hospitalId=1&adminUserId=20
    // GET /api/roles?hospitalId=1&adminUserId=20&roleName=Admin
    // =====================================================
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? hospitalId = null,
        [FromQuery] int? adminUserId = null,
        [FromQuery] string? roleName = null)
    {
        var query = _context.RolePermissions
            .AsNoTracking()
            .AsQueryable();

        if (hospitalId.HasValue && hospitalId.Value > 0)
        {
            query = query.Where(x =>
                x.HospitalId == hospitalId.Value);
        }

        if (adminUserId.HasValue && adminUserId.Value > 0)
        {
            query = query.Where(x =>
                x.AdminUserId == adminUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var normalizedRole = roleName.Trim().ToLower();

            query = query.Where(x =>
                x.RoleName != null &&
                x.RoleName.ToLower() == normalizedRole);
        }

        var roles = await query
            .OrderBy(x => x.HospitalId)
            .ThenBy(x => x.AdminUserId)
            .ThenBy(x => x.RoleName)
            .ThenBy(x => x.Module)
            .ToListAsync();

        var result = roles.Select(role =>
            ToResponse(role, 1));

        return Ok(result);
    }

    // =====================================================
    // GET ONE ROLE MODULE PERMISSION
    // =====================================================
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var role = await _context.RolePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role permission not found."
            });
        }

        return Ok(ToResponse(role, 1));
    }

    // =====================================================
    // CREATE ROLE MODULE PERMISSION
    // SuperAdmin -> Clinic -> Specific Admin -> Module
    // =====================================================
    [HttpPost]
    public async Task<IActionResult> Create(
        RoleRequest model)
    {
        if (model.HospitalId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid hospitalId is required."
            });
        }

        if (model.AdminUserId <= 0)
        {
            return BadRequest(new
            {
                message = "Valid adminUserId is required."
            });
        }

        var roleName = string.IsNullOrWhiteSpace(model.RoleName)
            ? "Admin"
            : model.RoleName.Trim();

        var module = model.Module?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(module))
        {
            return BadRequest(new
            {
                message = "Module is required."
            });
        }

        var hospitalExists = await _context.Hospitals
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == model.HospitalId &&
                x.IsActive);

        if (!hospitalExists)
        {
            return BadRequest(new
            {
                message = "Clinic not found or inactive."
            });
        }

        var adminExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == model.AdminUserId &&
                x.Role == "Admin" &&
                x.HospitalId == model.HospitalId &&
                x.IsActive);

        if (!adminExists)
        {
            return BadRequest(new
            {
                message = "Selected Admin does not belong to this clinic or is inactive."
            });
        }

        var exists = await _context.RolePermissions
            .AnyAsync(x =>
                x.HospitalId == model.HospitalId &&
                x.AdminUserId == model.AdminUserId &&
                x.RoleName != null &&
                x.RoleName.ToLower() == roleName.ToLower() &&
                (x.Module ?? "").ToLower() == module.ToLower());

        if (exists)
        {
            return BadRequest(new
            {
                message = "Permission already exists for this clinic, Admin and module."
            });
        }

        var role = new RolePermission
        {
            HospitalId = model.HospitalId,
            AdminUserId = model.AdminUserId,
            RoleName = roleName,
            Module = module,
            CanView = model.CanView ?? true,
            CanCreate = model.CanCreate ?? true,
            CanEdit = model.CanEdit ?? true,
            CanDelete = model.CanDelete ?? true
        };

        _context.RolePermissions.Add(role);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Admin module permission created successfully.",
            role = ToResponse(role, 1)
        });
    }

    // =====================================================
    // UPDATE ROLE MODULE PERMISSION
    // =====================================================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        RoleRequest model)
    {
        var role = await _context.RolePermissions
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role permission not found."
            });
        }

        var hospitalId = model.HospitalId > 0
            ? model.HospitalId
            : role.HospitalId;

        var adminUserId = model.AdminUserId > 0
            ? model.AdminUserId
            : role.AdminUserId;

        var roleName = !string.IsNullOrWhiteSpace(model.RoleName)
            ? model.RoleName.Trim()
            : role.RoleName ?? "Admin";

        var module = model.Module != null
            ? model.Module.Trim()
            : role.Module ?? string.Empty;

        var adminExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == adminUserId &&
                x.Role == "Admin" &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (!adminExists)
        {
            return BadRequest(new
            {
                message = "Selected Admin does not belong to this clinic or is inactive."
            });
        }

        var duplicate = await _context.RolePermissions
            .AnyAsync(x =>
                x.Id != id &&
                x.HospitalId == hospitalId &&
                x.AdminUserId == adminUserId &&
                x.RoleName != null &&
                x.RoleName.ToLower() == roleName.ToLower() &&
                (x.Module ?? "").ToLower() == module.ToLower());

        if (duplicate)
        {
            return BadRequest(new
            {
                message = "Permission already exists for this clinic, Admin and module."
            });
        }

        role.HospitalId = hospitalId;
        role.AdminUserId = adminUserId;
        role.RoleName = roleName;
        role.Module = module;

        if (model.CanView.HasValue)
            role.CanView = model.CanView.Value;

        if (model.CanCreate.HasValue)
            role.CanCreate = model.CanCreate.Value;

        if (model.CanEdit.HasValue)
            role.CanEdit = model.CanEdit.Value;

        if (model.CanDelete.HasValue)
            role.CanDelete = model.CanDelete.Value;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Admin module permission updated successfully.",
            role = ToResponse(role, 1)
        });
    }

    // =====================================================
    // UPDATE PERMISSIONS
    // Kept for frontend compatibility
    // =====================================================
    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> UpdatePermissions(
        int id,
        RoleRequest model)
    {
        return await Update(id, model);
    }

    // =====================================================
    // DELETE ONE ROLE MODULE PERMISSION
    // =====================================================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _context.RolePermissions
            .FirstOrDefaultAsync(x =>
                x.Id == id);

        if (role == null)
        {
            return NotFound(new
            {
                message = "Role permission not found."
            });
        }

        _context.RolePermissions.Remove(role);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Admin module permission deleted successfully."
        });
    }

    // =====================================================
    // ROLE DROPDOWN
    // =====================================================
    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        return Ok(new[]
        {
            "Admin"
        });
    }
}
