
using System;

namespace AuthDemo.Models;

public class PharmacyPayment
{
    public int Id { get; set; }

    public int PharmacyBillId { get; set; }

    public int HospitalId { get; set; }

    public int BranchId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
