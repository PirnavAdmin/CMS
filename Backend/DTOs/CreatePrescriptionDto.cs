namespace AuthDemo.DTOs;

public class CreatePrescriptionDto
{
    // =====================================================
    // APPOINTMENT
    // =====================================================

    public int AppointmentId
    { get; set; }

    // =====================================================
    // PATIENT
    // =====================================================

    public int PatientId
    { get; set; }

    // =====================================================
    // PRESCRIPTION DETAILS
    // =====================================================

    public string? Diagnosis
    { get; set; }

    public string? Instructions
    { get; set; }

    public DateTime FollowUpDate
    { get; set; }

    // =====================================================
    // STATUS
    // =====================================================

    // Draft
    // Completed

    public string? Status
    { get; set; }

    // =====================================================
    // MEDICINES
    // =====================================================

    public List<CreatePrescriptionItemDto>
        Medicines
    { get; set; }
        = new();

    public List<CreateLabTestOrderDto> LabTests { get; set; } = new();

    // Used when the clinic subscription does not include the internal Lab module.
    public List<string> ExternalTestNames { get; set; } = new();
}