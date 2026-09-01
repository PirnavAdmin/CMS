using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreateBillingDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Valid AppointmentId is required.")]
    public int AppointmentId { get; set; }

    // OP / Lab / Pharmacy
    [Required(ErrorMessage = "BillingType is required.")]
    public string BillingType { get; set; } = "OP";

    // Used only for OP billing.
    // When zero, backend can use the doctor's consultation fee.
    [Range(
        0,
        double.MaxValue,
        ErrorMessage = "Consultation charge cannot be negative.")]
    public decimal ConsultationCharge { get; set; }

    // Used only for Lab billing.
    [Range(
        0,
        double.MaxValue,
        ErrorMessage = "Lab charge cannot be negative.")]
    public decimal LabCharge { get; set; }

    // Used only for Pharmacy billing.
    [Range(
        0,
        double.MaxValue,
        ErrorMessage = "Medicine charge cannot be negative.")]
    public decimal MedicineCharge { get; set; }

    [Range(
        0,
        100,
        ErrorMessage = "GST percentage must be between 0 and 100.")]
    public decimal GstPercentage { get; set; }

    // Cash / Card / UPI / Online
    public string? PaymentMode { get; set; } = "Cash";

    // Pending / Paid
    public string Status { get; set; } = "Pending";

    public decimal Discount { get; set; }
}