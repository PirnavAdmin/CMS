
namespace AuthDemo.DTOs;

// =====================================================
// PATIENT REGISTRATION
// =====================================================

public class PatientPortalRegisterDto
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? MobileNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    // Patient selects hospital during registration
    public int HospitalId { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }
}

// =====================================================
// PATIENT LOGIN
// =====================================================

public class PatientPortalLoginDto
{
    public string? EmailOrMobile { get; set; }

    public string? Password { get; set; }
}

// =====================================================
// PATIENT PROFILE UPDATE
// =====================================================

public class PatientProfileUpdateDto
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? BloodGroup { get; set; }

    public string? MobileNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContactName { get; set; }

    public string? EmergencyContactRelationship { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? Allergies { get; set; }

    public string? ChronicDiseases { get; set; }

    public string? CurrentMedications { get; set; }
}

// =====================================================
// BOOK APPOINTMENT
// =====================================================

public class PatientPortalBookAppointmentDto
{
    // HospitalId is taken from logged-in patient.
    // No HospitalId required in request body.

    public int BranchId { get; set; }

    public int DoctorId { get; set; }

    public DateTime Date { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string? ReasonForVisit { get; set; }
}

// =====================================================
// RESCHEDULE APPOINTMENT
// =====================================================

public class PatientPortalRescheduleDto
{
    public DateTime Date { get; set; }

    public string StartTime { get; set; } = string.Empty;
}

// =====================================================
// PAYMENT DTO
// Used for OP, Lab and Pharmacy billing payments
// =====================================================

public class PatientPortalPaymentDto
{
    // Examples: Cash, UPI, Card
    public string? PaymentMode { get; set; }

    // UPI transaction ID, card reference or receipt number
    public string? ReferenceNumber { get; set; }

    // Optional partial amount; omitted means collect the full outstanding balance.
    public decimal? Amount { get; set; }
}

// =====================================================
// SELECT BRANCH
// =====================================================

public class SelectBranchDto
{
    public int BranchId { get; set; }
}
