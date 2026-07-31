using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using AuthDemo.Data;
using AuthDemo.DTOs;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/login-history")]
[Authorize]
public class LoginHistoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public LoginHistoryController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // SUPER ADMIN
    // VIEW ADMIN LOGIN HISTORY
    // =====================================================

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("admins")]
    public async Task<IActionResult> GetAdminLoginHistory(
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? online = null)
    {
        var query = _context.AuditLogs
            .Where(x => x.Role == "Admin")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.UserName.Contains(search));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x =>
                x.LoginTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x =>
                x.LoginTime <= toDate.Value);
        }

        if (online.HasValue)
        {
            query = query.Where(x =>
                x.IsOnline == online.Value);
        }

        var logs = await query
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new LoginHistoryDto
            {
                UserName = x.UserName,
                Role = x.Role,
                ClinicId = x.ClinicId,
                BranchId = x.BranchId,
                IpAddress = x.IpAddress,
                Browser = x.Browser,
                Device = x.Device,
                LoginTime = x.LoginTime,
                LogoutTime = x.LogoutTime,
                IsOnline = x.IsOnline
            })
            .ToListAsync();

        return Ok(logs);
    }

    // =====================================================
    // ADMIN
    // VIEW DOCTOR & RECEPTIONIST LOGIN HISTORY
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("staff")]
    public async Task<IActionResult> GetStaffLoginHistory(
        string? search = null,
        int? branchId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? online = null)
    {
        var hospitalClaim =
            User.FindFirst("HospitalId")?.Value;

        if (hospitalClaim == null)
        {
            return Unauthorized();
        }

        int hospitalId = int.Parse(hospitalClaim);

        var query = _context.AuditLogs
            .Where(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.UserName.Contains(search));
        }

        if (branchId.HasValue)
        {
            query = query.Where(x =>
                x.BranchId == branchId);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x =>
                x.LoginTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x =>
                x.LoginTime <= toDate.Value);
        }

        if (online.HasValue)
        {
            query = query.Where(x =>
                x.IsOnline == online.Value);
        }

        var logs = await query
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new LoginHistoryDto
            {
                UserName = x.UserName,
                Role = x.Role,
                ClinicId = x.ClinicId,
                BranchId = x.BranchId,
                IpAddress = x.IpAddress,
                Browser = x.Browser,
                Device = x.Device,
                LoginTime = x.LoginTime,
                LogoutTime = x.LogoutTime,
                IsOnline = x.IsOnline
            })
            .ToListAsync();

        return Ok(logs);
    }
}