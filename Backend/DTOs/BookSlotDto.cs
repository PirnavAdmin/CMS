using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class BookSlotDto
{
    // Branch must be provided for a receptionist booking.
    // Nullable is retained for compatibility with existing clients.
    [Required(ErrorMessage = "BranchId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than zero.")]
    public int? BranchId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than zero.")]
    public int DoctorId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than zero.")]
    public int PatientId { get; set; }

    [Required(ErrorMessage = "Date is required.")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "StartTime is required.")]
    public string StartTime { get; set; } = string.Empty;

    // Cash / Card / UPI / Online
    [Required(ErrorMessage = "PaymentMode is required.")]
    public string PaymentMode { get; set; } = string.Empty;

    // Required for UPI / Card / Online; validated by appointment service.
    public string? TransactionId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "PaidAmount must be greater than zero.")]
    public decimal PaidAmount { get; set; }

    [Required(ErrorMessage = "PaymentStatus is required.")]
    public string PaymentStatus { get; set; } = "Paid";

    public string? ChiefComplaints { get; set; }
    public string? BloodPressure { get; set; }
    public string? SugarLevel { get; set; }
    public string? Temperature { get; set; }
    public string? Weight { get; set; }
    public string? PulseRate { get; set; }
    public string? RespiratoryRate { get; set; }
}
