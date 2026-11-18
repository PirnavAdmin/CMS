using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.Extensions.Configuration;

namespace AuthDemo.Services;

public class NotificationService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;

    public NotificationService(IConfiguration config, AppDbContext context)
    {
        _config = config;
        _context = context;
    }

    // Existing email functionality, with resource disposal and basic configuration validation.
    public async Task SendEmail(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("Recipient email is required.", nameof(to));

        var host = _config["EmailSettings:Host"];
        var sender = _config["EmailSettings:Email"];
        var password = _config["EmailSettings:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) ||
            string.IsNullOrWhiteSpace(password) ||
            !int.TryParse(_config["EmailSettings:Port"], out var port))
            throw new InvalidOperationException("EmailSettings SMTP configuration is incomplete.");

        using var smtp = new SmtpClient(host)
        {
            Port = port,
            Credentials = new NetworkCredential(sender, password),
            EnableSsl = true
        };
        using var mail = new MailMessage(sender, to, subject, body);
        await smtp.SendMailAsync(mail);
    }

    // For platform subscription events. Super Admin sees these in the dashboard.
    // Optionally include the specific pharmacy Admin as an additional recipient.
    public Task<Notification> SendSubscriptionNotificationAsync(
        string title,
        string message,
        int? pharmacyAdminUserId = null)
    {
        return SaveNotificationAsync(
            title,
            message,
            targetRoles: "SuperAdmin",
            targetUserIds: pharmacyAdminUserId.HasValue
                ? new[] { pharmacyAdminUserId.Value }
                : null);
    }

    // For pharmacy-specific events such as billing, appointments and stock alerts.
    // A concrete recipient is required: TargetRoles="Admin" alone would expose
    // one pharmacy's operational notifications to other pharmacies' Admins.
    public Task<Notification> SendPharmacyAdminNotificationAsync(
        int pharmacyAdminUserId,
        string title,
        string message)
    {
        if (pharmacyAdminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(pharmacyAdminUserId));

        return SaveNotificationAsync(
            title,
            message,
            targetRoles: null,
            targetUserIds: new[] { pharmacyAdminUserId });
    }

    // General targeted notification; no global/untargeted dashboard broadcasts.
    public async Task<Notification> SaveNotificationAsync(
        string title,
        string message,
        string? targetRoles,
        IEnumerable<int>? targetUserIds = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Notification title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Notification message is required.", nameof(message));

        var userIds = targetUserIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (userIds.Any(id => id <= 0))
            throw new ArgumentException("All recipient user IDs must be positive.", nameof(targetUserIds));
        if (string.IsNullOrWhiteSpace(targetRoles) && userIds.Length == 0)
            throw new ArgumentException("A target role or at least one user ID is required.");

        var notification = new Notification
        {
            Title = title.Trim(),
            Message = message.Trim(),
            TargetRoles = string.IsNullOrWhiteSpace(targetRoles) ? null : targetRoles.Trim(),
            TargetUsers = userIds.Length == 0 ? null : string.Join(",", userIds),
            IsRead = false,
            IsSent = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }
}
