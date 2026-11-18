
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class UpsertSubscriptionPlanDto : IValidatableObject
{
    // BASIC / SUPER / PREMIUM
    [Required]
    [MaxLength(30)]
    public string PlanType { get; set; } = "Basic";

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    // Allowed durations: 1, 3, 6, 12 months.
    [Required]
    public int DurationMonths { get; set; }

    // CMS is mandatory.
    // Lab and Pharmacy modules are optional.
    public bool IncludesCms { get; set; } = true;

    public bool IncludesLab { get; set; } = false;

    public bool IncludesPharmacy { get; set; } = false;

    // Price for this plan and duration.
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Price { get; set; }

    // Maximum number of active branches allowed.
    // Includes the automatically created Main Branch.
    [Range(1, 10000,
        ErrorMessage = "MaxBranches must be at least 1.")]
    public int MaxBranches { get; set; } = 1;

    // Maximum appointments scheduled per clinic per day.
    // Shared across all branches and booking channels.
    [Range(1, 1000000,
        ErrorMessage = "DailyAppointmentLimit must be at least 1.")]
    public int DailyAppointmentLimit { get; set; } = 20;

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        // Validate subscription plan type.
        if (string.IsNullOrWhiteSpace(PlanType) ||
            !(string.Equals(
                  PlanType, "Basic",
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(
                  PlanType, "Super",
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(
                  PlanType, "Premium",
                  StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "Plan type must be Basic, Super, or Premium.",
                new[] { nameof(PlanType) });
        }

        // Validate allowed subscription durations.
        if (DurationMonths != 1 &&
            DurationMonths != 3 &&
            DurationMonths != 6 &&
            DurationMonths != 12)
        {
            yield return new ValidationResult(
                "Duration must be 1, 3, 6, or 12 months.",
                new[] { nameof(DurationMonths) });
        }

        // CMS must always be included.
        if (!IncludesCms)
        {
            yield return new ValidationResult(
                "CMS must be included in every subscription plan.",
                new[] { nameof(IncludesCms) });
        }

        // MaxBranches and DailyAppointmentLimit
        // are configurable by Super Admin.
        // [Range] attributes validate their minimum
        // and maximum allowed values.
    }
}

public class AssignClinicSubscriptionDto : IValidatableObject
{
    [Range(1, int.MaxValue,
        ErrorMessage = "A valid clinic ID is required.")]
    public int HospitalId { get; set; }

    [Range(1, int.MaxValue,
        ErrorMessage = "A valid subscription plan ID is required.")]
    public int SubscriptionPlanId { get; set; }

    public DateTime? StartDate { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (StartDate.HasValue &&
            StartDate.Value == default)
        {
            yield return new ValidationResult(
                "Start date must be a valid date.",
                new[] { nameof(StartDate) });
        }
    }
}
