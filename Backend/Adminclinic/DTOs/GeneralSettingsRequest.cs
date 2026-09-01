namespace AuthDemo.DTOs;

public class GeneralSettingsRequest
{
    public string? AppName { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? ConfigurationNotes { get; set; }
}
