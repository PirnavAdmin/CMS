
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public class HospitalMerchantSetting
{
    public int Id { get; set; }

    [Required]
    public int HospitalId { get; set; }

    [Required, MaxLength(100)]
    public string Provider { get; set; } = "Razorpay";

    [Required, MaxLength(150)]
    public string MerchantName { get; set; } = "";

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    public bool IsEnabled { get; set; } = false;

    [MaxLength(200)]
    public string? CredentialReference { get; set; }

    [Required, MaxLength(30)] public string KycStatus { get; set; } = "NotStarted";
    [MaxLength(150)] public string? ProviderMerchantAccountId { get; set; }
    [MaxLength(500)] public string? KycNotes { get; set; }
    public DateTime? KycSubmittedAt { get; set; }
    public DateTime? KycReviewedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
