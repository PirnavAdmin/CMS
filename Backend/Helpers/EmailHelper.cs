using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuthDemo.Helpers;

public class EmailHelper
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailHelper>? _logger;

    public EmailHelper(IConfiguration config, ILogger<EmailHelper>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    public async Task SendEmail(string toEmail, string otp)
    {
        if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 ||
            !System.Linq.Enumerable.All(otp, char.IsDigit))
            throw new ArgumentException("A six-digit OTP is required.", nameof(otp));

        var body = $@"
            <h2>Clinic Management System - Password Verification</h2>
            <p>Use this one-time code to verify your email and set or reset your password:</p>
            <h1 style=""letter-spacing:4px"">{WebUtility.HtmlEncode(otp)}</h1>
            <p>This code expires in <strong>10 minutes</strong>.</p>
            <p>If you did not request this code, you can ignore this email.</p>
            <p>Regards,<br/>Clinic Management Team</p>";

        await SendHtmlAsync(toEmail, "Clinic Management System - Password Verification OTP", body);
    }

    public async Task SendAdminRegistrationOtp(string toEmail, string otp)
    {
        if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 ||
            !System.Linq.Enumerable.All(otp, char.IsDigit))
            throw new ArgumentException("A six-digit OTP is required.", nameof(otp));

        var body = $@"
            <h2>Verify your CMS Admin account</h2>
            <p>Use this one-time code to verify your email address:</p>
            <h1 style=""letter-spacing:4px"">{WebUtility.HtmlEncode(otp)}</h1>
            <p>This code expires in <strong>10 minutes</strong>.</p>
            <p>If you did not create this account, ignore this email.</p>
            <p>Regards,<br/>Clinic Management Team</p>";

        await SendHtmlAsync(toEmail, "Verify your CMS Admin account", body);
    }

    public async Task SendAdminCredentials(string toEmail, string tempPassword)
    {
        if (string.IsNullOrWhiteSpace(tempPassword))
            throw new ArgumentException("Temporary password is required.", nameof(tempPassword));

        var loginUrl = FirstNonEmpty("App:AdminLoginUrl", "Smtp:AdminLoginUrl");
        var loginLine = Uri.TryCreate(loginUrl, UriKind.Absolute, out var parsedUrl) &&
                        (parsedUrl.Scheme == Uri.UriSchemeHttps || parsedUrl.Scheme == Uri.UriSchemeHttp)
            ? $"<p>Login page: <a href=\"{WebUtility.HtmlEncode(parsedUrl.AbsoluteUri)}\">{WebUtility.HtmlEncode(parsedUrl.AbsoluteUri)}</a></p>"
            : string.Empty;

        var body = $@"
            <h2>Welcome to Clinic Management System</h2>
            <p>Your Admin account has been created successfully.</p>
            <p><strong>Email:</strong> {WebUtility.HtmlEncode(toEmail)}</p>
            <p><strong>Temporary Password:</strong> {WebUtility.HtmlEncode(tempPassword)}</p>
            {loginLine}
            <p>Please log in and change your password immediately.</p>
            <p>If you did not expect this account, contact your administrator.</p>
            <p>Regards,<br/>Clinic Management Team</p>";

        await SendHtmlAsync(toEmail, "Welcome to Clinic Management System", body);
    }

    public async Task SendStaffCredentials(
        string toEmail, string name, string role, string temporaryPassword)
    {
        if (string.IsNullOrWhiteSpace(temporaryPassword))
            throw new ArgumentException("Temporary password is required.", nameof(temporaryPassword));

        var body = $@"
            <h2>Welcome to Clinic Management System</h2>
            <p>Dear {WebUtility.HtmlEncode(name)},</p>
            <p>Your <strong>{WebUtility.HtmlEncode(role)}</strong> account has been created successfully.</p>
            <p><strong>Email:</strong> {WebUtility.HtmlEncode(toEmail)}</p>
            <p><strong>Temporary Password:</strong> {WebUtility.HtmlEncode(temporaryPassword)}</p>
            <p>Please log in and change your password immediately.</p>
            <p>Regards,<br/>Clinic Management Team</p>";

        await SendHtmlAsync(toEmail, $"{role} account created - Clinic Management System", body);
    }

    public async Task SendAppointmentConfirmation(
        string toEmail,
        string patientName,
        string hospitalName,
        string DoctorName,
        string tokenNumber,
        DateTime appointmentDate,
        TimeSpan appointmentTime)
    {
        var safeHospital = WebUtility.HtmlEncode(hospitalName);
        var formattedDate = appointmentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var formattedTime = appointmentTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        var body = $@"
            <h2>Appointment Confirmation</h2>
            <p>Dear {WebUtility.HtmlEncode(patientName)},</p>
            <p>Your appointment has been booked successfully.</p>
            <p><strong>Hospital:</strong> {safeHospital}</p>
            <p><strong>Token Number:</strong> {WebUtility.HtmlEncode(tokenNumber)}</p>
            <p><strong>Doctor Name:</strong> {WebUtility.HtmlEncode(DoctorName)}</p>
            <p><strong>Date:</strong> {formattedDate}</p>
            <p><strong>Time:</strong> {formattedTime}</p>
            <p>Thank you,<br/>{safeHospital}</p>";

        await SendHtmlAsync(toEmail, "Appointment Booked Successfully", body);
    }

    private string? FirstNonEmpty(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }

    private async Task SendHtmlAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        var username = FirstNonEmpty("Smtp:Username", "EmailSettings:Username", "EmailSettings:Email");
        var password = FirstNonEmpty("Smtp:Password", "EmailSettings:Password");
        var host = FirstNonEmpty("Smtp:Host", "EmailSettings:Host");
        var portText = FirstNonEmpty("Smtp:Port", "EmailSettings:Port");
        var fromEmail = FirstNonEmpty("Smtp:FromEmail", "EmailSettings:FromEmail", "EmailSettings:Email") ?? username;
        var fromName = FirstNonEmpty("Smtp:FromName", "EmailSettings:FromName") ?? "Clinic Management System";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail) ||
            !int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            _logger?.LogError(
                "SMTP settings missing or invalid. HostPresent={HostPresent}, PortValid={PortValid}, UsernamePresent={UsernamePresent}, PasswordPresent={PasswordPresent}, FromPresent={FromPresent}",
                !string.IsNullOrWhiteSpace(host),
                int.TryParse(portText, out var configuredPort) && configuredPort > 0 && configuredPort <= 65535,
                !string.IsNullOrWhiteSpace(username),
                !string.IsNullOrWhiteSpace(password),
                !string.IsNullOrWhiteSpace(fromEmail));
            throw new InvalidOperationException("SMTP settings are missing or invalid. Check application configuration.");
        }

        // Default: port 465 uses implicit TLS; other ports require STARTTLS.
        // Optional Smtp:Security values: Auto, StartTls, SslOnConnect.
        var securityText = FirstNonEmpty("Smtp:Security", "EmailSettings:Security");
        var security = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        if (!string.IsNullOrWhiteSpace(securityText))
        {
            if (securityText.Equals("SslOnConnect", StringComparison.OrdinalIgnoreCase) ||
                securityText.Equals("Ssl", StringComparison.OrdinalIgnoreCase))
                security = SecureSocketOptions.SslOnConnect;
            else if (securityText.Equals("StartTls", StringComparison.OrdinalIgnoreCase))
                security = SecureSocketOptions.StartTls;
            else if (securityText.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                security = SecureSocketOptions.Auto;
            else
                throw new InvalidOperationException("Invalid Smtp:Security value. Use Auto, StartTls, or SslOnConnect.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var smtp = new SmtpClient { Timeout = 30000 };
        try
        {
            _logger?.LogInformation("Connecting to SMTP host {Host}:{Port} with {Security}", host, port, security);
            await smtp.ConnectAsync(host, port, security);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
            _logger?.LogInformation("SMTP accepted message for recipient; subject {Subject}", subject);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            _logger?.LogError(ex, "SMTP authentication failed at {Host}:{Port}. Verify username, app password and account security settings.", host, port);
            throw new InvalidOperationException("SMTP authentication failed. Check the configured username and app password.", ex);
        }
        catch (SmtpCommandException ex)
        {
            _logger?.LogError(ex, "SMTP server rejected command at {Host}:{Port}; status {StatusCode}; error {ErrorCode}", host, port, ex.StatusCode, ex.ErrorCode);
            throw new InvalidOperationException("SMTP server rejected the email. Check application logs for the server response.", ex);
        }
        catch (SmtpProtocolException ex)
        {
            _logger?.LogError(ex, "SMTP protocol error at {Host}:{Port}", host, port);
            throw new InvalidOperationException("SMTP protocol error. Verify host, port and TLS mode.", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SMTP connection or send failed at {Host}:{Port} using {Security}", host, port, security);
            throw new InvalidOperationException("Email could not be sent. Check SMTP connectivity and application logs.", ex);
        }
    }
}
