
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreatePharmacistDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[6-9]\d{9}$")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int BranchId { get; set; }
}
