namespace AuthDemo.Models;

public class DoctorBranch
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int HospitalId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
