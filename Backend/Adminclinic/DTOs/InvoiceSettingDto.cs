namespace AuthDemo.DTOs;

public class CreateInvoiceSettingDto
{
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
}

public class UpdateInvoiceSettingDto
{
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
}
