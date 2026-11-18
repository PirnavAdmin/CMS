
namespace AuthDemo.Models;

public class PrescriptionItem
{
    public int Id { get; set; }

    // =====================================================
    // PRESCRIPTION
    // =====================================================

    public int PrescriptionId
    { get; set; }

    public Prescription? Prescription
    { get; set; }

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

    // Links prescribed medicine to Medicines table.
    // Nullable to support existing prescriptions.
    public int? MedicineId
    { get; set; }

    // Total quantity prescribed by the doctor.
    // Example: 2 tablets daily for 5 days = 10.
    public int PrescribedQuantity
    { get; set; } = 0;
}
