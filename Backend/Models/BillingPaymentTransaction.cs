namespace AuthDemo.Models;

public class BillingPaymentTransaction
{
    public int Id { get; set; }
    public int BillingId { get; set; }
    public Billing Billing { get; set; } = null!;
    public int HospitalId { get; set; }
    public int? BranchId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = "Paid";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
}

