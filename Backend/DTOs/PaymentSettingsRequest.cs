namespace AuthDemo.DTOs;

public class PaymentSettingsRequest
{
    public string? ConfigurationName { get; set; }
    public string? GatewayProvider { get; set; }
    public string? MerchantId { get; set; }
    public string? PublicKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Mode { get; set; }
    public string? Status { get; set; }
    public string? ConfigurationNotes { get; set; }
}
