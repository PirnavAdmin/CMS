namespace AuthDemo.Models;

public class Setting
{
    public int Id { get; set; }

    // GENERAL
    public string? ApplicationName { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
    public string? GeneralStatus { get; set; }
    public string? ConfigurationNotes { get; set; }

    // EMAIL
    public string? SenderName { get; set; }
    public string? FromEmail { get; set; }
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? EmailStatus { get; set; }
    public string? EmailConfigurationNotes { get; set; }

    // SMS
    public string? SmsConfigurationName { get; set; }
    public string? SmsProvider { get; set; }
    public string? SmsSenderId { get; set; }
    public string? SmsApiKey { get; set; }
    public string? SmsApiSecret { get; set; }
    public string? SmsStatus { get; set; }
    public string? SmsConfigurationNotes { get; set; }

    // PAYMENT
    public string? PaymentConfigurationName { get; set; }
    public string? GatewayProvider { get; set; }
    public string? MerchantId { get; set; }
    public string? PublicKey { get; set; }
    public string? SecretKey { get; set; }
    public string? PaymentMode { get; set; }
    public string? PaymentStatus { get; set; }
    public string? PaymentConfigurationNotes { get; set; }

    // LEGACY FIELDS - keep existing code working
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? Address { get; set; }
    public bool EmailNotificationsEnabled { get; set; }
    public bool SmsNotificationsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
