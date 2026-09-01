namespace AuthDemo.Models;

public class AuditLog
{
    public int Id { get; set; }

    // =====================================================
    // USER DETAILS
    // =====================================================

    public int UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    // =====================================================
    // CLINIC DETAILS
    // =====================================================

    public int? ClinicId { get; set; }

    public Hospital? Clinic { get; set; }

    public int? BranchId { get; set; }

    public Branch? Branch { get; set; }

    // =====================================================
    // ACTIVITY DETAILS
    // =====================================================

    public string Action { get; set; } = string.Empty;
    // Examples:
    // Login
    // Logout
    // Create Patient
    // Update Patient
    // Book Appointment
    // Cancel Appointment
    // Billing
    // Payment
    // Add Prescription

    public string? SystemAction { get; set; }

    public bool IsLoginActivity { get; set; }

    // =====================================================
    // DEVICE INFORMATION
    // =====================================================

    public string? IpAddress { get; set; }

    public string? Browser { get; set; }

    public string? Device { get; set; }

    // =====================================================
    // LOGIN DETAILS
    // =====================================================

    public DateTime LoginTime { get; set; } = DateTime.UtcNow;

    public DateTime? LogoutTime { get; set; }

    public bool IsOnline { get; set; }

    // =====================================================
    // GENERAL
    // =====================================================

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}