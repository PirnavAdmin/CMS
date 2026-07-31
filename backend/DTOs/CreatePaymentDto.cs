namespace AuthDemo.DTOs;

public class CreatePaymentDto
{
    public int AppointmentId { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public int BranchId { get; set; }

    public DateTime Date { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string PaymentMode { get; set; } = string.Empty;
}