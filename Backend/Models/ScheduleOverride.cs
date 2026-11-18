namespace AuthDemo.Models;

public class ScheduleOverride
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public int DoctorId { get; set; }

    // Branch where this exception applies / target branch for BranchShift.
    public int? BranchId { get; set; }

    // Used only for BranchShift. Slots in this time range are removed from the source branch.
    public int? SourceBranchId { get; set; }

    public DateTime Date { get; set; }

    // Leave | TimeChange | BranchShift
    public string OverrideType { get; set; } = "TimeChange";

    public TimeSpan? WorkStart { get; set; }
    public TimeSpan? WorkEnd { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }

    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Doctor? Doctor { get; set; }
    public Branch? Branch { get; set; }
}
