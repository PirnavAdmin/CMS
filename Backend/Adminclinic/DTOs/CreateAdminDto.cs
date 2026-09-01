public class CreateAdminDto
{
    public string? Name { get; set; }

    public string? FullName { get; set; }

    public string? MobileNumber { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; } = "Admin";

    public int HospitalId { get; set; }

    public bool SendWelcomeEmail { get; set; } = true;
}