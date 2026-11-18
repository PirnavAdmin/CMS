using System.ComponentModel.DataAnnotations;
namespace AuthDemo.Models;
public sealed class CmsSubscriptionSettlementRecord
{
    public long Id { get; set; }
    public int SubscriptionPurchaseId { get; set; }
    [Required, MaxLength(150)] public string GatewayPaymentId { get; set; } = "";
    [Required, MaxLength(150)] public string SettlementId { get; set; } = "";
    [Required, MaxLength(20)] public string Mode { get; set; } = "Test";
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public DateTime SettledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
