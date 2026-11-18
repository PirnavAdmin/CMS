using AuthDemo.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthDemo.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _config;

    public JwtHelper(IConfiguration config)
    {
        _config = config;
    }

    private SigningCredentials GetSigningCredentials()
    {
        var secret = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 UTF-8 bytes.");

        return new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
    }

    private string WriteToken(IEnumerable<Claim> claims)
    {
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: GetSigningCredentials());

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Used by AuthService.LoginAsync and by a future refresh endpoint.
    // Always pass a User freshly loaded from the database when refreshing.
    public string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var role = user.Role ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name ?? string.Empty),
            new("email", user.Email ?? string.Empty),
            new("userId", user.Id.ToString()),
            new(ClaimTypes.Role, role),
            new("role", role)
        };

        if (!string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            if (user.HospitalId.HasValue)
                claims.Add(new Claim("HospitalId", user.HospitalId.Value.ToString()));

            if (user.BranchId.HasValue)
                claims.Add(new Claim("BranchId", user.BranchId.Value.ToString()));

            if (user.DoctorId.HasValue)
                claims.Add(new Claim("DoctorId", user.DoctorId.Value.ToString()));
        }

        return WriteToken(claims);
    }

    public string GenerateSuperAdminToken(SuperAdmin superAdmin)
    {
        ArgumentNullException.ThrowIfNull(superAdmin);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, superAdmin.Id.ToString()),
            new(ClaimTypes.Name, superAdmin.Name ?? string.Empty),
            new(ClaimTypes.Email, superAdmin.Email ?? string.Empty),
            new(ClaimTypes.Role, "SuperAdmin")
        };

        return WriteToken(claims);
    }
}
