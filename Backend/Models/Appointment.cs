namespace AuthDemo.Models;

public class Appointment
{
    public int Id { get; set; }


    // =====================================================
    // DOCTOR
    // =====================================================

    public int DoctorId { get; set; }

    public Doctor Doctor { get; set; }
    // =====================================================
    // BOOKING SOURCE
    // =====================================================

    // Online  = Patient portal booking
    // Offline = Receptionist/Admin booking

    public string BookingType { get; set; } = "Offline";

    // =====================================================
    // PATIENT
    // =====================================================

    public int PatientId { get; set; }

    public Patient Patient { get; set; }


    // =====================================================
    // APPOINTMENT DATE & TIME
    // =====================================================

    public DateTime Date { get; set; }

    public TimeSpan StartTime { get; set; }


    // =====================================================
    // TOKEN NUMBER
    // =====================================================

    public string TokenNumber { get; set; }


    // =====================================================
    // CONSULTATION PAYMENT
    // =====================================================

    // Doctor consultation fee
    public decimal ConsultationFee { get; set; }


    // Cash / UPI / Card / Online
    public string? PaymentMode { get; set; }


    // Pending / Paid / Failed / Refunded
    public string PaymentStatus { get; set; } = "Pending";


    // Payment completed date
    public DateTime? PaymentDate { get; set; }


    // Transaction Id from payment gateway
    public string? TransactionId { get; set; }


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


    // =====================================================
    // STATUS FLOW
    // =====================================================

    // Waiting
    // Confirmed
    // InProgress
    // PrescriptionAdded
    // Completed
    // Cancelled

    public string Status { get; set; } = "Waiting";


    // =====================================================
    // MULTI HOSPITAL SUPPORT
    // =====================================================

    public int HospitalId { get; set; }

    public Hospital Hospital { get; set; }


    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }


    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}