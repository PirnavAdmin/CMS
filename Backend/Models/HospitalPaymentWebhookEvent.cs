using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class HospitalPaymentWebhookEvent
{
    public long Id { get; set; }
    [Required, MaxLength(150)] public string EventId { get; set; } = "";
    [Required, MaxLength(100)] public string EventType { get; set; } = "";
    public int HospitalId { get; set; }
    [Required, MaxLength(20)] public string Mode { get; set; } = "Test";
    [MaxLength(150)] public string? GatewayOrderId { get; set; }
    [Required, MaxLength(30)] public string ProcessingStatus { get; set; } = "Received";
    [MaxLength(1000)] public string? LastError { get; set; }
    [Required] public string PayloadJson { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
