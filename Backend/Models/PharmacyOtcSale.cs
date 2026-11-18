using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

// Additional non-prescription sale, kept separate from doctor-authorized dispensing.
public class PharmacyOtcSale
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public int BranchId { get; set; }
    public int AppointmentId { get; set; }
    public int PharmacistUserId { get; set; }

    // Draft -> Committed -> Billed, or Cancelled. Transitions enforced by service.
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CommittedAt { get; set; }
    public int? BillingId { get; set; }

    public List<PharmacyOtcSaleItem> Items { get; set; } = new();
}
