namespace AuthDemo.DTOs;

public class LoginHistoryDto
{
    public string UserName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public int? ClinicId { get; set; }

    public int? BranchId { get; set; }

    public string? IpAddress { get; set; }

    public string? Browser { get; set; }

    public string? Device { get; set; }

    public DateTime LoginTime { get; set; }

    public DateTime? LogoutTime { get; set; }

    public bool IsOnline { get; set; }
}