namespace AuthDemo.DTOs;

public class SmsSettingsRequest
{
    public string? ConfigurationName { get; set; }
    public string? Provider { get; set; }
    public string? SenderId { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? Status { get; set; }
    public string? ConfigurationNotes { get; set; }
}
