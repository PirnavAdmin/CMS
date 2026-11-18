
namespace AuthDemo.Models;

public class PharmacyDispenseItem
{
    public int Id { get; set; }

    public int PharmacyDispenseId { get; set; }

    public int PrescriptionItemId { get; set; }

    public int StockBatchId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
