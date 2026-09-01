namespace AuthDemo.Models;

public class LabTestMaster
{
    public int Id { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SampleType { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public decimal Price { get; set; }
    public int TurnaroundHours { get; set; }
    public string? Instructions { get; set; }
    public int HospitalId { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Hospital? Hospital { get; set; }
    public Branch? Branch { get; set; }
}
