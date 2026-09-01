namespace AuthDemo.DTOs;

// =====================================================
// GENERIC/INTERNAL SCHEDULE OVERRIDE DTO
// Used internally by ScheduleController validation
// =====================================================
public class ScheduleOverrideDto
{
    public int DoctorId { get; set; }

    // For Leave/TimeChange = selected branch
    // For BranchShift = target branch
    public int? BranchId { get; set; }

    // Used only for BranchShift
    public int? SourceBranchId { get; set; }

    public DateTime Date { get; set; }

    public string? OverrideType { get; set; }

    public string? WorkStart { get; set; }

    public string? WorkEnd { get; set; }

    public string? BreakStart { get; set; }

    public string? BreakEnd { get; set; }

    public string? Reason { get; set; }
}


// =====================================================
// LEAVE
// POST   /api/Schedule/overrides/leave
// PUT    /api/Schedule/overrides/leave/{id}
// DELETE /api/Schedule/overrides/leave/{id}
// =====================================================
public class LeaveScheduleOverrideDto
{
    public int DoctorId { get; set; }

    // Null can mean leave for all doctor's branches that day.
    // Supply BranchId for branch-specific leave.
    public int? BranchId { get; set; }

    public DateTime Date { get; set; }

    public string? Reason { get; set; }
}


// =====================================================
// TIME CHANGE
// POST /api/Schedule/overrides/time-change
// =====================================================
public class TimeChangeScheduleOverrideDto
{
    public int DoctorId { get; set; }

    public int BranchId { get; set; }

    public DateTime Date { get; set; }

    public string WorkStart { get; set; }
        = string.Empty;

    public string WorkEnd { get; set; }
        = string.Empty;

    public string? BreakStart { get; set; }

    public string? BreakEnd { get; set; }

    public string? Reason { get; set; }
}


// =====================================================
// BRANCH SHIFT
// POST /api/Schedule/overrides/branch-shift
// =====================================================
public class BranchShiftScheduleOverrideDto
{
    public int DoctorId { get; set; }

    // Branch doctor is moving FROM
    public int SourceBranchId { get; set; }

    // Branch doctor is moving TO
    public int TargetBranchId { get; set; }

    public DateTime Date { get; set; }

    public string WorkStart { get; set; }
        = string.Empty;

    public string WorkEnd { get; set; }
        = string.Empty;

    public string? BreakStart { get; set; }

    public string? BreakEnd { get; set; }

    public string? Reason { get; set; }
}