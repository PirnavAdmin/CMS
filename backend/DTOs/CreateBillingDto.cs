namespace AuthDemo.DTOs;

public class CreateBillingDto
{
    public int AppointmentId { get; set; }
    // OP, Lab, Pharmacy
    public string BillingType { get; set; } = "OP";
    public decimal ConsultationCharge { get; set; }
    public decimal MedicineCharge { get; set; }
    public decimal LabCharge { get; set; }
    public decimal GstPercentage { get; set; }
    public string? PaymentMode { get; set; }
    public string Status { get; set; } = "Paid";
}
