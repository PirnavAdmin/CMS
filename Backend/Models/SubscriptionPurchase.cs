
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthDemo.Models;

public class SubscriptionPurchase : IValidatableObject
{
    public int Id { get; set; }

    // ============================================
    // CUSTOMER DETAILS
    // ============================================

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string MobileNumber { get; set; } = string.Empty;

    // ============================================
    // PURCHASED SUBSCRIPTION PLAN
    // ============================================

    public int SubscriptionPlanId { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    // Actual amount payable for this purchase.
    // For upgrades, this is after unused credit.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal Amount { get; set; }

    // Initial, Renewal or Upgrade.
    [Required]
    [MaxLength(20)]
    public string PurchaseType { get; set; } = "Initial";

    // Immutable purchased entitlement snapshots.
    public int PurchasedDurationMonths { get; set; }

    public bool PurchasedIncludesCms { get; set; } = true;
    public bool PurchasedIncludesLab { get; set; } = false;
    public bool PurchasedIncludesPharmacy { get; set; } = false;

    // Maximum active branches purchased.
    [Range(1, 10000)]
    public int PurchasedMaxBranches { get; set; } = 1;

    // Maximum daily appointments purchased.
    [Range(1, 1000000)]
    public int PurchasedDailyAppointmentLimit { get; set; } = 20;

    // ============================================
    // UPGRADE DETAILS
    // ============================================

    // Existing subscription being replaced.
    // Required for an Upgrade purchase.
    public int? OriginalSubscriptionId { get; set; }

    [ForeignKey(nameof(OriginalSubscriptionId))]
    public ClinicSubscription? OriginalSubscription { get; set; }

    // Original subscription's eligible paid amount.
    // Used as the basis for prorated credit.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal OriginalPaidAmount { get; set; }

    // Credit calculated for unused subscription time.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal UnusedCreditAmount { get; set; }

    // New plan price before credit is deducted.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal GrossNewPlanAmount { get; set; }

    // Final amount after applying eligible credit.
    [Column(TypeName = "decimal(18,2)")]
    [Range(typeof(decimal), "0.00", "999999999.00")]
    public decimal FinalPayableAmount { get; set; }

    // FreshTerm for upgrades.
    // Other purchase types leave this empty.
    [MaxLength(30)]
    public string? UpgradeMode { get; set; }

    // ============================================
    // PAYMENT DETAILS
    // ============================================

    [MaxLength(30)]
    public string PaymentMethod { get; set; } = string.Empty;

    // Pending, Paid, Failed.
    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Pending";

    [MaxLength(150)]
    public string? GatewayOrderId { get; set; }

    [MaxLength(150)]
    public string? GatewayPaymentId { get; set; }

    // Credential used to create this CMS Razorpay order.
    // Nullable for purchases created before credential-version tracking.
    public int? RazorpayCredentialId { get; set; }

    // ============================================
    // PAYMENTS - PHASE 4C RAZORPAY VERIFICATION
    // ============================================

    // Test or Live mode used when the original
    // Razorpay order was created.
    //
    // Must be set by the backend during order creation.
    // Do not change it when merchant settings change.
    [MaxLength(20)]
    public string? GatewayMode { get; set; }

    // Set to true only after the backend independently
    // verifies the captured Razorpay payment.
    //
    // Never set this directly from frontend input.
    public bool IsGatewayPaymentVerified { get; set; } = false;

    // UTC timestamp of successful gateway verification.
    public DateTime? GatewayVerifiedAt { get; set; }

    // ============================================
    // PAYMENT TRANSACTION DETAILS
    // ============================================

    [MaxLength(150)]
    public string? TransactionReference { get; set; }

    [MaxLength(500)]
    public string? PaymentFailureReason { get; set; }

    [Range(0, int.MaxValue)]
    public int PaymentAttemptCount { get; set; } = 0;

    public DateTime? LastPaymentAttemptAt { get; set; }

    // ============================================
    // ADMIN AND CLINIC ASSIGNMENT
    // ============================================

    // Initial purchase: Created when the self-registered Admin is linked;
    // payment verification is independently required before clinic creation.
    // Renewal / Upgrade: existing Admin is already assigned.
    [MaxLength(30)]
    public string AdminCreationStatus { get; set; } = "Pending";

    public int? AdminUserId { get; set; }

    public int? HospitalId { get; set; }

    // ============================================
    // TIMESTAMPS
    // ============================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    // ============================================
    // VALIDATION
    // ============================================

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (PurchaseType != "Initial" &&
            PurchaseType != "Renewal" &&
            PurchaseType != "Upgrade")
        {
            yield return new ValidationResult(
                "Purchase type must be Initial, Renewal or Upgrade.",
                new[] { nameof(PurchaseType) });
        }

        if (SubscriptionPlanId <= 0)
        {
            yield return new ValidationResult(
                "A valid subscription plan is required.",
                new[] { nameof(SubscriptionPlanId) });
        }

        if (Amount < 0)
        {
            yield return new ValidationResult(
                "Purchase amount cannot be negative.",
                new[] { nameof(Amount) });
        }

        if (PurchasedDurationMonths != 1 &&
            PurchasedDurationMonths != 3 &&
            PurchasedDurationMonths != 6 &&
            PurchasedDurationMonths != 12)
        {
            yield return new ValidationResult(
                "Duration must be 1, 3, 6 or 12 months.",
                new[] { nameof(PurchasedDurationMonths) });
        }

        if (!PurchasedIncludesCms)
        {
            yield return new ValidationResult(
                "CMS must be included in every subscription.",
                new[] { nameof(PurchasedIncludesCms) });
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

        // ========================================
        // PURCHASE TYPE VALIDATION
        // ========================================

        if (PurchaseType == "Upgrade")
        {
            if (!OriginalSubscriptionId.HasValue ||
                OriginalSubscriptionId.Value <= 0)
            {
                yield return new ValidationResult(
                    "An upgrade must reference the existing subscription.",
                    new[] { nameof(OriginalSubscriptionId) });
            }

            if (UpgradeMode != "FreshTerm")
            {
                yield return new ValidationResult(
                    "UpgradeMode must be FreshTerm for upgrades.",
                    new[] { nameof(UpgradeMode) });
            }

            if (!HospitalId.HasValue || HospitalId.Value <= 0 ||
                !AdminUserId.HasValue || AdminUserId.Value <= 0)
            {
                yield return new ValidationResult(
                    "An upgrade requires an existing clinic and Admin.",
                    new[]
                    {
                        nameof(HospitalId),
                        nameof(AdminUserId)
                    });
            }
        }

        // ========================================
        // AMOUNT VALIDATION
        // ========================================

        if (PurchaseType == "Upgrade")
        {
            decimal expectedPayable = Math.Max(
                0m,
                GrossNewPlanAmount - UnusedCreditAmount);

            if (FinalPayableAmount != expectedPayable)
            {
                yield return new ValidationResult(
                    "Final payable amount must equal the new plan " +
                    "price minus eligible unused credit.",
                    new[] { nameof(FinalPayableAmount) });
            }

            if (Amount != FinalPayableAmount)
            {
                yield return new ValidationResult(
                    "Amount must match FinalPayableAmount.",
                    new[] { nameof(Amount) });
            }

            if (UnusedCreditAmount > GrossNewPlanAmount)
            {
                yield return new ValidationResult(
                    "Unused credit cannot exceed the new plan price " +
                    "for this purchase.",
                    new[] { nameof(UnusedCreditAmount) });
            }
        }
        else
        {
            if (Amount <= 0)
            {
                yield return new ValidationResult(
                    "Initial and renewal purchases must have " +
                    "a positive payable amount.",
                    new[] { nameof(Amount) });
            }
        }

        // ========================================
        // PAYMENT VALIDATION
        // ========================================

        // An empty payment method is allowed while
        // a checkout is being prepared.
        if (!string.IsNullOrWhiteSpace(PaymentMethod) &&
            PaymentMethod != "UPI" &&
            PaymentMethod != "Card" &&
            PaymentMethod != "NetBanking")
        {
            yield return new ValidationResult(
                "Payment method must be UPI, Card or NetBanking.",
                new[] { nameof(PaymentMethod) });
        }

        if (PaymentStatus != "Pending" &&
            PaymentStatus != "Paid" &&
            PaymentStatus != "Failed")
        {
            yield return new ValidationResult(
                "Payment status must be Pending, Paid or Failed.",
                new[] { nameof(PaymentStatus) });
        }

        if (AdminCreationStatus != "Pending" &&
            AdminCreationStatus != "Created")
        {
            yield return new ValidationResult(
                "Admin creation status must be Pending or Created.",
                new[] { nameof(AdminCreationStatus) });
        }

        if (PaymentStatus == "Paid" && !PaidAt.HasValue)
        {
            yield return new ValidationResult(
                "Paid purchases require a payment timestamp.",
                new[] { nameof(PaidAt) });
        }

        if (ActivatedAt.HasValue &&
            (PaymentStatus != "Paid" ||
             !PaidAt.HasValue ||
             !AdminUserId.HasValue ||
             !HospitalId.HasValue))
        {
            yield return new ValidationResult(
                "An activated purchase must be paid " +
                "and assigned to an Admin and clinic.",
                new[] { nameof(ActivatedAt) });
        }

        // ========================================
        // PAYMENTS - PHASE 4C VERIFICATION VALIDATION
        // ========================================

        // A verified payment must reference a real
        // gateway order and payment.
        if (IsGatewayPaymentVerified &&
            (string.IsNullOrWhiteSpace(GatewayOrderId) ||
             string.IsNullOrWhiteSpace(GatewayPaymentId) ||
             (GatewayMode != "Test" && GatewayMode != "Live") ||
             !GatewayVerifiedAt.HasValue))
        {
            yield return new ValidationResult(
                "A verified Razorpay payment requires an order ID, " +
                "payment ID, gateway mode and verification timestamp.",
                new[]
                {
                    nameof(IsGatewayPaymentVerified),
                    nameof(GatewayOrderId),
                    nameof(GatewayPaymentId),
                    nameof(GatewayMode),
                    nameof(GatewayVerifiedAt)
                });
        }

        // A verification timestamp must not exist
        // without verified gateway evidence.
        if (!IsGatewayPaymentVerified &&
            GatewayVerifiedAt.HasValue)
        {
            yield return new ValidationResult(
                "GatewayVerifiedAt requires a verified gateway payment.",
                new[]
                {
                    nameof(IsGatewayPaymentVerified),
                    nameof(GatewayVerifiedAt)
                });
        }
    }
}
