namespace AuthDemo.DTOs;

public class CreateScheduleDto
{
    public int? BranchId { get; set; }

    public int DoctorId { get; set; }

    public List<string>? Days { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set ; }

    // API request lo 09:00 AM format accept cheyyadaniki string
    public string WorkStart { get; set; } = string.Empty;

    public string WorkEnd { get; set; } = string.Empty;

    public string BreakStart { get; set; } = string.Empty;

    public string BreakEnd { get; set; } = string.Empty;
}