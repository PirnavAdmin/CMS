namespace AuthDemo.Models;

public class NotificationRead
{
    public int Id { get; set; }
    public int NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int? SuperAdminId { get; set; }
    public SuperAdmin? SuperAdmin { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}
