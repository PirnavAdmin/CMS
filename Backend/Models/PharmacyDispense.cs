
using System;
using System.Collections.Generic;

namespace AuthDemo.Models;

public class PharmacyDispense
{
    public int Id { get; set; }

    public int HospitalId { get; set; }

    public int BranchId { get; set; }

    public int PrescriptionId { get; set; }

    public int PharmacistUserId { get; set; }

    public DateTime DispensedAt { get; set; } = DateTime.UtcNow;

    public List<PharmacyDispenseItem> Items { get; set; } = new();
}
