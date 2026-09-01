using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

/// <summary>
/// Fields that a nurse is allowed to update. Clinical diagnosis,
/// prescriptions, billing, doctor and appointment schedule are excluded.
/// </summary>
public class UpdateNursePatientDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BloodGroup { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
}
