
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public class SubscriptionPlan : IValidatableObject
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    // Supported plan categories: Basic, Super, Premium.
    [Required]
    [MaxLength(20)]
    public string PlanType { get; set; } = "Basic";

    // Allowed durations: 1, 3, 6 and 12 months.
    [Range(1, 12)]
    public int DurationMonths { get; set; }

    // CMS is mandatory. Lab and Pharmacy are optional.
    public bool IncludesCms { get; set; } = true;
    public bool IncludesLab { get; set; } = false;
    public bool IncludesPharmacy { get; set; } = false;

    // Price for the selected plan, modules and duration.
    // Checkout must snapshot price and entitlements.
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Price { get; set; }

    // Maximum number of active branches allowed.
    // Inactive branches do not consume the limit.
    [Range(1, 10000)]
    public int MaxBranches { get; set; } = 1;

    // Maximum appointments scheduled per clinic per day,
    // shared across all active branches and booking channels.
    [Range(1, 1000000)]
    public int DailyAppointmentLimit { get; set; } = 20;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PlanType) ||
            !(string.Equals(PlanType, "Basic",
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(PlanType, "Super",
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(PlanType, "Premium",
                  StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "Plan type must be Basic, Super, or Premium.",
                new[] { nameof(PlanType) });
        }

        if (DurationMonths != 1 &&
            DurationMonths != 3 &&
            DurationMonths != 6 &&
            DurationMonths != 12)
        {
            yield return new ValidationResult(
                "Duration must be 1, 3, 6, or 12 months.",
                new[] { nameof(DurationMonths) });
        }

        if (!IncludesCms)
        {
            yield return new ValidationResult(
                "CMS is mandatory for every subscription plan.",
                new[] { nameof(IncludesCms) });
        }

        // Initial limits for each plan category.
        // These values are currently fixed by plan type.
        // If Super Admin must edit limits independently,
        // this validation will be adjusted in Step 2.
        int expectedBranches;
        int expectedAppointments;

        switch (PlanType?.Trim().ToUpperInvariant())
        {
            case "BASIC":
                expectedBranches = 1;
                expectedAppointments = 20;
                break;

            case "SUPER":
                expectedBranches = 3;
                expectedAppointments = 50;
                break;

            case "PREMIUM":
                expectedBranches = 10;
                expectedAppointments = 100;
                break;

            default:
                yield break;
        }

        if (MaxBranches != expectedBranches)
        {
            yield return new ValidationResult(
                $"{PlanType} plan must allow " +
                $"{expectedBranches} active branch(es).",
                new[] { nameof(MaxBranches) });
        }

        if (DailyAppointmentLimit != expectedAppointments)
        {
            yield return new ValidationResult(
                $"{PlanType} plan must allow " +
                $"{expectedAppointments} appointments per day.",
                new[] { nameof(DailyAppointmentLimit) });
        }
    }
}
