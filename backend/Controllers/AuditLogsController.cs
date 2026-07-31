using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuditLogsController(AppDbContext context)
    {
        _context = context;
    }

    private int? GetHospitalId()
    {
        return int.TryParse(User.FindFirstValue("HospitalId"), out var id)
            ? id
            : null;
    }

    private string GetRole()
    {
        return User.FindFirstValue(ClaimTypes.Role)
               ?? User.FindFirstValue("role")
               ?? string.Empty;
    }

    private IQueryable<AuditLog> GetHospitalScopedQuery()
    {
        var query = _context.AuditLogs.AsNoTracking();
        var role = GetRole();
        var hospitalId = GetHospitalId();

        if (role == "Admin" && hospitalId.HasValue)
        {
            query = query.Where(x => x.ClinicId == hospitalId.Value);
        }

        return query;
    }

    // Admin can filter audit logs by a branch belonging to the same hospital.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? branchId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int take = 100)
    {
        var hospitalId = GetHospitalId();

        if (GetRole() == "Admin" && !hospitalId.HasValue)
            return Unauthorized(new { message = "Hospital information is missing from token" });

        if (branchId.HasValue && GetRole() == "Admin")
        {
            var branchExists = await _context.Branches.AnyAsync(x =>
                x.Id == branchId.Value && x.HospitalId == hospitalId!.Value);

            if (!branchExists)
                return NotFound(new { message = "Branch not found in your hospital" });
        }

        take = Math.Clamp(take, 1, 500);

        var query = GetHospitalScopedQuery()
            .Where(x => !x.IsLoginActivity);

        if (branchId.HasValue)
            query = query.Where(x => x.BranchId == branchId.Value);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Role,
                hospitalId = x.ClinicId,
                x.BranchId,
                branchName = x.Branch != null ? x.Branch.Name : "Unassigned",
                x.Action,
                x.SystemAction,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("login-history")]
    public async Task<IActionResult> LoginHistory(
        [FromQuery] int? branchId = null,
        [FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);

        var query = GetHospitalScopedQuery()
            .Where(x => x.IsLoginActivity);

        if (branchId.HasValue)
            query = query.Where(x => x.BranchId == branchId.Value);

        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Role,
                x.BranchId,
                branchName = x.Branch != null ? x.Branch.Name : "Unassigned",
                x.Action,
                x.SystemAction,
                x.LoginTime,
                x.LogoutTime,
                x.IsOnline,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }

    // Dashboard endpoint: one summary card/row per branch in the Admin's hospital.
    [HttpGet("dashboard/branch-wise")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetBranchWiseDashboard(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int recentPerBranch = 5)
    {
        var hospitalId = GetHospitalId();
        if (!hospitalId.HasValue)
            return Unauthorized(new { message = "Hospital information is missing from token" });

        recentPerBranch = Math.Clamp(recentPerBranch, 1, 20);
        var start = from ?? DateTime.UtcNow.Date;
        var end = to ?? DateTime.UtcNow;

        if (end < start)
            return BadRequest(new { message = "The 'to' date must be after the 'from' date" });

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId.Value)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.ClinicId == hospitalId.Value &&
                x.Timestamp >= start &&
                x.Timestamp <= end)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                x.Id,
                x.BranchId,
                x.UserName,
                x.Role,
                x.Action,
                x.SystemAction,
                x.IsLoginActivity,
                x.IsOnline,
                x.Timestamp
            })
            .ToListAsync();

        var result = branches.Select(branch =>
        {
            var branchLogs = logs.Where(x => x.BranchId == branch.Id).ToList();
            return new
            {
                branchId = branch.Id,
                branchName = branch.Name,
                totalActivities = branchLogs.Count,
                loginActivities = branchLogs.Count(x => x.IsLoginActivity),
                businessActivities = branchLogs.Count(x => !x.IsLoginActivity),
                onlineUsers = branchLogs
                    .Where(x => x.IsLoginActivity && x.IsOnline)
                    .Select(x => $"{x.Role}:{x.UserName}")
                    .Distinct()
                    .Count(),
                vitalsUpdates = branchLogs.Count(x => x.Action == "Update Patient Vitals"),
                lastActivityAt = branchLogs.Select(x => (DateTime?)x.Timestamp).FirstOrDefault(),
                recentActivities = branchLogs
                    .Take(recentPerBranch)
                    .Select(x => new
                    {
                        x.Id,
                        x.UserName,
                        x.Role,
                        x.Action,
                        x.SystemAction,
                        x.Timestamp
                    })
            };
        }).ToList();

        var unassignedLogs = logs.Where(x => !x.BranchId.HasValue).ToList();

        return Ok(new
        {
            hospitalId,
            from = start,
            to = end,
            totals = new
            {
                branches = branches.Count,
                activities = logs.Count,
                loginActivities = logs.Count(x => x.IsLoginActivity),
                businessActivities = logs.Count(x => !x.IsLoginActivity),
                vitalsUpdates = logs.Count(x => x.Action == "Update Patient Vitals"),
                unassignedActivities = unassignedLogs.Count
            },
            branchWise = result
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var log = await GetHospitalScopedQuery()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Role,
                hospitalId = x.ClinicId,
                x.BranchId,
                branchName = x.Branch != null ? x.Branch.Name : "Unassigned",
                x.Action,
                x.SystemAction,
                x.IsLoginActivity,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.LoginTime,
                x.LogoutTime,
                x.IsOnline,
                x.Timestamp
            })
            .FirstOrDefaultAsync();

        return log == null ? NotFound() : Ok(log);
    }

    // Kept for internal/admin use, but the hospital and user identity are taken from the token.
    [HttpPost]
    public async Task<IActionResult> Create(AuditLog model)
    {
        var hospitalId = GetHospitalId();
        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : 0;

        if (GetRole() == "Admin" && !hospitalId.HasValue)
            return Unauthorized(new { message = "Hospital information is missing from token" });

        if (model.BranchId.HasValue && hospitalId.HasValue)
        {
            var validBranch = await _context.Branches.AnyAsync(x =>
                x.Id == model.BranchId.Value && x.HospitalId == hospitalId.Value);

            if (!validBranch)
                return BadRequest(new { message = "Invalid branch for this hospital" });
        }

        model.UserId = userId;
        model.UserName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Admin";
        model.Role = GetRole();
        model.ClinicId = hospitalId ?? model.ClinicId;
        model.Timestamp = DateTime.UtcNow;

        _context.AuditLogs.Add(model);
        await _context.SaveChangesAsync();

        return Ok(model);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var log = await _context.AuditLogs.FindAsync(id);
        if (log == null)
            return NotFound();

        _context.AuditLogs.Remove(log);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Audit log deleted successfully" });
    }
}
