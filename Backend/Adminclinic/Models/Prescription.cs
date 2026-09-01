namespace AuthDemo.Models;

public class Prescription
{
    public int Id
    { get; set; }

    public int AppointmentId
    { get; set; }

    public Appointment? Appointment
    { get; set; }

    public int PatientId
    { get; set; }

    public Patient? Patient
    { get; set; }

    public string? Diagnosis
    { get; set; }

    public string? Instructions
    { get; set; }

    public DateTime FollowUpDate
    { get; set; }

    public string? Status
    { get; set; }
        = "Draft";

    public int HospitalId
    { get; set; }

    public Hospital? Hospital
    { get; set; }

    public ICollection<PrescriptionItem>
        Medicines
    { get; set; }
        = new List<PrescriptionItem>();

    public ICollection<PrescriptionLabTest> LabTests { get; set; }
        = new List<PrescriptionLabTest>();

    public bool IsPrinted { get; set; } = false;
    public DateTime? PrintedAt { get; set; }
    public int? PrintedByUserId { get; set; }

    public DateTime CreatedAt
    { get; set; }
        = DateTime.UtcNow;
}