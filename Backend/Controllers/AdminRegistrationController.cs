using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/auth/admin")]
[AllowAnonymous]
[EnableRateLimiting("admin-registration")]
public sealed class AdminRegistrationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailHelper _email;
    private readonly ILogger<AdminRegistrationController> _logger;

    public AdminRegistrationController(AppDbContext db, EmailHelper email, ILogger<AdminRegistrationController> logger)
    { _db = db; _email = email; _logger = logger; }

    public sealed class RegisterAdminRequest
    {
        [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
        [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
        [Required, RegularExpression("^[6-9][0-9]{9}$")] public string MobileNumber { get; set; } = "";
        [Required, MinLength(8), StringLength(100)] public string Password { get; set; } = "";
    }

    public sealed class VerifyAdminOtpRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, RegularExpression("^[0-9]{6}$")] public string Otp { get; set; } = "";
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string HashOtp(string otp) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp)));

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterAdminRequest dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var email = Normalize(dto.Email);
        var mobile = dto.MobileNumber.Trim();
        User user;
        await using (var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            if (await _db.Users.AnyAsync(x => x.Email != null && x.Email.ToLower() == email))
                return Conflict(new { message = "An account with this email already exists." });
            if (await _db.Users.AnyAsync(x => x.MobileNumber == mobile))
                return Conflict(new { message = "An account with this mobile number already exists." });

            user = new User
            {
                Name = dto.Name.Trim(), Email = email, MobileNumber = mobile,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "Admin", HospitalId = null, BranchId = null,
                IsActive = true, EmailVerified = false, MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var sent = await IssueOtpAsync(email);
        if (!sent)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return StatusCode(503, new { message = "Unable to send verification email. Please retry registration." });
        }
        return StatusCode(201, new { message = "Admin account created. Verify the email OTP to continue.", email, otpExpiryMinutes = 10, emailVerified = false });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyAdminOtpRequest dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var email = Normalize(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email && x.Role == "Admin" && x.IsActive);
        if (user == null) return NotFound(new { message = "Admin registration was not found." });
        if (user.EmailVerified) return Ok(new { message = "Email is already verified.", emailVerified = true });
        var hashed = HashOtp(dto.Otp);
        var record = await _db.OtpVerifications.FirstOrDefaultAsync(x => x.Email == email && x.Purpose == "AdminRegistration" && x.Otp == hashed && !x.IsUsed && x.ExpiryTime > DateTime.UtcNow);
        if (record == null) return BadRequest(new { message = "Invalid or expired registration OTP." });
        record.IsUsed = true;
        user.EmailVerified = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Email verified successfully. You can now log in and purchase a subscription.", emailVerified = true });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendAdminOtpRequest dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var email = Normalize(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email && x.Role == "Admin" && x.IsActive);
        if (user == null) return Ok(new { message = "If an unverified registration exists, a new OTP will be sent." });
        if (user.EmailVerified) return Conflict(new { message = "Email is already verified." });
        if (!await IssueOtpAsync(email)) return StatusCode(503, new { message = "Unable to send OTP. Try again later." });
        return Ok(new { message = "A new verification OTP has been sent.", otpExpiryMinutes = 10 });
    }

    public sealed class ResendAdminOtpRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
    }

    private async Task<bool> IssueOtpAsync(string email)
    {
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var old = await _db.OtpVerifications.Where(x => x.Email == email && x.Purpose == "AdminRegistration" && !x.IsUsed).ToListAsync();
        _db.OtpVerifications.RemoveRange(old);
        _db.OtpVerifications.Add(new OtpVerification { Email = email, Otp = HashOtp(otp), Purpose = "AdminRegistration", ExpiryTime = DateTime.UtcNow.AddMinutes(10), IsUsed = false });
        await _db.SaveChangesAsync();
        try { await _email.SendAdminRegistrationOtp(email, otp); return true; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not deliver Admin registration OTP.");
            var failed = await _db.OtpVerifications.FirstOrDefaultAsync(x => x.Email == email && x.Purpose == "AdminRegistration" && x.Otp == HashOtp(otp));
            if (failed != null) { _db.OtpVerifications.Remove(failed); await _db.SaveChangesAsync(); }
            return false;
        }
    }
}
