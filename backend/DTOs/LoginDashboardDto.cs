namespace AuthDemo.DTOs;

public class LoginDashboardDto
{
    public int TotalUsers { get; set; }

    public int OnlineUsers { get; set; }

    public int TodayLogins { get; set; }

    public string? LastLoginUser { get; set; }

    public DateTime? LastLoginTime { get; set; }
}