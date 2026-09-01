namespace AuthDemo.Models;

public class Billing
{
    // =====================================================
    // PRIMARY KEY
    // =====================================================

    public int Id { get; set; }

    // =====================================================
    // BILL TYPE
    // OP / Lab / Pharmacy
    // =====================================================

    public string BillingType { get; set; } = "OP";

    // =====================================================
    // APPOINTMENT
    // =====================================================

    public int AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }

    // =====================================================
    // PATIENT
    // =====================================================

    public int PatientId { get; set; }

    public Patient? Patient { get; set; }

    // =====================================================
    // DOCTOR
    // =====================================================

    public int DoctorId { get; set; }

    public Doctor? Doctor { get; set; }

    // =====================================================
    // HOSPITAL
    // =====================================================

    public int HospitalId { get; set; }

    public Hospital? Hospital { get; set; }

    // =====================================================
    // BRANCH
    // =====================================================

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    // =====================================================
    // BILL AMOUNTS
    // =====================================================

    public decimal ConsultationCharge { get; set; }

    public decimal LabCharge { get; set; }

    public decimal MedicineCharge { get; set; }

    public decimal SubTotal { get; set; }

    public decimal Discount { get; set; }
    public decimal GstPercentage { get; set; }

    public decimal GstAmount { get; set; }

    public decimal TotalAmount { get; set; }

    // =====================================================
    // PAYMENT
    // =====================================================

    public string PaymentMode { get; set; } = "Cash";

    // Pending / Paid
    public string Status { get; set; } = "Pending";

    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}