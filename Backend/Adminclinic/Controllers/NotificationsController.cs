using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // Optional patient-specific notification
    public int? PatientId { get; set; }

    // Examples:
    // "Admin"
    // "Nurse"
    // "Admin,Doctor,Receptionist,Nurse"
    public string? TargetRoles { get; set; }

    // Optional comma-separated User IDs:
    // "3,8,12"
    public string? TargetUsers { get; set; }
}

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "SuperAdmin,Admin,Doctor,Receptionist,Nurse,LabTechnician")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly NotificationService _service;

    public NotificationsController(
        AppDbContext context,
        NotificationService service)
    {
        _context = context;
        _service = service;
    }

    // =====================================================
    // SEND NOTIFICATION
    // SuperAdmin can send to one role, multiple roles,
    // specific users, or a specific patient.
    // =====================================================
    [HttpPost("send")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Send(NotificationRequest model)
    {
        if (model.PatientId.HasValue)
        {
            var patientExists = await _context.Patients
                .AnyAsync(x => x.Id == model.PatientId.Value);

            if (!patientExists)
            {
                return BadRequest(new
                {
                    message = "Patient not found."
                });
            }
        }

        var notification = new Notification
        {
            Title = model.Title?.Trim() ?? string.Empty,

            Message = model.Message?.Trim() ?? string.Empty,

            PatientId = model.PatientId,

            TargetRoles = string.IsNullOrWhiteSpace(model.TargetRoles)
                ? null
                : model.TargetRoles.Trim(),

            TargetUsers = string.IsNullOrWhiteSpace(model.TargetUsers)
                ? null
                : model.TargetUsers.Trim(),

            IsRead = false,

            IsSent = true,

            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification sent successfully.",

            notification = new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.PatientId,
                notification.TargetRoles,
                notification.TargetUsers,
                notification.IsRead,
                notification.IsSent,
                notification.CreatedAt
            }
        });
    }

    // =====================================================
    // GET NOTIFICATIONS
    // SuperAdmin sees all notifications.
    // Other staff only see notifications targeted to their
    // role/user, plus global staff notifications.
    // Patient-specific notifications stay in Patient Portal.
    // =====================================================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var role = GetRole();
        var userId = GetUserId();

        var allNotifications = await _context.Notifications
            .AsNoTracking()
            .Include(x => x.Patient)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        IEnumerable<Notification> visibleNotifications;

        if (string.Equals(
            role,
            "SuperAdmin",
            StringComparison.OrdinalIgnoreCase))
        {
            visibleNotifications = allNotifications;
        }
        else
        {
            visibleNotifications = allNotifications.Where(x =>
            {
                // Patient notifications are handled by
                // /api/patient-portal/notifications
                if (x.PatientId.HasValue)
                {
                    return false;
                }

                var hasRoleTargets =
                    !string.IsNullOrWhiteSpace(x.TargetRoles);

                var hasUserTargets =
                    !string.IsNullOrWhiteSpace(x.TargetUsers);

                // No role/user target = global staff notification
                if (!hasRoleTargets && !hasUserTargets)
                {
                    return true;
                }

                var roleMatch = false;

                if (hasRoleTargets && !string.IsNullOrWhiteSpace(role))
                {
                    var roles = x.TargetRoles!
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries);

                    roleMatch = roles.Any(r =>
                        string.Equals(
                            r,
                            role,
                            StringComparison.OrdinalIgnoreCase));
                }

                var userMatch = false;

                if (hasUserTargets && userId.HasValue)
                {
                    var users = x.TargetUsers!
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries);

                    userMatch = users.Any(u =>
                        int.TryParse(u, out var id) &&
                        id == userId.Value);
                }

                return roleMatch || userMatch;
            });
        }

        var result = visibleNotifications
            .Select(x => new
            {
                x.Id,

                x.Title,

                x.Message,

                x.TargetRoles,

                x.TargetUsers,

                PatientId = x.PatientId,

                PatientName = x.Patient != null
                    ? x.Patient.Name
                    : null,

                x.IsRead,

                x.IsSent,

                x.CreatedAt
            })
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // NOTIFICATION STATISTICS
    // Stats are based on notifications visible to the user.
    // =====================================================
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var role = GetRole();
        var userId = GetUserId();

        var allNotifications = await _context.Notifications
            .AsNoTracking()
            .ToListAsync();

        IEnumerable<Notification> visibleNotifications;

        if (string.Equals(
            role,
            "SuperAdmin",
            StringComparison.OrdinalIgnoreCase))
        {
            visibleNotifications = allNotifications;
        }
        else
        {
            visibleNotifications = allNotifications.Where(x =>
            {
                if (x.PatientId.HasValue)
                {
                    return false;
                }

                var hasRoleTargets =
                    !string.IsNullOrWhiteSpace(x.TargetRoles);

                var hasUserTargets =
                    !string.IsNullOrWhiteSpace(x.TargetUsers);

                if (!hasRoleTargets && !hasUserTargets)
                {
                    return true;
                }

                var roleMatch = false;

                if (hasRoleTargets && !string.IsNullOrWhiteSpace(role))
                {
                    var roles = x.TargetRoles!
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries);

                    roleMatch = roles.Any(r =>
                        string.Equals(
                            r,
                            role,
                            StringComparison.OrdinalIgnoreCase));
                }

                var userMatch = false;

                if (hasUserTargets && userId.HasValue)
                {
                    var users = x.TargetUsers!
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries);

                    userMatch = users.Any(u =>
                        int.TryParse(u, out var id) &&
                        id == userId.Value);
                }

                return roleMatch || userMatch;
            });
        }

        var list = visibleNotifications.ToList();

        return Ok(new
        {
            total = list.Count,
            read = list.Count(x => x.IsRead),
            unread = list.Count(x => !x.IsRead)
        });
    }

    // =====================================================
    // DELETE NOTIFICATION
    // SuperAdmin only
    // =====================================================
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id);

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        _context.Notifications.Remove(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification deleted successfully."
        });
    }

    // =====================================================
    // TOKEN HELPERS
    // =====================================================
    private string? GetRole()
    {
        return User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role");
    }

    private int? GetUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var id)
            ? id
            : null;
    }
}
