using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class RegisterNurseDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public int HospitalId { get; set; }

    [Required]
    public int BranchId { get; set; }
}
