
using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Models;

public sealed class PatientAppointmentPayment
{
    public long Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int PatientId { get; set; }
    public int HospitalId { get; set; }

    public decimal Amount { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "INR";

    [Required, MaxLength(20)]
    public string Mode { get; set; } = "Test";

    [Required, MaxLength(80)]
    public string LocalOrderId { get; set; }
        = Guid.NewGuid().ToString("N");

    [MaxLength(150)]
    public string? RazorpayOrderId { get; set; }

    [MaxLength(150)]
    public string? RazorpayPaymentId { get; set; }

    public int? RazorpayCredentialId { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Creating";

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CapturedAt { get; set; }
}
