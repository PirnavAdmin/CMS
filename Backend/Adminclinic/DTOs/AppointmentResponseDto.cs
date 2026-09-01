namespace AuthDemo.DTOs;

public class AppointmentResponseDto
{
    // =====================================================
    // APPOINTMENT
    // =====================================================
    public int? BranchId { get; set; }

    public string? BranchName { get; set; }

    public int HospitalId { get; set; }

    public int Id { get; set; }

    public int DoctorId { get; set; }

    public int PatientId { get; set; }

    public string TokenNumber { get; set; } = string.Empty;

    //BOOKING SOURCE//
    public string BookingType { get; set; } = string.Empty;

    // =====================================================
    // PATIENT
    // =====================================================

    public string? PatientName { get; set; }

    public string? PatientCode { get; set; }

    public int Age { get; set; }

    public string? Gender { get; set; }

    public string? Phone { get; set; }

    public string? BloodGroup { get; set; }

    // =====================================================
    // DOCTOR
    // =====================================================

    public string? DoctorName { get; set; }

    public string? DoctorSpecialization { get; set; }

    // =====================================================
    // DATE & TIME
    // =====================================================

    public DateTime Date { get; set; }

    public string? Time { get; set; }

    // =====================================================
    // RECEPTIONIST DETAILS
    // =====================================================

    public string? ChiefComplaints { get; set; }

    public string? BloodPressure { get; set; }

    public string? SugarLevel { get; set; }

    public string? Temperature { get; set; }

    public string? Weight { get; set; }

    public string? PulseRate { get; set; }

    public string? RespiratoryRate { get; set; }

    // =====================================================
    // PAYMENT DETAILS (Added to resolve CS0117 errors)
    // =====================================================

    public decimal ConsultationFee { get; set; }

    public string? PaymentMode { get; set; }

    public string? PaymentStatus { get; set; }

    // =====================================================
    // STATUS
    // =====================================================

    public string? Status { get; set; }
}