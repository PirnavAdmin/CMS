

using Microsoft.EntityFrameworkCore;

using BCrypt.Net;

using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Linq;
namespace AuthDemo.Services;

public class AuthService
    : IAuthService
{
    private readonly AppDbContext
        _context;

    private readonly JwtHelper
        _jwtHelper;

    private readonly EmailHelper
        _emailHelper;

    private readonly IHttpContextAccessor
    _httpContextAccessor;

    public AuthService(
     AppDbContext context,
     JwtHelper jwtHelper,
     EmailHelper emailHelper,
     IHttpContextAccessor httpContextAccessor)
    {
        _context =
            context;

        _jwtHelper =
            jwtHelper;

        _emailHelper =
            emailHelper;

        _httpContextAccessor =
            httpContextAccessor;
    }
    // =====================================================
    // REGISTER ADMIN + HOSPITAL
    // =====================================================

    public async Task<string>
        RegisterAsync(
            RegisterDto dto)
    {
        var exists =
            await _context.Users

                .AnyAsync(x =>
                    x.Email ==
                    dto.Email
                );

        if (exists)
        {
            return
                "Email already exists";
        }

        // =================================================
        // CREATE HOSPITAL
        // =================================================

        var hospital =
            new Hospital
            {
                Name =
                    dto.HospitalName,

                Address =
                    dto.HospitalAddress,

                Phone =
                    dto.HospitalPhone,

                Email =
                    dto.HospitalEmail
            };

        _context.Hospitals
            .Add(hospital);

        await _context
            .SaveChangesAsync();

        // =================================================
        // CREATE ADMIN USER
        // =================================================

        var user =
            new User
            {
                Name =
                    dto.Name,

                MobileNumber =
                    dto.MobileNumber,

                Email =
                    dto.Email,

                PasswordHash =
                    BCrypt.Net.BCrypt
                        .HashPassword(
                            dto.Password
                        ),

                Role =
                    "Admin",

                HospitalId =
                    hospital.Id
            };

        _context.Users
            .Add(user);

        await _context
            .SaveChangesAsync();

        return
            "Admin registered successfully";
    }

    // =====================================================
    // REGISTER DOCTOR
    // =====================================================

    public async Task<string>
        RegisterDoctorAsync(
            RegisterDoctorDto dto)
    {
        var exists =
            await _context.Users

                .AnyAsync(x =>
                    x.Email ==
                    dto.Email
                );

        if (exists)
        {
            return
                "Email already exists";
        }

        var doctor =
            await _context.Doctors

                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    dto.DoctorId
                );

        if (doctor == null)
        {
            return
                "Doctor not found";
        }

        var user =
            new User
            {
                Name =
                    dto.Name,

                MobileNumber =
                    dto.MobileNumber,

                Email =
                    dto.Email,

                PasswordHash =
                    BCrypt.Net.BCrypt
                        .HashPassword(
                            dto.Password
                        ),

                Role =
                    "Doctor",

                DoctorId =
                    doctor.Id,

                HospitalId =
                    doctor.HospitalId,

                MustChangePassword = true
            };

        _context.Users
            .Add(user);

        await _context
            .SaveChangesAsync();

        await _emailHelper.SendStaffCredentials(
            dto.Email!, dto.Name ?? "Doctor", "Doctor", dto.Password!);

        return
            "Doctor registered successfully";
    }

    // =====================================================
    // REGISTER RECEPTIONIST
    // =====================================================

    public async Task<string>
        RegisterReceptionistAsync(
            RegisterReceptionistDto dto)
    {
        var exists =
            await _context.Users

                .AnyAsync(x =>
                    x.Email ==
                    dto.Email
                );

        if (exists)
        {
            return
                "Email already exists";
        }

        var hospital =
            await _context.Hospitals

                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    dto.HospitalId
                );

        if (hospital == null)
        {
            return
                "Hospital not found";
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return "Password must contain at least 6 characters";

        var receptionist =
            new Receptionist
            {
                Name =
                    dto.Name,

                Email =
                    dto.Email,

                Phone =
                    dto.Phone,

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                BranchId = dto.BranchId,

                HospitalId =
                    dto.HospitalId
            };

        _context.Receptionists
            .Add(receptionist);

        var user =
            new User
            {
                Name =
                    dto.Name,

                MobileNumber =
                    dto.Phone,

                Email =
                    dto.Email,

                PasswordHash =
                    receptionist.PasswordHash,

                BranchId = dto.BranchId,
                MustChangePassword = true,

                Role =
                    "Receptionist",

                HospitalId =
                    dto.HospitalId
            };

        _context.Users
            .Add(user);

        await _context
            .SaveChangesAsync();

        await _emailHelper.SendStaffCredentials(
            dto.Email!, dto.Name ?? "Receptionist", "Receptionist", dto.Password!);

        return
            "Receptionist registered successfully";
    }

    // =====================================================
    // REGISTER NURSE
    // =====================================================

    public async Task<string> RegisterNurseAsync(RegisterNurseDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(x => x.Email == normalizedEmail);
        if (exists) return "Email already exists";

        var branch = await _context.Branches.FirstOrDefaultAsync(x =>
            x.Id == dto.BranchId &&
            x.HospitalId == dto.HospitalId &&
            x.IsActive);

        if (branch == null) return "Invalid or inactive branch";

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = normalizedEmail,
            MobileNumber = dto.Phone.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Nurse",
            HospitalId = dto.HospitalId,
            BranchId = dto.BranchId,
            IsActive = true,
            MustChangePassword = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _context.Staffs.Add(new Staff
        {
            UserId = user.Id,
            Role = "Nurse",
            HospitalId = dto.HospitalId,
            BranchId = dto.BranchId,
            IsActive = true
        });

        await _context.SaveChangesAsync();

        await _emailHelper.SendStaffCredentials(
            user.Email!, user.Name ?? "Nurse", "Nurse", dto.Password);

        return "Nurse registered successfully";
    }

    // =====================================================
    // LOGIN
    // =====================================================

    public async Task<LoginResponseDto?>
        LoginAsync(
            LoginDto dto)
    {
        var user =
            await _context.Users
                .Include(x => x.Hospital)
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    (x.Email == dto.Email || x.MobileNumber == dto.Email));

        if (user == null)
        {
            return null;
        }

        var valid =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if (!valid)
        {
            return null;
        }

        var patient = user.Role == "Patient"
            ? await _context.Patients.FirstOrDefaultAsync(x =>
                x.Email == user.Email || x.Phone == user.MobileNumber)
            : null;
        Branch? branch = null;

        if (user.DoctorId != null)
        {
            var doctor = await _context.Doctors
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.Id == user.DoctorId);

            branch = doctor?.Branch;
        }
        else if (user.Role == "Receptionist")
        {
            var receptionist = await _context.Receptionists
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.Email == user.Email);

            branch = receptionist?.Branch;
        }
        else if (user.Role is "Staff" or "Nurse")
        {
            var staff = await _context.Staffs
                .Include(x => x.Branch)
                .FirstOrDefaultAsync(x => x.UserId == user.Id);

            branch = staff?.Branch;
        }

        // Newer staff records also keep BranchId directly on User.
        if (branch == null && user.BranchId.HasValue)
        {
            branch = await _context.Branches.FirstOrDefaultAsync(x =>
                x.Id == user.BranchId.Value && x.HospitalId == user.HospitalId);
        }

        var permissions = new List<UserPermissionResponseDto>();

        if (user.Role is "Doctor" or "Receptionist" or "Nurse")
        {
            var savedPermissions = await _context.UserPermissions
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToDictionaryAsync(x => x.Module, StringComparer.OrdinalIgnoreCase);

            permissions = PermissionModules.All
                .Select(module =>
                {
                    savedPermissions.TryGetValue(module, out var permission);

                    return new UserPermissionResponseDto
                    {
                        Module = module,
                        CanView = permission?.CanView ?? true,
                        CanCreate = permission?.CanCreate ?? true,
                        CanEdit = permission?.CanEdit ?? true,
                        CanDelete = permission?.CanDelete ?? true
                    };
                })
                .ToList();
        }

        var token =
            _jwtHelper.GenerateToken(user);

        // =====================================================
        // LOGIN AUDIT
        // =====================================================

        var httpContext = _httpContextAccessor.HttpContext;

        var ipAddress =
            httpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext?.Connection.RemoteIpAddress?.ToString()
            ?? "Unknown";

        var browser =
            httpContext?.Request.Headers["User-Agent"].ToString()
            ?? "Unknown";

        string device = "Desktop";

        if (browser.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            device = "Android";
        }
        else if (browser.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
        {
            device = "iPhone";
        }
        else if (browser.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            device = "iPad";
        }

        var auditLog = new AuditLog
        {
            UserId = user.Id,
            UserName = user.Name,
            Role = user.Role,

            ClinicId = user.HospitalId,
            BranchId = branch?.Id,

            Action = "Login",
            SystemAction = "User Login",
            IsLoginActivity = true,

            IpAddress = ipAddress,
            Browser = browser,
            Device = device,

            LoginTime = DateTime.UtcNow,
            IsOnline = true,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync();

        return new LoginResponseDto
        {
            Token = token,
            Name = user.Name,
            Role = user.Role,
            Email = user.Email,
            DoctorId = user.DoctorId,
            HospitalId = user.Role == "SuperAdmin"
    ? null
    : user.HospitalId,

            HospitalName = user.Role == "SuperAdmin"
    ? null
    : user.Hospital?.Name,
            BranchId = branch?.Id,
            BranchName = branch?.Name,
            PatientId = patient?.Id,
            PatientCode = patient?.PatientCode,
            MustChangePassword = user.MustChangePassword,
            Permissions = permissions
        };
    }


    // =====================================================
    // FORGOT PASSWORD
    // =====================================================

    public async Task<string>
        ForgotPasswordAsync(
            string email)
    {
        var user =
            await _context.Users

                .FirstOrDefaultAsync(x =>
                    x.Email ==
                    email
                );

        if (user == null)
        {
            return
                "User not found";
        }

        // =================================================
        // GENERATE OTP
        // =================================================

        var otp =
            new Random()

                .Next(
                    100000,
                    999999
                )

                .ToString();

        // =================================================
        // REMOVE OLD OTP
        // =================================================

        var oldOtps =
            await _context
                .OtpVerifications

                .Where(x =>
                    x.Email ==
                    email
                )

                .ToListAsync();

        if (oldOtps.Any())
        {
            _context
                .OtpVerifications
                .RemoveRange(oldOtps);
        }

        // =================================================
        // SAVE OTP
        // =================================================

        var otpData =
            new OtpVerification
            {
                Email =
                    email,

                Otp =
                    otp,

                ExpiryTime =
                    DateTime.UtcNow
                        .AddMinutes(10),

                IsUsed =
                    false
            };

        _context.OtpVerifications
            .Add(otpData);

        await _context
            .SaveChangesAsync();

        // =================================================
        // SEND REAL EMAIL
        // =================================================

        await _emailHelper
            .SendEmail(
                email,
                otp
            );

        return
            "OTP sent successfully";
    }

    // =====================================================
    // VERIFY OTP
    // =====================================================

    public async Task<string>
        VerifyOtpAsync(
            string otp)
    {
        var otpData =
            await _context
                .OtpVerifications

                .FirstOrDefaultAsync(x =>

                    x.Otp ==
                    otp &&

                    x.IsUsed ==
                    false &&

                    x.ExpiryTime >
                    DateTime.UtcNow
                );

        if (otpData == null)
        {
            return null;
        }

        var resetToken =
            Guid.NewGuid()
                .ToString();

        otpData.IsUsed =
            true;

        otpData.ResetToken =
            resetToken;

        otpData.ResetTokenExpiry =
            DateTime.UtcNow
                .AddMinutes(15);

        await _context
            .SaveChangesAsync();

        return resetToken;
    }

    // =====================================================
    // RESET PASSWORD
    // =====================================================

    public async Task<string>
        ResetPasswordAsync(
            string token,
            string newPassword)
    {
        var otpData =
            await _context
                .OtpVerifications

                .FirstOrDefaultAsync(x =>

                    x.ResetToken ==
                    token &&

                    x.ResetTokenExpiry >
                    DateTime.UtcNow
                );

        if (otpData == null)
        {
            return
                "Invalid or expired token";
        }

        var user =
            await _context.Users

                .FirstOrDefaultAsync(x =>
                    x.Email ==
                    otpData.Email
                );

        if (user == null)
        {
            return
                "User not found";
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt
                .HashPassword(
                    newPassword
                );

        await _context
            .SaveChangesAsync();

        return
            "Password reset successful";
    }

    // =====================================================
    // CHANGE PASSWORD
    // =====================================================

    public async Task<string>
        ChangePasswordAsync(
            int userId,
            ChangePasswordDto dto)
    {
        var user =
            await _context.Users

                .FirstOrDefaultAsync(x =>
                    x.Id == userId);

        if (user == null)
        {
            return "User not found";
        }

        bool isValidPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.OldPassword,
                user.PasswordHash);

        if (!isValidPassword)
        {
            return "Old password is incorrect";
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        user.MustChangePassword = false;

        await _context.SaveChangesAsync();

        return "Password changed successfully";
    }
}

