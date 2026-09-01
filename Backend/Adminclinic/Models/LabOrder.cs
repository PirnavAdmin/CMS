namespace AuthDemo.Models;

public class LabOrder
{
    public int Id { get; set; }
    public int? PrescriptionLabTestId { get; set; }
    public PrescriptionLabTest? PrescriptionLabTest { get; set; }
    public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int? LabTestMasterId { get; set; }
    public LabTestMaster? LabTestMaster { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SampleType { get; set; }
    public decimal Price { get; set; }
    public string? Instructions { get; set; }
    public string Priority { get; set; } = "Routine";
    public int HospitalId { get; set; }
    public int? BranchId { get; set; }
    public string Status { get; set; } = "Ordered";
    public string? ResultValue { get; set; }
    public string? ResultUnit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? Remarks { get; set; }
    public string? ReportFilePath { get; set; }
    public string? ReportFileName { get; set; }
    public string? ReportContentType { get; set; }
    public int? TechnicianUserId { get; set; }
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SampleCollectedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
