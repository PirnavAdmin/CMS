namespace AuthDemo.Models;

public class UserPermission
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int HospitalId { get; set; }
    public Hospital? Hospital { get; set; }

    public string Module { get; set; } = string.Empty;

    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }

    public int AssignedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
