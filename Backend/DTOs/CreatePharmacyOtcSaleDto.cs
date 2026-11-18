using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

// Hospital, branch, and pharmacist must be resolved from authenticated context.
public class CreatePharmacyOtcSaleDto
{
    [Range(1, int.MaxValue)]
    public int AppointmentId { get; set; }

    [Required, MinLength(1)]
    public List<CreatePharmacyOtcSaleItemDto> Items { get; set; } = new();
}

public class CreatePharmacyOtcSaleItemDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
