
namespace AuthDemo.DTOs;

public class CreatePrescriptionItemDto
{
    // =====================================================
    // MEDICINE DETAILS
    // =====================================================

    public string? MedicineName
    { get; set; }

    public string? Dosage
    { get; set; }

    public string? Frequency
    { get; set; }

    public string? Duration
    { get; set; }

    public string? Notes
    { get; set; }

    // =====================================================
    // PHARMACY INTEGRATION - NEW FIELDS
    // =====================================================

    // Medicine ID selected from the pharmacy catalog.
    // Nullable for compatibility with old prescriptions.
    public int? MedicineId
    { get; set; }

    // Total number of tablets/units prescribed.
    public int PrescribedQuantity
    { get; set; } = 0;
}
