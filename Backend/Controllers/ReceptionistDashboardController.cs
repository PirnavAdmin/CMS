using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist")]
public class ReceptionistDashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReceptionistDashboardController(
        AppDbContext context)
    {
        _context = context;
    }

    // =====================================
    // GET HOSPITAL ID FROM TOKEN
    // =====================================

    private int GetHospitalId()
    {
        return int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var hospitalId)
                ? hospitalId
                : 0;
    }

    // =====================================
    // GET BRANCH ID FROM TOKEN
    // =====================================

    private int? GetBranchId()
    {
        return int.TryParse(
            User.FindFirstValue("BranchId"),
            out var branchId)
                ? branchId
                : null;
    }

    // =====================================
    // DASHBOARD
    // Receptionist sees only own branch
    // =====================================

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var hospitalId = GetHospitalId();
        var branchId = GetBranchId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message =
                    "HospitalId is missing from authentication token."
            });
        }

        if (!branchId.HasValue ||
            branchId.Value <= 0)
        {
            return Forbid();
        }

        var today = DateTime.Today;

        var appointments =
            _context.Appointments
                .Where(x =>
                    x.HospitalId == hospitalId &&
                    x.BranchId == branchId.Value &&
                    x.Date.Date == today
                );

        var totalToday =
            await appointments.CountAsync();

        var waiting =
            await appointments.CountAsync(x =>
                x.Status == "Waiting");

        var confirmed =
            await appointments.CountAsync(x =>
                x.Status == "Confirmed");

        var inProgress =
            await appointments.CountAsync(x =>
                x.Status == "InProgress");

        var completed =
            await appointments.CountAsync(x =>
                x.Status == "Completed");

        return Ok(new
        {
            hospitalId,
            branchId,

            totalTodayAppointments =
                totalToday,

            waitingAppointments =
                waiting,

            confirmedAppointments =
                confirmed,

            inProgressAppointments =
                inProgress,

            completedAppointments =
                completed
        });
    }
}