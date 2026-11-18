using System.Threading.Tasks;
using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

/// <summary>
/// Authentication and user registration operations.
/// Paid subscription Admin accounts should be provisioned by the Super Admin workflow,
/// not by exposing RegisterAsync as a public endpoint.
/// </summary>
public interface IAuthService
{
    // Existing registration operations
    Task<string> RegisterAsync(RegisterDto dto);
    Task<string> RegisterDoctorAsync(RegisterDoctorDto dto);
    Task<string> RegisterReceptionistAsync(RegisterReceptionistDto dto);
    Task<string> RegisterNurseAsync(RegisterNurseDto dto);

    // Authentication. LoginResponseDto includes MustChangePassword.
    Task<LoginResponseDto?> LoginAsync(LoginDto dto);

    // Forgot-password OTP flow
    Task<string> ForgotPasswordAsync(string email);
    Task<string> VerifyOtpAsync(string otp);
    Task<string> ResetPasswordAsync(string token, string newPassword);

    // Authenticated password change (including first-login temporary password).
    Task<string> ChangePasswordAsync(int userId, ChangePasswordDto dto);
}
