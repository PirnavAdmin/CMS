
namespace AuthDemo.Models;

public class InvoiceSetting
{
    public int Id { get; set; }

    // =====================================================
    // HOSPITAL RELATIONSHIP
    // =====================================================

    public int HospitalId { get; set; }

    public Hospital? Hospital { get; set; }

    // =====================================================
    // INVOICE TEMPLATE
    // =====================================================

    public string InvoiceTemplate { get; set; } = "Professional";

    // =====================================================
    // CLINIC DETAILS
    // =====================================================

    public string? ClinicName { get; set; }

    public string? ClinicAddress { get; set; }

    public string? ClinicPhone { get; set; }

    public string? ClinicEmail { get; set; }

    // =====================================================
    // TAX AND REGISTRATION DETAILS
    // =====================================================

    public string? GstNumber { get; set; }

    public string? RegistrationNumber { get; set; }

    // =====================================================
    // INVOICE HEADER
    // =====================================================

    public string? HeaderTitle { get; set; }

    public string? HeaderSubtitle { get; set; }

    // =====================================================
    // INVOICE FOOTER
    // =====================================================

    public string? FooterNote { get; set; }

    // =====================================================
    // INVOICE APPEARANCE
    // =====================================================

    public string AccentColor { get; set; } = "#0f9d9d";

    public string? LogoUrl { get; set; }

    // =====================================================
    // AUDIT FIELDS
    // =====================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
