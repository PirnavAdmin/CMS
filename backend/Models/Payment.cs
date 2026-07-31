namespace AuthDemo.Models;

public class Payment
{
    public int Id { get; set; }


    // Appointment reference after booking
    public int? AppointmentId { get; set; }

    public Appointment? Appointment { get; set; }
    public int DoctorId { get; set; }

    public Doctor Doctor { get; set; }

    public int BranchId { get; set; }

    public Branch Branch { get; set; }

    public DateTime AppointmentDate { get; set; }

    public TimeSpan AppointmentTime { get; set; }

    public int HospitalId { get; set; }


    // Patient

    public int PatientId { get; set; }

    public Patient Patient { get; set; }



    // Payment Details

    public decimal Amount { get; set; }


    // UPI / Card / Cash / Online

    public string PaymentMode { get; set; }



    // Pending / Paid / Failed

    public string Status { get; set; }
        = "Pending";



    // Gateway transaction id

    public string? TransactionId { get; set; }



    public DateTime? PaymentDate { get; set; }



    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;
}