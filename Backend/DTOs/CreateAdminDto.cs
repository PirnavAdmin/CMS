using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreateAdminDto : IValidatableObject
{
    // Both Name and FullName are retained for existing clients.
    [MaxLength(150)]
    public string? Name { get; set; }

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? MobileNumber { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    // Role is retained for compatibility. The server must enforce Admin.
    [MaxLength(64)]
    public string? Role { get; set; } = "Admin";

    // Retained for older endpoints. New paid onboarding assigns a clinic
    // only after the Admin creates their first clinic.
    public int? HospitalId { get; set; }

    public bool SendWelcomeEmail { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var effectiveName = !string.IsNullOrWhiteSpace(Name) ? Name : FullName;
        if (string.IsNullOrWhiteSpace(effectiveName))
        {
            yield return new ValidationResult(
                "Name or FullName is required.",
                new[] { nameof(Name), nameof(FullName) });
        }

        var effectivePhone = !string.IsNullOrWhiteSpace(MobileNumber) ? MobileNumber : Phone;
        if (!string.IsNullOrWhiteSpace(effectivePhone) &&
            !System.Text.RegularExpressions.Regex.IsMatch(effectivePhone, @"^[6-9]\d{9}$"))
        {
            yield return new ValidationResult(
                "MobileNumber or Phone must be a valid 10-digit Indian mobile number.",
                new[] { nameof(MobileNumber), nameof(Phone) });
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return new ValidationResult("Email is required.", new[] { nameof(Email) });
        }

        if (!string.IsNullOrWhiteSpace(Role) &&
            !string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Only the Admin role can be created using this DTO.",
                new[] { nameof(Role) });
        }

        if (HospitalId.HasValue && HospitalId.Value <= 0)
        {
            yield return new ValidationResult(
                "HospitalId must be a positive number when supplied.",
                new[] { nameof(HospitalId) });
        }
    }
}

