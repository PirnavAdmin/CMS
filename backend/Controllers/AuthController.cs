using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Services.Interfaces;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    private readonly AppDbContext _context;

    public AuthController(
        IAuthService authService,
        AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    // =====================================================
    // REGISTER DOCTOR
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpPost("register-doctor")]
    public async Task<IActionResult> RegisterDoctor(RegisterDoctorDto dto)
    {
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
    public async Task<IActionResult> RegisterReceptionist(RegisterReceptionistDto dto)
    {
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
    public async Task<IActionResult> RegisterNurse(RegisterNurseDto dto)
    {
        var adminHospitalIdClaim = User.FindFirst("HospitalId")?.Value;
        if (!int.TryParse(adminHospitalIdClaim, out var adminHospitalId))
            return Unauthorized(new { message = "Hospital information is missing from token" });

        // Admin can create a nurse only inside the admin's own clinic.
        dto.HospitalId = adminHospitalId;

        var result = await _authService.RegisterNurseAsync(dto);
        if (result != "Nurse registered successfully")
            return BadRequest(new { message = result });

        return Ok(new { message = result });
    }

    // =========================================================
    // LOGIN
    // =========================================================

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

        return Ok(new
        {
            message = "Login successful",

            token = result.Token,

            name = result.Name,

            role = result.Role,

            email = result.Email,

            doctorId = result.DoctorId,

            hospitalId = result.HospitalId,

            hospitalName = result.HospitalName,

            branchId = result.BranchId,

            branchName = result.BranchName,

            patientId = result.PatientId,

            patientCode = result.PatientCode,

            mustChangePassword = result.MustChangePassword,

            forcePasswordChange = result.MustChangePassword,

            permissions = result.Permissions
        });
    }

    // ======================================================
    // LOGOUT
    // =======================================================

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

        int userId = int.Parse(userIdClaim.Value);

        var auditLog =
            await _context.AuditLogs
                .Where(x => x.UserId == userId && x.IsOnline)
                .OrderByDescending(x => x.LoginTime)
                .FirstOrDefaultAsync();

        if (auditLog != null)
        {
            auditLog.IsOnline = false;
            auditLog.LogoutTime = DateTime.UtcNow;
            auditLog.Action = "Logout";
            auditLog.SystemAction = "User Logged Out";

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
    public IActionResult Me()
    {
        var role =
            User.Claims.FirstOrDefault(
                x => x.Type == "role")?.Value;

        var email =
            User.Claims.FirstOrDefault(
                x => x.Type == "email")?.Value;

        var hospitalId =
            User.Claims.FirstOrDefault(
                x => x.Type == "HospitalId")?.Value;

        var doctorId =
            User.Claims.FirstOrDefault(
                x => x.Type == "DoctorId")?.Value;

        return Ok(new
        {
            role,
            email,
            hospitalId,
            doctorId
        });
    }

    // =====================================================
    // FORGOT PASSWORD
    // =====================================================

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordDto dto)
    {
        var result =
            await _authService
                .ForgotPasswordAsync(dto.Email);

        return Ok(new
        {
            message = result
        });
    }

    // =====================================================
    // VERIFY OTP
    // =====================================================

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] string otp)
    {
        var token =
            await _authService
                .VerifyOtpAsync(otp);

        if (token == null)
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

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordDto dto)
    {
        var result =
            await _authService
                .ResetPasswordAsync(
                    dto.Token,
                    dto.NewPassword);

        return Ok(new
        {
            message = result
        });
    }

    // =====================================================
    // CHANGE PASSWORD
    // =====================================================

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordDto dto)
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

        int userId =
            int.Parse(userIdClaim.Value);

        var result =
            await _authService
                .ChangePasswordAsync(userId, dto);

        if (result != "Password changed successfully")
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
}