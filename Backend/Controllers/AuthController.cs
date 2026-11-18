using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Services.Interfaces;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        AppDbContext context,
        JwtHelper jwtHelper,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    // SaaS Admin self-registration is handled by AdminRegistrationController.
    // This controller retains authenticated staff registration and existing login flows.

    private async Task<int?> GetAssignedAdminHospitalIdAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return null;

        return await _context.Users.AsNoTracking()
            .Where(x => x.Id == id && x.Role == "Admin" && x.IsActive &&
                        x.HospitalId.HasValue && !x.MustChangePassword)
            .Select(x => x.HospitalId)
            .FirstOrDefaultAsync();
    }

    // =====================================================
    // REGISTER DOCTOR
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("register-doctor")]
    public async Task<IActionResult> RegisterDoctor(
        RegisterDoctorDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Doctor details are required." });
        var hospitalId = await GetAssignedAdminHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();
        dto.HospitalId = hospitalId.Value;

        var result =
            await _authService.RegisterDoctorAsync(dto);

        return Ok(new
        {
            message = result
        });
    }

    // =====================================================
    // REGISTER RECEPTIONIST
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("register-receptionist")]
    public async Task<IActionResult> RegisterReceptionist(
        RegisterReceptionistDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Receptionist details are required." });
        var hospitalId = await GetAssignedAdminHospitalIdAsync();
        if (!hospitalId.HasValue)
            return Forbid();
        dto.HospitalId = hospitalId.Value;

        var result =
            await _authService.RegisterReceptionistAsync(dto);

        return Ok(new
        {
            message = result
        });
    }

    // =====================================================
    // REGISTER NURSE
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("register-nurse")]
    public async Task<IActionResult> RegisterNurse(
        RegisterNurseDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Nurse details are required." });
        var adminHospitalId = await GetAssignedAdminHospitalIdAsync();
        if (!adminHospitalId.HasValue) return Forbid();
        dto.HospitalId = adminHospitalId.Value;

        var result =
            await _authService.RegisterNurseAsync(dto);

        if (result != "Nurse registered successfully")
        {
            return BadRequest(new
            {
                message = result
            });
        }

        return Ok(new
        {
            message = result
        });
    }

    // =====================================================
    // LOGIN
    // =====================================================

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var result =
            await _authService.LoginAsync(dto);

        if (result == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password"
            });
        }

        // =================================================
        // SUPER ADMIN LOGIN RESPONSE
        // =================================================
        //
        // SuperAdmin is system/SaaS level.
        //
        // Do NOT return:
        // HospitalId
        // HospitalName
        // BranchId
        // BranchName
        // DoctorId
        // PatientId
        // PatientCode
        // Permissions
        //
        // because they are not related to SuperAdmin.
        // =================================================

        if (string.Equals(
                result.Role,
                "SuperAdmin",
                StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                message = "Login successful",

                token = result.Token,

                name = result.Name,

                role = result.Role,

                email = result.Email
            });
        }

        // =================================================
        // OTHER ROLE LOGIN RESPONSE
        // =================================================
        //
        // Admin / Doctor / Receptionist / Nurse / Patient
        // continue using the existing response.
        // =================================================

        return Ok(new
        {
            message = "Login successful",

            token = result.Token,

            name = result.Name,

            role = result.Role,

            email = result.Email,

            doctorId = result.DoctorId,

            hospitalId = result.HospitalId,

            onboardingRequired =
                string.Equals(result.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                && !result.HospitalId.HasValue,

            hospitalName = result.HospitalName,

            branchId = result.BranchId,

            branchName = result.BranchName,

            patientId = result.PatientId,

            patientCode = result.PatientCode,

            mustChangePassword =
                result.MustChangePassword,

            forcePasswordChange =
                result.MustChangePassword,

            permissions =
                result.Permissions
        });
    }

    // =====================================================
    // REFRESH ADMIN TOKEN AFTER CLINIC ONBOARDING
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        // Always reload the account from the database. The old token
        // may have been issued before a clinic was assigned.
        var admin = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.Role == "Admin" &&
                x.IsActive);

        if (admin == null)
        {
            return Unauthorized(new { message = "Admin account is inactive or unavailable." });
        }

        // Do not issue a refreshed Admin token until password setup is complete.
        if (admin.MustChangePassword)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Complete password setup before refreshing your token.",
                mustChangePassword = true,
                forcePasswordChange = true
            });
        }

        // Generate a fresh token from the database user, including the
        // HospitalId assigned when the Admin created the clinic.
        var newToken = _jwtHelper.GenerateToken(admin);

        string? hospitalName = null;
        if (admin.HospitalId.HasValue)
        {
            hospitalName = await _context.Hospitals
                .AsNoTracking()
                .Where(x => x.Id == admin.HospitalId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            message = "Token refreshed successfully.",
            token = newToken,
            userId = admin.Id,
            name = admin.Name,
            email = admin.Email,
            role = admin.Role,
            hospitalId = admin.HospitalId,
            hospitalName,
            branchId = admin.BranchId,
            onboardingRequired = !admin.HospitalId.HasValue,
            mustChangePassword = admin.MustChangePassword,
            forcePasswordChange = admin.MustChangePassword,
            requiresRelogin = false
        });
    }

    // =====================================================
    // LOGOUT
    // =====================================================

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim =
            User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized(new
            {
                message = "Invalid token"
            });
        }

        if (!int.TryParse(
                userIdClaim.Value,
                out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user information in token"
            });
        }

        var auditLog =
            await _context.AuditLogs
                .Where(x =>
                    x.UserId == userId &&
                    x.IsOnline)
                .OrderByDescending(x => x.LoginTime)
                .FirstOrDefaultAsync();

        if (auditLog != null)
        {
            auditLog.IsOnline = false;

            auditLog.LogoutTime =
                DateTime.UtcNow;

            auditLog.Action =
                "Logout";

            auditLog.SystemAction =
                "User Logged Out";

            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            message = "Logout successful"
        });
    }

    // =====================================================
    // GET CURRENT USER
    // =====================================================

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(new { message = "Invalid user identity." });

        var account = await _context.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Role,
                x.HospitalId,
                x.BranchId,
                x.MustChangePassword
            })
            .FirstOrDefaultAsync();

        if (account == null)
            return Unauthorized(new { message = "User account is inactive or unavailable." });

        if (string.Equals(account.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return Ok(new { role = account.Role, email = account.Email });

        // Always use the current database assignment: the login JWT can
        // predate clinic creation and contain no HospitalId.
        return Ok(new
        {
            userId = account.Id,
            name = account.Name,
            email = account.Email,
            role = account.Role,
            hospitalId = account.HospitalId,
            branchId = account.BranchId,
            doctorId = User.FindFirstValue("DoctorId"),
            onboardingRequired = account.Role == "Admin" && !account.HospitalId.HasValue,
            mustChangePassword = account.MustChangePassword,
            forcePasswordChange = account.MustChangePassword
        });
    }

    // =====================================================
    // FORGOT PASSWORD
    // =====================================================

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email is required." });

        // Avoid exposing whether a particular email is registered.
        const string safeMessage =
            "If an active account exists, an OTP will be sent to its email address.";

        try
        {
            var result = (await _authService.ForgotPasswordAsync(dto.Email.Trim()))?.Trim();

            if (string.Equals(result, "User not found", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result, "OTP sent successfully", StringComparison.OrdinalIgnoreCase))
                return Ok(new { message = safeMessage });

            // An unexpected result indicates a service-side failure or a response
            // contract mismatch. Log the result on the server; do not expose it
            // publicly because it might reveal account status or mail internals.
            _logger.LogWarning(
                "Forgot-password service returned an unexpected result: {ServiceResult}",
                result ?? "<null>");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Password reset is temporarily unavailable. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forgot-password request failed in the authentication service.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Password reset is temporarily unavailable. Please try again later."
            });
        }
    }

    // =====================================================
    // =====================================================
    // VERIFY OTP
    // =====================================================

    [AllowAnonymous]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] string otp)
    {
        if (string.IsNullOrWhiteSpace(otp) || otp.Trim().Length != 6 ||
            !otp.Trim().All(char.IsDigit))
            return BadRequest(new { message = "Enter a valid six-digit OTP." });

        var token =
            await _authService
                .VerifyOtpAsync(otp.Trim());

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new
            {
                message = "Invalid or expired OTP"
            });
        }

        return Ok(new
        {
            message = "OTP verified",
            resetToken = token
        });
    }

    // =====================================================
    // RESET PASSWORD
    // =====================================================

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.Token) ||
            string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return BadRequest(new { message = "A valid reset token and password of at least 8 characters are required." });

        var result =
            await _authService
                .ResetPasswordAsync(
                    dto.Token,
                    dto.NewPassword);

        if (result != "Password reset successful")
            return BadRequest(new { message = result });

        return Ok(new { message = result });
    }

    // =====================================================
    // CHANGE PASSWORD
    // =====================================================

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.OldPassword) ||
            string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return BadRequest(new { message = "Old password and a new password of at least 8 characters are required." });

        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized(new
            {
                message = "Invalid token"
            });
        }

        if (!int.TryParse(
                userIdClaim.Value,
                out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user information in token"
            });
        }

        var result =
            await _authService
                .ChangePasswordAsync(
                    userId,
                    dto);

        if (result !=
            "Password changed successfully")
        {
            return BadRequest(new
            {
                message = result
            });
        }

        return Ok(new
        {
            message = result,
            mustChangePassword = false,
            requiresRelogin = true
        });
    }
}

