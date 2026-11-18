
namespace AuthDemo.Models;

public class Hospital
{
    public int Id { get; set; }

    // =====================================================
    // HOSPITAL DETAILS
    // =====================================================

    public string? Name { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? District { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }

    // =====================================================
    // INVOICE / REGISTRATION DETAILS
    // =====================================================

    // GST registration number, if applicable
    public string? Gstin { get; set; }

    // Hospital / Clinic registration number
    public string? RegistrationNumber { get; set; }

    // Hospital logo URL or relative file path
    public string? LogoUrl { get; set; }

    // =====================================================
    // ACTIVE STATUS
    // =====================================================

    public bool IsActive { get; set; } = true;

    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
