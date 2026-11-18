using System.ComponentModel.DataAnnotations;
namespace AuthDemo.Models;
public sealed class HospitalPaymentDispute
{
    public long Id { get; set; }
    public int HospitalId { get; set; }
    public long HospitalPaymentTransactionId { get; set; }
    public HospitalPaymentTransaction Transaction { get; set; } = null!;
    [Required, MaxLength(150)] public string GatewayDisputeId { get; set; } = "";
    [Required, MaxLength(150)] public string GatewayPaymentId { get; set; } = "";
    public decimal Amount { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = "INR";
    [MaxLength(300)] public string? Reason { get; set; }
    [Required, MaxLength(40)] public string Status { get; set; } = "Created";
    public DateTime? EvidenceDueBy { get; set; }
    public string PayloadJson { get; set; } = "";
    [Required] public string EvidenceDocumentIdsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}
