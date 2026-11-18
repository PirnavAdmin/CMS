using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Models;

public class PharmacyOtcSaleItem
{
    public int Id { get; set; }
    public int PharmacyOtcSaleId { get; set; }
    public PharmacyOtcSale PharmacyOtcSale { get; set; } = null!;

    public int MedicineId { get; set; }
    public int StockBatchId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    // Server-populated price/tax snapshots, never trusted from request JSON.
    [Precision(18, 2)]
    public decimal UnitPrice { get; set; }

    [Precision(5, 2)]
    public decimal GstPercentage { get; set; }
}
