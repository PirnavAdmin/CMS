namespace AuthDemo.Models;

public class RolePermission
{
    public int Id { get; set; }

    // Clinic / Hospital this permission belongs to

    public int? HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
 

    public int? AdminUserId { get; set; }

    // Specific Admin inside the clinic.
    // Allows multiple Admins in the same clinic
    // to have different module permissions.
    

    // Role will normally be "Admin"
    // when SuperAdmin assigns permissions.
    public string? RoleName { get; set; }

    // Frontend-defined module
    // Example: Dashboard, Doctors, Reports, Billing
    public string? Module { get; set; }

    public bool CanView { get; set; }

    public bool CanCreate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}