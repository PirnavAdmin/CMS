namespace AuthDemo.DTOs;

public class EmailSettingsRequest
{
    public string? SenderName { get; set; }
    public string? FromEmail { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? Status { get; set; }
    public string? ConfigurationNotes { get; set; }
}
