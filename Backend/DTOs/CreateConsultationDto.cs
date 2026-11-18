namespace AuthDemo.DTOs;

public class CreateConsultationDto
{
    public int AppointmentId { get; set; }

    public int PatientId { get; set; }

    public string? Diagnosis { get; set; }

    public string? ClinicalNotes { get; set; }

    // Doctor selected diagnostic tests
    public List<string> DiagnosisTests { get; set; } = new();
}