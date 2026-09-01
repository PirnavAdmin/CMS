using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthDemo.DTOs;
namespace AuthDemo.Controllers;

[ApiController]
[Route("api/SuperAdmin")]
[Route("api/dashboard")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuperAdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    [HttpGet("summary")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalClinics = await _context.Hospitals
            .CountAsync(x => x.Email != "platform@cms.local" && x.IsActive);

        var totalAdmins = await _context.Users
            .CountAsync(x => x.Role == "Admin" && x.IsActive);

        var totalUsers = await _context.Users
            .CountAsync(x => x.Role != "SuperAdmin");

        var activeUsers = await _context.Users
            .CountAsync(x => x.Role != "SuperAdmin" && x.IsActive);

        var totalRevenue = await _context.Billings
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        return Ok(new
        {
            totalClinics,
            clinics = totalClinics,
            totalAdmins,
            admins = totalAdmins,
            totalUsers,
            users = totalUsers,
            activeUsers,
            totalRevenue,
            revenue = totalRevenue,
            revenueSummary = totalRevenue,
            revenueMtd = totalRevenue
        });
    }
    // =====================================================
    // ADMIN LOGIN DASHBOARD
    // =====================================================

    [HttpGet("admin-login-dashboard")]
    public async Task<IActionResult> AdminLoginDashboard()
    {
        var totalAdmins = await _context.Users
            .CountAsync(x => x.Role == "Admin" && x.IsActive);

        var onlineAdmins = await _context.AuditLogs
            .CountAsync(x =>
                x.Role == "Admin" &&
                x.IsOnline);

        var todayLogins = await _context.AuditLogs
            .CountAsync(x =>
                x.Role == "Admin" &&
                x.LoginTime.Date == DateTime.UtcNow.Date);

        var lastLogin = await _context.AuditLogs
            .Where(x => x.Role == "Admin")
            .OrderByDescending(x => x.LoginTime)
            .FirstOrDefaultAsync();

        return Ok(new LoginDashboardDto
        {
            TotalUsers = totalAdmins,
            OnlineUsers = onlineAdmins,
            TodayLogins = todayLogins,
            LastLoginUser = lastLogin?.UserName,
            LastLoginTime = lastLogin?.LoginTime
        });
    }
    // =====================================================
    // ONLINE ADMINS
    // =====================================================

    [HttpGet("online-admins")]
    public async Task<IActionResult> OnlineAdmins()
    {
        var admins = await _context.AuditLogs
            .Where(x =>
                x.Role == "Admin" &&
                x.IsOnline)
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new
            {
                x.UserName,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.LoginTime,
                x.ClinicId
            })
            .ToListAsync();

        return Ok(admins);
    }
    // =====================================================
    // ADMIN LOGIN TREND
    // =====================================================

    [HttpGet("admin-login-trend")]
    public async Task<IActionResult> AdminLoginTrend()
    {
        var trend = await _context.AuditLogs
            .Where(x =>
                x.Role == "Admin" &&
                x.LoginTime >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(x => x.LoginTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return Ok(trend);
    }
    // =====================================================
    // HOSPITAL LOGIN REPORT
    // =====================================================

    [HttpGet("hospital-login-report")]
    public async Task<IActionResult> HospitalLoginReport()
    {
        var report = await _context.AuditLogs
            .GroupBy(x => new
            {
                x.ClinicId
            })
            .Select(g => new
            {
                HospitalId = g.Key.ClinicId,
                TotalLogins = g.Count(x => x.Role == "Admin"),
                OnlineAdmins = g.Count(x =>
                    x.Role == "Admin" &&
                    x.IsOnline)
            })
            .ToListAsync();

        return Ok(report);
    }
    // =====================================================
    // ACTIVE ADMIN SESSIONS
    // =====================================================

    [HttpGet("active-admin-sessions")]
    public async Task<IActionResult> ActiveAdminSessions()
    {
        var sessions = await _context.AuditLogs
            .Where(x =>
                x.Role == "Admin" &&
                x.IsOnline)
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new
            {
                x.UserName,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.LoginTime
            })
            .ToListAsync();

        return Ok(sessions);
    }
}
