namespace AuthDemo.Models;

public class Notification
{
    public int Id { get; set; }

    // =====================================================
    // NOTIFICATION DETAILS
    // =====================================================

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // =====================================================
    // PATIENT (NULL = GLOBAL NOTIFICATION)
    // =====================================================

    public int? PatientId { get; set; }

    public Patient? Patient { get; set; }

    // =====================================================
    // STATUS
    // =====================================================

    public bool IsRead { get; set; } = false;

    public bool IsSent { get; set; } = true;

    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}