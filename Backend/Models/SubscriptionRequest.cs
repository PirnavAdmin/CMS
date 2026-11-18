using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthDemo.Models;

// Legacy subscription-request record. Checkout/payment attempts are tracked
// separately by SubscriptionPurchase; do not use this model to mark a payment
// successful without payment verification.
public class SubscriptionRequest : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number.")]
    [MaxLength(10)]
    public string MobileNumber { get; set; } = string.Empty;

    public int SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    // Snapshot of the plan name, amount, duration and module access at request time.
    [Required]
    [MaxLength(150)]
    public string PlanName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public int DurationMonths { get; set; }
    public bool IncludesCms { get; set; } = true;
    public bool IncludesLab { get; set; }
    public bool IncludesPharmacy { get; set; }

    // Preserve existing status values used by current controllers/database.
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "PendingPayment";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? AdminUserId { get; set; }
    public int? HospitalId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SubscriptionPlanId <= 0)
            yield return new ValidationResult("A valid subscription plan is required.", new[] { nameof(SubscriptionPlanId) });

        if (DurationMonths != 1 && DurationMonths != 3 && DurationMonths != 6 && DurationMonths != 12)
            yield return new ValidationResult("Duration must be 1, 3, 6 or 12 months.", new[] { nameof(DurationMonths) });

        if (!IncludesCms)
            yield return new ValidationResult("CMS must be included in every subscription.", new[] { nameof(IncludesCms) });

        if (Amount <= 0)
            yield return new ValidationResult("Subscription amount must be greater than zero.", new[] { nameof(Amount) });

        if (PaidAt.HasValue && PaidAt.Value < CreatedAt)
            yield return new ValidationResult("Payment date cannot be earlier than request creation.", new[] { nameof(PaidAt) });

        if (AdminUserId.HasValue && AdminUserId.Value <= 0)
            yield return new ValidationResult("Admin user ID must be valid.", new[] { nameof(AdminUserId) });

        if (HospitalId.HasValue && HospitalId.Value <= 0)
            yield return new ValidationResult("Clinic ID must be valid.", new[] { nameof(HospitalId) });
    }
}

