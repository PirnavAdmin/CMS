namespace AuthDemo.DTOs;

public class BookSlotDto
{
    // =====================================================
    // DOCTOR
    // =====================================================

    public int? BranchId { get; set; }

    public int DoctorId { get; set; }


    // =====================================================
    // PATIENT
    // =====================================================

    public int PatientId { get; set; }


    // =====================================================
    // APPOINTMENT DATE & TIME
    // =====================================================

    public DateTime Date { get; set; }

    public string StartTime { get; set; } = string.Empty;


    // =====================================================
    // PAYMENT DETAILS
    // Payment should be completed before this DTO is used
    // to create appointment
    // =====================================================


    // Cash / Card / UPI / Online
    public string PaymentMode { get; set; } = string.Empty;


    // Transaction reference from payment gateway
    // Required for UPI/Card/Online
    public string? TransactionId { get; set; }


    // Consultation amount paid
    public decimal PaidAmount { get; set; }


    // Paid / Failed / Pending
    public string PaymentStatus { get; set; } = "Paid";


    // =====================================================
    // RECEPTIONIST FILLED DETAILS
    // =====================================================

    public string? ChiefComplaints { get; set; }

    public string? BloodPressure { get; set; }

    public string? SugarLevel { get; set; }

    public string? Temperature { get; set; }

    public string? Weight { get; set; }

    public string? PulseRate { get; set; }

    public string? RespiratoryRate { get; set; }
}