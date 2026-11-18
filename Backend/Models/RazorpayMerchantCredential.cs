using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

/// <summary>
/// Razorpay credentials for the CMS owner or a specific hospital.
/// Historical settlement reconciliation must select a credential by its
/// explicitly pinned Id, then validate its merchant ownership and mode.
/// </summary>
public class RazorpayMerchantCredential
{
    public int Id { get; set; }

    // "CMS" or "Hospital".
    [Required, MaxLength(20)]
    public string MerchantType { get; set; } = "";

    // Null for CMS; required for a hospital merchant.
    public int? HospitalId { get; set; }

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    // Public Razorpay identifier (not a secret).
    [Required, MaxLength(200)]
    public string KeyId { get; set; } = "";

    // Encrypted with ASP.NET Core Data Protection. Never log or return these.
    [Required]
    public string EncryptedKeySecret { get; set; } = "";

    [Required]
    public string EncryptedWebhookSecret { get; set; } = "";

    // Active controls selection for NEW payment operations only.
    // A retired credential can still be needed for historical reconciliation.
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RetiredAt { get; set; }

    /// <summary>
    /// Checks the identity of a credential explicitly pinned to a historical
    /// hospital settlement. Does not select credentials or validate whether
    /// the key is still accepted by Razorpay.
    /// </summary>
    public bool MatchesHistoricalHospitalSettlement(int hospitalId, string mode)
    {
        return Id > 0
            && hospitalId > 0
            && HospitalId == hospitalId
            && string.Equals(MerchantType, "Hospital", StringComparison.Ordinal)
            && (string.Equals(mode, "Test", StringComparison.Ordinal)
                || string.Equals(mode, "Live", StringComparison.Ordinal))
            && string.Equals(Mode, mode, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(KeyId)
            && !string.IsNullOrWhiteSpace(EncryptedKeySecret);
    }
}
