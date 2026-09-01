namespace AuthDemo.Models;

public class InvoiceSetting
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
    public string InvoiceTemplate { get; set; } = "Professional";
    public string? HeaderTitle { get; set; }
    public string? HeaderSubtitle { get; set; }
    public string? ClinicAddress { get; set; }
    public string? ClinicPhone { get; set; }
    public string? ClinicEmail { get; set; }
    public string? GstNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? FooterNote { get; set; }
    public string AccentColor { get; set; } = "#0f9d9d";
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
