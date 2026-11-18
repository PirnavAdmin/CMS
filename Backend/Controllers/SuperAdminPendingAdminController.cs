using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/superadmin/pending-admin-creations")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminPendingAdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SuperAdminPendingAdminController> _logger;

    public SuperAdminPendingAdminController(
        AppDbContext context,
        IAuthService authService,
        IConfiguration configuration,
        ILogger<SuperAdminPendingAdminController> logger)
    {
        _context = context;
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
    }

    // ============================================================
    // GET ALL PENDING ADMIN CREATIONS
    // GET /api/superadmin/pending-admin-creations
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetPendingAdminCreations()
    {
        var purchases = await _context.SubscriptionPurchases
            .AsNoTracking()
            .Where(x =>
                x.PurchaseType == "Initial" &&
                x.PaymentStatus == "Paid" &&
                x.PaidAt.HasValue &&
                x.IsGatewayPaymentVerified &&
                x.GatewayVerifiedAt.HasValue &&
                (x.GatewayMode == "Test" || x.GatewayMode == "Live") &&
                x.GatewayOrderId != null && x.GatewayOrderId != "" &&
                x.GatewayPaymentId != null && x.GatewayPaymentId != "" &&
                x.AdminCreationStatus == "Pending" &&
                x.AdminUserId == null &&
                x.HospitalId == null &&
                x.ActivatedAt == null &&
                x.PurchasedIncludesCms &&
                (x.PurchasedDurationMonths == 1 ||
                 x.PurchasedDurationMonths == 3 ||
                 x.PurchasedDurationMonths == 6 ||
                 x.PurchasedDurationMonths == 12) &&
                x.Amount > 0)
            .OrderByDescending(x => x.PaidAt)
            .Select(x => new
            {
                purchaseId = x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,

                x.SubscriptionPlanId,
                planName = x.SubscriptionPlan.Name,

                durationMonths = x.PurchasedDurationMonths,

                includesCms = x.PurchasedIncludesCms,
                includesLab = x.PurchasedIncludesLab,
                includesPharmacy = x.PurchasedIncludesPharmacy,

                x.Amount,
                currency = "INR",

                x.PaymentMethod,
                x.PaymentStatus,
                x.GatewayPaymentId,
                x.TransactionReference,
                x.PaymentAttemptCount,
                x.LastPaymentAttemptAt,

                x.AdminCreationStatus,
                x.AdminUserId,
                x.HospitalId,

                x.CreatedAt,
                x.PaidAt,
                eligibleForAdminCreation = true
            })
            .ToListAsync();

        return Ok(new
        {
            totalCount = purchases.Count,
            purchases
        });
    }

    // ============================================================
    // GET PURCHASE DETAILS BY ID
    // GET /api/superadmin/pending-admin-creations/{id}
    // ============================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPendingAdminCreation(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid purchase ID is required."
            });
        }

        var purchase = await _context.SubscriptionPurchases
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                purchaseId = x.Id,

                x.Name,
                x.Email,
                x.MobileNumber,

                x.SubscriptionPlanId,
                planName = x.SubscriptionPlan.Name,

                durationMonths = x.PurchasedDurationMonths,

                includesCms = x.PurchasedIncludesCms,
                includesLab = x.PurchasedIncludesLab,
                includesPharmacy = x.PurchasedIncludesPharmacy,

                x.Amount,
                currency = "INR",

                x.PaymentMethod,
                x.PaymentStatus,

                x.GatewayOrderId,
                x.GatewayPaymentId,
                x.TransactionReference,
                x.PaymentFailureReason,
                x.PaymentAttemptCount,
                x.LastPaymentAttemptAt,
                eligibleForAdminCreation =
                    x.PurchaseType == "Initial" &&
                    x.PaymentStatus == "Paid" &&
                    x.PaidAt.HasValue &&
                    x.IsGatewayPaymentVerified &&
                    x.GatewayVerifiedAt.HasValue &&
                    (x.GatewayMode == "Test" || x.GatewayMode == "Live") &&
                    x.GatewayOrderId != null && x.GatewayOrderId != "" &&
                    x.GatewayPaymentId != null && x.GatewayPaymentId != "" &&
                    x.AdminCreationStatus == "Pending" &&
                    !x.AdminUserId.HasValue &&
                    !x.HospitalId.HasValue &&
                    !x.ActivatedAt.HasValue &&
                    x.PurchasedIncludesCms &&
                    (x.PurchasedDurationMonths == 1 ||
                     x.PurchasedDurationMonths == 3 ||
                     x.PurchasedDurationMonths == 6 ||
                     x.PurchasedDurationMonths == 12) &&
                    x.Amount > 0,

                x.AdminCreationStatus,
                x.AdminUserId,
                x.HospitalId,

                x.CreatedAt,
                x.PaidAt,
                x.ActivatedAt
            })
            .FirstOrDefaultAsync();

        if (purchase == null)
        {
            return NotFound(new
            {
                message = "Subscription purchase not found."
            });
        }

        return Ok(purchase);
    }

    // ============================================================
    // CREATE ADMIN FROM PAID PURCHASE
    // POST /api/superadmin/pending-admin-creations/{id}/create-admin
    //
    // Super Admin creates Admin.
    // Admin creates their clinic after logging in.
    // ============================================================

    [HttpPost("{id:int}/create-admin")]
    public async Task<IActionResult> CreateAdmin(int id)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "Valid purchase ID is required."
            });
        }

        User admin;
        int purchaseId;

        bool purchasedIncludesCms;
        bool purchasedIncludesLab;
        bool purchasedIncludesPharmacy;
        int purchasedDurationMonths;
        decimal purchasedAmount;
        string temporaryPassword;

        // Serializable transaction prevents concurrent creation
        // of multiple Admins for the same purchase.
        await using (var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable))
        {
            // Lock this purchase row until commit so two simultaneous requests
            // cannot create separate Admin accounts for the same purchase.
            var purchase = await _context.SubscriptionPurchases
                .FromSqlInterpolated($"SELECT * FROM SubscriptionPurchases WHERE Id = {id} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (purchase == null)
            {
                return NotFound(new
                {
                    message = "Subscription purchase not found."
                });
            }

            if (!string.Equals(
                    purchase.PaymentStatus,
                    "Paid",
                    StringComparison.OrdinalIgnoreCase) ||
                !purchase.PaidAt.HasValue)
            {
                return Conflict(new
                {
                    message =
                        "A confirmed paid purchase is required before creating an Admin."
                });
            }

            // PAYMENTS - PHASE 5B: INITIAL ADMIN CREATION SECURITY.
            // A demo/simulated Paid flag is not captured-payment proof.
            if (purchase.PurchaseType != "Initial" ||
                !purchase.IsGatewayPaymentVerified ||
                !purchase.GatewayVerifiedAt.HasValue ||
                (purchase.GatewayMode != "Test" && purchase.GatewayMode != "Live") ||
                string.IsNullOrWhiteSpace(purchase.GatewayOrderId) ||
                string.IsNullOrWhiteSpace(purchase.GatewayPaymentId))
            {
                return Conflict(new
                {
                    message = "Initial Admin creation requires a verified Razorpay payment.",
                    purchaseId = purchase.Id,
                    isGatewayPaymentVerified = purchase.IsGatewayPaymentVerified
                });
            }

            if (purchase.AdminCreationStatus != "Pending" ||
                purchase.AdminUserId.HasValue)
            {
                return Conflict(new
                {
                    message =
                        "An Admin has already been created or this purchase is not pending."
                });
            }

            if (purchase.HospitalId.HasValue ||
                purchase.ActivatedAt.HasValue)
            {
                return Conflict(new
                {
                    message =
                        "This purchase is already linked to or activated for a clinic."
                });
            }

            // CMS is mandatory for all supported subscription plans.
            if (!purchase.PurchasedIncludesCms)
            {
                return Conflict(new
                {
                    message =
                        "Invalid subscription purchase. CMS must be included."
                });
            }

            // Only the four configured durations are supported.
            if (purchase.PurchasedDurationMonths != 1 &&
                purchase.PurchasedDurationMonths != 3 &&
                purchase.PurchasedDurationMonths != 6 &&
                purchase.PurchasedDurationMonths != 12)
            {
                return Conflict(new
                {
                    message =
                        "Invalid purchased subscription duration."
                });
            }

            if (purchase.Amount <= 0)
            {
                return Conflict(new
                {
                    message =
                        "Invalid purchased subscription amount."
                });
            }

            var email = (purchase.Email ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            var name = (purchase.Name ?? string.Empty).Trim();
            var mobile = (purchase.MobileNumber ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 150 ||
                email.Length > 200 ||
                !new System.ComponentModel.DataAnnotations
                    .EmailAddressAttribute()
                    .IsValid(email) ||
                !Regex.IsMatch(mobile, @"^[6-9]\d{9}$"))
            {
                return BadRequest(new
                {
                    message = "Purchase name, email, or mobile number is invalid."
                });
            }

            var emailExists = await _context.Users
                .AnyAsync(x =>
                    x.Email != null &&
                    x.Email.ToLower() == email);

            if (emailExists)
            {
                return Conflict(new
                {
                    message =
                        "An account already exists with this email. Resolve the existing account before creating an Admin."
                });
            }

            // Generate a strong temporary password. Only its hash is stored in the database.
            temporaryPassword = GenerateTemporaryPassword();

            admin = new User
            {
                Name = name,
                Email = email,
                MobileNumber = mobile,

                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(temporaryPassword),

                Role = "Admin",

                HospitalId = null,
                BranchId = null,
                DoctorId = null,

                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(admin);
            await _context.SaveChangesAsync();

            // Link the new Admin to the paid purchase.
            purchase.AdminUserId = admin.Id;
            purchase.AdminCreationStatus = "Created";

            // Keep the original purchase snapshot unchanged.
            purchasedIncludesCms = purchase.PurchasedIncludesCms;
            purchasedIncludesLab = purchase.PurchasedIncludesLab;
            purchasedIncludesPharmacy =
                purchase.PurchasedIncludesPharmacy;

            purchasedDurationMonths =
                purchase.PurchasedDurationMonths;

            purchasedAmount = purchase.Amount;

            purchaseId = purchase.Id;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Send the welcome email only after the database transaction commits.
        // If SMTP is unavailable, the Admin remains created and the response reports the failure.
        var welcomeEmailSent = false;
        string? welcomeEmailError = null;

        try
        {
            await SendTemporaryPasswordEmailAsync(
                admin.Name ?? "Admin",
                admin.Email!,
                temporaryPassword);

            welcomeEmailSent = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Welcome email failed for Admin {AdminId}", admin.Id);
            // Do not expose SMTP credentials or internal exception details to the client.
            welcomeEmailError = ex is SmtpException
                ? "Email server rejected or could not send the message."
                : "Welcome email could not be sent. Check the SMTP configuration.";
        }

        return Ok(new
        {
            message = welcomeEmailSent
                ? "Admin created successfully. A welcome email with the login email and temporary password was sent."
                : "Admin created, but the welcome email could not be sent. Check SMTP settings and retry sending the credentials securely.",

            purchaseId,

            adminUserId = admin.Id,
            adminName = admin.Name,
            adminEmail = admin.Email,

            role = admin.Role,

            hospitalId = admin.HospitalId,
            branchId = admin.BranchId,

            durationMonths = purchasedDurationMonths,

            includesCms = purchasedIncludesCms,
            includesLab = purchasedIncludesLab,
            includesPharmacy = purchasedIncludesPharmacy,

            amount = purchasedAmount,
            currency = "INR",

            adminCreationStatus = "Created",
            welcomeEmailSent,
            welcomeEmailError,
            mustChangePassword = true,
            nextStep = welcomeEmailSent
                ? "Log in with the emailed temporary password and change it immediately. Use Forgot Password to receive an OTP if the password is forgotten."
                : "The Admin account has been created, but the welcome email failed. Fix SMTP configuration; the Admin can use Forgot Password to receive an OTP if that email flow is working."
        });
    }

    // POST /api/superadmin/pending-admin-creations/{id}/resend-credentials
    // Only a SuperAdmin can issue a replacement temporary password.
    // This invalidates the previous password after the email is accepted by SMTP.
    [HttpPost("{id:int}/resend-credentials")]
    public async Task<IActionResult> ResendCredentials(int id)
    {
        if (id <= 0)
            return BadRequest(new { message = "Valid purchase ID is required." });

        var purchase = await _context.SubscriptionPurchases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (purchase == null)
            return NotFound(new { message = "Subscription purchase not found." });

        if (purchase.AdminCreationStatus != "Created" ||
            !purchase.AdminUserId.HasValue ||
            !string.Equals(purchase.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "This purchase has no created Admin." });

        var admin = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == purchase.AdminUserId.Value && x.Role == "Admin");

        if (admin == null || !admin.IsActive || string.IsNullOrWhiteSpace(admin.Email))
            return Conflict(new { message = "Active Admin account not found." });

        // Restrict this recovery operation to accounts still awaiting first password change.
        if (!admin.MustChangePassword)
            return Conflict(new { message = "Admin has already changed the initial password. Use Forgot Password instead." });

        var replacementPassword = GenerateTemporaryPassword();
        try
        {
            await SendTemporaryPasswordEmailAsync(admin.Name ?? "Admin", admin.Email,
                replacementPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend credentials email failed for Admin {AdminId}", admin.Id);
            return StatusCode(502, new
            {
                message = "Email could not be sent. The current password was not changed."
            });
        }

        // Hash only; never return or log the replacement password.
        // SMTP and database updates cannot be atomic: a DB failure after send
        // requires another resend attempt.
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(replacementPassword);
        admin.MustChangePassword = true;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "A new temporary password was emailed to the Admin.",
            adminUserId = admin.Id,
            adminEmail = admin.Email,
            welcomeEmailSent = true,
            mustChangePassword = true
        });
    }

    private static string GenerateTemporaryPassword()
    {
        // Base64Url-safe random password, with 144 bits of entropy.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private async Task SendTemporaryPasswordEmailAsync(
        string adminName,
        string adminEmail,
        string temporaryPassword)
    {
        // Prefer the existing EmailSettings used by the OTP service.
        // The Smtp section remains supported as a fallback.
        var host = _configuration["EmailSettings:Host"] ?? _configuration["Smtp:Host"];
        var portValue = _configuration["EmailSettings:Port"] ?? _configuration["Smtp:Port"];
        var username = _configuration["EmailSettings:Email"] ?? _configuration["Smtp:Username"];
        var password = _configuration["EmailSettings:Password"] ?? _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"] ?? username;
        var fromName = _configuration["Smtp:FromName"] ?? "CMS Support";
        var enableSslValue = _configuration["Smtp:EnableSsl"];

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(portValue) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException(
                "SMTP settings are missing. Configure EmailSettings (Host, Port, Email, Password) or Smtp settings.");
        }

        if (!int.TryParse(portValue, out var port) || port < 1 || port > 65535)
        {
            throw new InvalidOperationException("Smtp:Port is invalid.");
        }

        var enableSsl = !bool.TryParse(enableSslValue, out var ssl) || ssl;
        var loginUrl = _configuration["App:AdminLoginUrl"]
            ?? "http://localhost:3000/login";

        var safeName = WebUtility.HtmlEncode(adminName);
        var safeEmail = WebUtility.HtmlEncode(adminEmail);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);
        var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);
        var safeFromName = WebUtility.HtmlEncode(fromName);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Your CMS Admin account is ready",
            IsBodyHtml = true,
            Body = $@"
<!doctype html>
<html>
<body style=""font-family:Arial,sans-serif;color:#222;line-height:1.6"">
  <h2>Welcome to CMS, {safeName}</h2>
  <p>Your Admin account has been created.</p>
  <p><strong>Login email:</strong> {safeEmail}</p>
  <p><strong>Temporary password:</strong>
    <span style=""font-family:monospace;font-size:16px"">{safePassword}</span>
  </p>
  <p><a href=""{safeLoginUrl}"">Open the CMS login page</a></p>
  <p><strong>Important:</strong> Sign in with the temporary password and change it immediately.
     Do not share this email or password with anyone.</p>
  <p>If you later forget your password, use <strong>Forgot Password</strong> on the login page.
     An OTP will be sent to this email address to reset your password.</p>
  <p>Regards,<br/>{safeFromName}</p>
</body>
</html>"
        };

        message.To.Add(new MailAddress(adminEmail));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };

        await client.SendMailAsync(message);
    }
}


