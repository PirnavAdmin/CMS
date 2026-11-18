
using System;

namespace AuthDemo.Models;

public class PharmacyBill
{
    public int Id { get; set; }

    public int HospitalId { get; set; }

    public int BranchId { get; set; }

    public int PharmacyDispenseId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string Status { get; set; } = "Unpaid";

    public string? PaymentMethod { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }
}
