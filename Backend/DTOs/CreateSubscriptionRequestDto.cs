using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreateSubscriptionRequestDto
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        @"^[6-9]\d{9}$",
        ErrorMessage = "Enter a valid 10-digit mobile number.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int SubscriptionPlanId { get; set; }
}