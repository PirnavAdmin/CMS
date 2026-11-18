
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthDemo.Models;

public class ClinicSubscription : IValidatableObject
{
    public int Id { get; set; }

    // Clinic assigned after the paid Admin completes onboarding.
    public int HospitalId { get; set; }
    public Hospital? Hospital { get; set; }

    // Keep the original plan relationship for reporting.
    // Purchased entitlements are stored as snapshots below.
    public int SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }

    // UTC subscription period:
    // StartDate is inclusive; EndDate is exclusive.
    //
    // Subscription is usable only when:
    // Status == "Active"
    // StartDate <= DateTime.UtcNow
    // EndDate > DateTime.UtcNow
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Purchased subscription amount.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal Amount { get; set; }

    // Preserve existing status strings for compatibility
    // with controllers and middleware.
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    // Purchased module snapshots.
    // Never recalculate these from edited plan values.
    public bool IncludesCms { get; set; } = true;
    public bool IncludesLab { get; set; } = false;
    public bool IncludesPharmacy { get; set; } = false;

    // Purchased branch limit snapshot.
    // Main Branch counts as one active branch.
    // Inactive branches do not consume the limit.
    [Range(1, 10000)]
    public int PurchasedMaxBranches { get; set; } = 1;

    // Purchased daily appointment limit snapshot.
    // Shared across every branch and booking channel
    // within this clinic.
    [Range(1, 1000000)]
    public int PurchasedDailyAppointmentLimit { get; set; } = 20;

    // When an upgrade replaces this subscription,
    // record the time it was superseded.
    // Null means it has not been superseded.
    public DateTime? SupersededAt { get; set; }

    // Link to the replacement subscription.
    // Null for subscriptions that were not upgraded.
    public int? SupersededBySubscriptionId { get; set; }

    // Self-reference to the new subscription.
    [ForeignKey(nameof(SupersededBySubscriptionId))]
    public ClinicSubscription? SupersededBySubscription
    {
        get;
        set;
    }

    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Explicit validation for API/service use.
    // EF Core SaveChanges does not automatically invoke
    // IValidatableObject.
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (HospitalId <= 0)
        {
            yield return new ValidationResult(
                "A valid clinic is required.",
                new[] { nameof(HospitalId) });
        }

        if (SubscriptionPlanId <= 0)
        {
            yield return new ValidationResult(
                "A valid subscription plan is required.",
                new[] { nameof(SubscriptionPlanId) });
        }

        if (StartDate == default ||
            EndDate == default ||
            EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "EndDate must be later than StartDate.",
                new[] { nameof(StartDate), nameof(EndDate) });
        }

        if (!IncludesCms)
        {
            yield return new ValidationResult(
                "CMS must be included in every clinic subscription.",
                new[] { nameof(IncludesCms) });
        }

        if (string.IsNullOrWhiteSpace(Status))
        {
            yield return new ValidationResult(
                "Subscription status is required.",
                new[] { nameof(Status) });
        }

        if (PurchasedMaxBranches < 1 ||
            PurchasedMaxBranches > 10000)
        {
            yield return new ValidationResult(
                "PurchasedMaxBranches must be between 1 and 10000.",
                new[] { nameof(PurchasedMaxBranches) });
        }

        if (PurchasedDailyAppointmentLimit < 1 ||
            PurchasedDailyAppointmentLimit > 1000000)
        {
            yield return new ValidationResult(
                "PurchasedDailyAppointmentLimit must be between 1 and 1000000.",
                new[] { nameof(PurchasedDailyAppointmentLimit) });
        }

        if (SupersededAt.HasValue &&
            SupersededAt.Value < StartDate)
        {
            yield return new ValidationResult(
                "SupersededAt cannot be earlier than StartDate.",
                new[] { nameof(SupersededAt) });
        }

        if (SupersededBySubscriptionId.HasValue &&
            SupersededBySubscriptionId.Value == Id &&
            Id > 0)
        {
            yield return new ValidationResult(
                "A subscription cannot supersede itself.",
                new[] { nameof(SupersededBySubscriptionId) });
        }
    }
}
