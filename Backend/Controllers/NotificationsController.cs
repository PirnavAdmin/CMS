using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public class NotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public string? TargetRoles { get; set; }
    public string? TargetUsers { get; set; }
}

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "SuperAdmin,Admin,Doctor,Receptionist,Nurse,LabTechnician,Pharmacist")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("send")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Send([FromBody] NotificationRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Message))
            return BadRequest(new { message = "Title and message are required." });

        if (model.PatientId.HasValue &&
            !await _context.Patients.AnyAsync(x => x.Id == model.PatientId.Value))
            return BadRequest(new { message = "Patient not found." });

        var userIds = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(model.TargetUsers))
        {
            foreach (var part in model.TargetUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out var id) || id <= 0)
                    return BadRequest(new { message = "TargetUsers must contain valid positive integer user IDs." });
                userIds.Add(id);
            }

            var validCount = await _context.Users.CountAsync(x => userIds.Contains(x.Id) && x.IsActive);
            if (validCount != userIds.Count)
                return BadRequest(new { message = "One or more target users do not exist or are inactive." });
        }

        var roles = (model.TargetRoles ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (!model.PatientId.HasValue && userIds.Count == 0 && roles.Length == 0)
            return BadRequest(new { message = "Specify PatientId, TargetUsers, or TargetRoles." });

        // A role alone has no pharmacy/tenant boundary in the Notification schema.
        // Require explicit recipients for staff notifications rather than broadcasting
        // to all pharmacies that happen to share a role.
        if (roles.Any(x => !string.Equals(x, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            && userIds.Count == 0)
            return BadRequest(new { message = "Staff role notifications require explicit TargetUsers to preserve pharmacy isolation." });

        // If both users and roles are specified, ensure the recipients really hold
        // one of those roles; this prevents accidental cross-role delivery.
        if (userIds.Count > 0 && roles.Length > 0)
        {
            var validRoleRecipients = await _context.Users.CountAsync(x =>
                userIds.Contains(x.Id) && x.IsActive && roles.Contains(x.Role));
            if (validRoleRecipients != userIds.Count)
                return BadRequest(new { message = "Every TargetUsers recipient must have one of the specified TargetRoles." });
        }

        // A notification addressed to a patient is for the patient portal; it
        // must not be made visible to staff simply by adding role/user targets.
        if (model.PatientId.HasValue && (userIds.Count > 0 || roles.Length > 0))
            return BadRequest(new { message = "Patient notifications cannot also target staff users or roles." });

        var notification = new Notification
        {
            Title = model.Title.Trim(),
            Message = model.Message.Trim(),
            PatientId = model.PatientId,
            TargetRoles = roles.Length == 0 ? null : string.Join(',', roles),
            TargetUsers = userIds.Count == 0 ? null : string.Join(',', userIds.OrderBy(x => x)),
            IsRead = false,
            IsSent = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Notification sent successfully.", notification });
    }

    // Compatibility with existing subscription notifications. A dedicated
    // Category column is preferable to text matching for long-term routing.
    private static bool IsSubscriptionNotification(Notification item)
    {
        var title = item.Title ?? "";
        var message = item.Message ?? "";
        return title.Contains("subscription", StringComparison.OrdinalIgnoreCase)
            || title.Contains("plan renewal", StringComparison.OrdinalIgnoreCase)
            || title.Contains("plan expired", StringComparison.OrdinalIgnoreCase)
            || title.Contains("plan activated", StringComparison.OrdinalIgnoreCase)
            || title.Contains("plan expiring", StringComparison.OrdinalIgnoreCase)
            || (message.Contains("subscription", StringComparison.OrdinalIgnoreCase)
                && !title.Contains("pharmacy bill", StringComparison.OrdinalIgnoreCase));
    }

    private bool CanView(Notification item)
    {
        if (item.PatientId.HasValue) return false;
        var role = GetRole();
        var userId = GetUserId();
        if (userId == null) return false;

        var roles = (item.TargetRoles ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var users = (item.TargetUsers ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var roleMatch = roles.Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase));
        // TargetUsers identifies Users records, not SuperAdmins records.
        var userMatch = !string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            && users.Any(x => int.TryParse(x, out var id) && id == userId.Value);

        // An explicit user list is authoritative; never let TargetRoles
        // expand its audience to users who were not individually addressed.
        if (users.Length > 0)
            return userMatch;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return IsSubscriptionNotification(item) && (roleMatch || roles.Length == 0);

        // Without a pharmacy/tenant ID on the notification, role-only operational
        // broadcasts cannot safely be scoped. Send to explicit user IDs instead.
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();

        // Reduce the data fetched before applying the final audience check.
        // TargetUsers is stored as CSV, so the final exact-ID match is performed
        // by CanView rather than an unsafe substring match in SQL.
        var role = GetRole();
        var all = await _context.Notifications.AsNoTracking()
            .Where(x => x.PatientId == null &&
                (x.TargetUsers != null || (role == "SuperAdmin" &&
                    (x.TargetRoles == null || x.TargetRoles.Contains("SuperAdmin")))))
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        var visible = all.Where(CanView).ToList();
        var ids = visible.Select(x => x.Id).ToArray();
        var isSuperAdmin = IsSuperAdmin();
        var readIds = await _context.NotificationReads.AsNoTracking()
            .Where(x => (isSuperAdmin ? x.SuperAdminId == userId.Value : x.UserId == userId.Value)
                && ids.Contains(x.NotificationId))
            .Select(x => x.NotificationId).ToListAsync();
        var readSet = readIds.ToHashSet();

        return Ok(visible.Select(x => new
        {
            x.Id,
            x.Title,
            x.Message,
            x.TargetRoles,
            x.TargetUsers,
            x.PatientId,
            PatientName = (string?)null,
            IsRead = readSet.Contains(x.Id),
            x.IsSent,
            x.CreatedAt
        }).ToList());
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var role = GetRole();
        var all = await _context.Notifications.AsNoTracking()
            .Where(x => x.PatientId == null &&
                (x.TargetUsers != null || (role == "SuperAdmin" &&
                    (x.TargetRoles == null || x.TargetRoles.Contains("SuperAdmin")))))
            .ToListAsync();
        var ids = all.Where(CanView).Select(x => x.Id).ToArray();
        var isSuperAdmin = IsSuperAdmin();
        var readIds = await _context.NotificationReads.AsNoTracking()
            .Where(x => (isSuperAdmin ? x.SuperAdminId == userId.Value : x.UserId == userId.Value)
                && ids.Contains(x.NotificationId))
            .Select(x => x.NotificationId)
            .Distinct()
            .ToListAsync();
        var read = readIds.Count;
        return Ok(new { total = ids.Length, read, unread = ids.Length - read });
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var notification = await _context.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (notification == null || !CanView(notification))
            return NotFound(new { message = "Notification not found." });

        var isSuperAdmin = IsSuperAdmin();
        var accountExists = isSuperAdmin
            ? await _context.SuperAdmins.AnyAsync(x => x.Id == userId.Value && x.IsActive)
            : await _context.Users.AnyAsync(x => x.Id == userId.Value && x.IsActive);
        if (!accountExists)
            return Unauthorized(new { message = "Authenticated account not found or inactive." });

        var alreadyRead = await _context.NotificationReads.AsNoTracking()
            .AnyAsync(x => x.NotificationId == id &&
                (isSuperAdmin ? x.SuperAdminId == userId.Value : x.UserId == userId.Value));
        if (!alreadyRead)
        {
            var read = new NotificationRead
            {
                NotificationId = id,
                UserId = isSuperAdmin ? null : userId.Value,
                SuperAdminId = isSuperAdmin ? userId.Value : null,
                ReadAt = DateTime.UtcNow
            };
            _context.NotificationReads.Add(read);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(read).State = EntityState.Detached;
                // Two concurrent requests may race against the unique index.
                var existsNow = await _context.NotificationReads.AsNoTracking()
                    .AnyAsync(x => x.NotificationId == id &&
                        (isSuperAdmin ? x.SuperAdminId == userId.Value : x.UserId == userId.Value));
                if (!existsNow) throw;
            }
        }
        return Ok(new { message = "Notification marked as read.", notificationId = id, isRead = true });
    }

    [HttpPatch("{id:int}/unread")]
    public async Task<IActionResult> MarkUnread(int id)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return Unauthorized();
        var notification = await _context.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (notification == null || !CanView(notification))
            return NotFound(new { message = "Notification not found." });

        var isSuperAdmin = IsSuperAdmin();
        var accountExists = isSuperAdmin
            ? await _context.SuperAdmins.AnyAsync(x => x.Id == userId.Value && x.IsActive)
            : await _context.Users.AnyAsync(x => x.Id == userId.Value && x.IsActive);
        if (!accountExists)
            return Unauthorized(new { message = "Authenticated account not found or inactive." });

        var record = await _context.NotificationReads
            .FirstOrDefaultAsync(x => x.NotificationId == id &&
                (isSuperAdmin ? x.SuperAdminId == userId.Value : x.UserId == userId.Value));
        if (record != null)
        {
            _context.NotificationReads.Remove(record);
            await _context.SaveChangesAsync();
        }
        return Ok(new { message = "Notification marked as unread.", notificationId = id, isRead = false });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id);
        if (notification == null || !CanView(notification))
            return NotFound(new { message = "Notification not found." });
        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Notification deleted successfully." });
    }

    private string? GetRole() => User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
    private bool IsSuperAdmin() => string.Equals(GetRole(), "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }
}
