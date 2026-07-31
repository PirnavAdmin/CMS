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

    // Optional
    public int? PatientId { get; set; }

    public string? TargetUsers { get; set; }
}

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "SuperAdmin,Admin,Doctor,Receptionist")]
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
    // Send Notification
    // =====================================================

    [HttpPost("send")]
    public async Task<IActionResult> Send(NotificationRequest model)
    {
        // If PatientId is supplied, verify patient exists
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
            Title = model.Title,
            Message = model.Message,

            PatientId = model.PatientId,

            IsRead = false,
            IsSent = true,

            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification sent successfully.",
            notification
        });
    }

    // =====================================================
    // Get All Notifications
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _context.Notifications
            .Include(x => x.Patient)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,

                x.Title,

                x.Message,

                PatientId = x.PatientId,

                PatientName = x.Patient != null
                    ? x.Patient.Name
                    : "All Patients",

                x.IsRead,

                x.IsSent,

                x.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    // =====================================================
    // Notification Statistics
    // =====================================================

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var total = await _context.Notifications.CountAsync();

        var unread = await _context.Notifications
            .CountAsync(x => !x.IsRead);

        var read = await _context.Notifications
            .CountAsync(x => x.IsRead);

        return Ok(new
        {
            total,
            read,
            unread
        });
    }

    // =====================================================
    // Delete Notification
    // =====================================================

    [HttpDelete("{id}")]
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
}