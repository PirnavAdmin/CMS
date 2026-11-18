using System;
using System.Collections.Generic;

namespace AuthDemo.DTOs;

public class PharmacyOtcSaleResponseDto
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int BranchId { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<PharmacyOtcSaleItemResponseDto> Items { get; set; } = new();
}

public class PharmacyOtcSaleItemResponseDto
{
    public int MedicineId { get; set; }
    public int StockBatchId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal GstPercentage { get; set; }
}
