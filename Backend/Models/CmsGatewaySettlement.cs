using System.ComponentModel.DataAnnotations;
namespace AuthDemo.Models;
public sealed class CmsGatewaySettlement
{
    public long Id { get; set; }
    [Required, MaxLength(20)] public string Mode { get; set; } = "Test";
    [Required, MaxLength(150)] public string SettlementId { get; set; } = "";
    [Required, MaxLength(30)] public string Status { get; set; } = "created";
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    [MaxLength(150)] public string? Utr { get; set; }
    public DateTime SettledAt { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}
