namespace AuthDemo.Models;

public class PrescriptionLabTest
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string Priority { get; set; } = "Routine";
    public string Status { get; set; } = "Ordered";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
