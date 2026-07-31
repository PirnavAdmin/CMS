using System.Globalization;
using System.Security.Claims;

using AuthDemo.Data;

using AuthDemo.DTOs;

using AuthDemo.Helpers;

using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]

[Route("api/patient-portal")]

public class PatientPortalController : ControllerBase

{

    private readonly AppDbContext _context;

    private readonly JwtHelper _jwtHelper;

    public PatientPortalController(AppDbContext context, JwtHelper jwtHelper)

    {

        _context = context;

        _jwtHelper = jwtHelper;

    }

    // =====================================================

    // Helpers

    // =====================================================

    private string GetEmail()

    {

        return User.Claims.FirstOrDefault(x => x.Type == "email")?.Value

               ?? User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value

               ?? string.Empty;

    }

    private int GetHospitalId()

    {

        var value = User.Claims.FirstOrDefault(x => x.Type == "HospitalId")?.Value;

        return int.TryParse(value, out var hospitalId) ? hospitalId : 0;

    }

    private static string BuildName(string? firstName, string? lastName)

    {

        return string.Join(" ", new[] { firstName, lastName }

            .Where(x => !string.IsNullOrWhiteSpace(x)))

            .Trim();

    }

    private static int CalculateAge(DateTime? dob)

    {

        if (dob == null)

        {

            return 0;

        }

        var age = DateTime.Today.Year - dob.Value.Year;

        if (dob.Value.Date > DateTime.Today.AddYears(-age))

        {

            age--;

        }

        return age < 0 ? 0 : age;

    }

    private async Task<Patient?> GetLoggedInPatient()

    {

        var email = GetEmail();

        return await _context.Patients

            .FirstOrDefaultAsync(x => x.Email == email);

    }

    private static bool TryParse12HourTime(string? input, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var acceptedFormats = new[]
        {
            "hh:mm tt",
            "h:mm tt"
        };

        if (!DateTime.TryParseExact(
                input.Trim(),
                acceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDateTime))
        {
            return false;
        }

        time = parsedDateTime.TimeOfDay;
        return true;
    }

    private async Task CreateNotification(string title, string message)

    {

        _context.Notifications.Add(new Notification

        {

            Title = title,

            Message = message,

            IsSent = true,

            CreatedAt = DateTime.UtcNow

        });

        await _context.SaveChangesAsync();

    }

    private static int ParseTokenSequence(string? tokenNumber)
    {
        if (string.IsNullOrWhiteSpace(tokenNumber))
        {
            return int.MaxValue;
        }

        var digits = new string(tokenNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var sequence) ? sequence : int.MaxValue;
    }

    private static string NormalizeQueueStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "inprogress" or "in consultation" or "inconsultation" => "InConsultation",
            "completed" or "prescriptionadded" => "Completed",
            "called" => "Called",
            "cancelled" => "Cancelled",
            "noshow" or "no show" => "NoShow",
            _ => "Waiting"
        };
    }

    // =====================================================

    // 1. Login / Registration

    // =====================================================

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(PatientPortalRegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.MobileNumber) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new
            {
                message = "First name, mobile number, email and password are required."
            });
        }

        if (dto.Password != dto.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Password and confirm password do not match."
            });
        }

        if (!dto.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Only Gmail addresses are allowed."
            });
        }

        if (dto.MobileNumber.Length != 10 ||
            !dto.MobileNumber.All(char.IsDigit) ||
            !"6789".Contains(dto.MobileNumber[0]))
        {
            return BadRequest(new
            {
                message = "Enter a valid mobile number."
            });
        }

        // Validate Hospital
        var hospital = await _context.Hospitals
            .FirstOrDefaultAsync(x => x.Id == dto.HospitalId && x.IsActive);

        if (hospital == null)
        {
            return BadRequest(new
            {
                message = "Selected hospital not found."
            });
        }

        var existingUser = await _context.Users
            .AnyAsync(x => x.Email == dto.Email ||
                           x.MobileNumber == dto.MobileNumber);

        if (existingUser)
        {
            return BadRequest(new
            {
                message = "Email or mobile number already exists."
            });
        }

        var name = BuildName(dto.FirstName, dto.LastName);

        var patientCode = $"P-{Random.Shared.Next(10000, 99999)}";

        var patient = new Patient
        {
            PatientCode = patientCode,
            Name = name,
            Phone = dto.MobileNumber,
            Age = CalculateAge(dto.DateOfBirth),
            Gender = dto.Gender ?? string.Empty,
            Email = dto.Email,
            Address = dto.Address,
            DateOfBirth = dto.DateOfBirth,

            // Store Selected Hospital
            HospitalId = dto.HospitalId
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var user = new User
        {
            Name = name,
            MobileNumber = dto.MobileNumber,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Patient",

            // Store Selected Hospital
            HospitalId = dto.HospitalId,

            MustChangePassword = false,
            IsActive = true
        };

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Patient registered successfully.",
            patientId = patient.Id,
            patientCode = patient.PatientCode,
            hospitalId = hospital.Id,
            hospitalName = hospital.Name
        });
    }

    // Patient login is handled only by POST /api/Auth/login.

    // =====================================================

    // 2. Patient Dashboard

    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var hospital = await _context.Hospitals
            .FirstOrDefaultAsync(x => x.Id == patient.HospitalId);

        var now = DateTime.Today;

        var upcomingAppointment = await _context.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Hospital)
            .Where(x => x.PatientId == patient.Id &&
                        x.Date.Date >= now &&
                        x.Status != "Cancelled")
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                doctorName = x.Doctor.Name,
                hospitalName = x.Hospital.Name,
                date = x.Date,
                time = x.Date.Date
                 .Add(x.StartTime)
                 .ToString("hh:mm tt"),
                status = x.Status
            })
            .FirstOrDefaultAsync();

        var previousAppointments = await _context.Appointments
            .CountAsync(x => x.PatientId == patient.Id &&
                             x.Date.Date < now);

        var prescriptions = await _context.Prescriptions
            .CountAsync(x => x.PatientId == patient.Id);

        var medicalRecords = await _context.MedicalHistories
            .CountAsync(x => x.PatientId == patient.Id);

        var billsPending = await _context.Billings
            .CountAsync(x => x.PatientId == patient.Id &&
                             x.Status == "Pending");

        return Ok(new
        {
            patient = new
            {
                patient.Id,
                patient.PatientCode,
                patient.Name
            },

            hospital = new
            {
                hospitalId = hospital?.Id,
                hospitalName = hospital?.Name
            },

            cards = new
            {
                upcomingAppointment,
                previousAppointments,
                prescriptions,
                medicalRecords,
                billsPending
            },

            quickActions = new[]
            {
            "Book Appointment",
            "View Reports",
            "View Prescriptions",
            "Payments"
        },

            notifications = await _context.Notifications
                .Where(x => x.IsSent)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .ToListAsync()
        });
    }

    // =====================================================
    // 3. My Profile
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var patient = await _context.Patients
            .Include(x => x.Hospital)
            .FirstOrDefaultAsync(x => x.Email == GetEmail());

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var history = await _context.MedicalHistories
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(new
        {
            patient.Id,
            patient.PatientCode,
            patient.Name,
            patient.Gender,
            patient.DateOfBirth,
            patient.Age,
            patient.BloodGroup,

            mobile = patient.Phone,
            patient.Email,
            patient.Address,

            hospital = new
            {
                hospitalId = patient.HospitalId,
                hospitalName = patient.Hospital?.Name
            },

            emergencyContact = new
            {
                name = patient.EmergencyContactName,
                phone = patient.EmergencyContactPhone
            },

            medicalInformation = new
            {
                allergies = history?.Allergies,
                chronicDiseases = history?.ChronicDiseases,
                currentMedications = history?.CurrentMedications
            }
        });
    }

    [Authorize(Roles = "Patient")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(PatientProfileUpdateDto dto)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var name = BuildName(dto.FirstName, dto.LastName);

        if (!string.IsNullOrWhiteSpace(name))
            patient.Name = name;

        patient.Gender = dto.Gender ?? patient.Gender;
        patient.DateOfBirth = dto.DateOfBirth ?? patient.DateOfBirth;
        patient.Age = CalculateAge(patient.DateOfBirth);

        patient.BloodGroup = dto.BloodGroup ?? patient.BloodGroup;
        patient.Phone = dto.MobileNumber ?? patient.Phone;
        patient.Email = dto.Email ?? patient.Email;
        patient.Address = dto.Address ?? patient.Address;

        patient.EmergencyContactName =
            dto.EmergencyContactName ?? patient.EmergencyContactName;

        patient.EmergencyContactPhone =
            dto.EmergencyContactPhone ?? patient.EmergencyContactPhone;

        var history = await _context.MedicalHistories
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.PatientId == patient.Id);

        if (history == null)
        {
            if (patient.HospitalId == null)
            {
                return BadRequest(new
                {
                    message = "Hospital is not assigned to this patient."
                });
            }

            history = new MedicalHistory
            {
                PatientId = patient.Id,
                HospitalId = patient.HospitalId.Value
            };

            _context.MedicalHistories.Add(history);
        }

        history.Allergies =
            dto.Allergies ?? history.Allergies;

        history.ChronicDiseases =
            dto.ChronicDiseases ?? history.ChronicDiseases;

        history.CurrentMedications =
            dto.CurrentMedications ?? history.CurrentMedications;

        var user = await _context.Users.FirstOrDefaultAsync(x =>
            x.Role == "Patient" &&
            x.Email == GetEmail());

        if (user != null)
        {
            user.Name = patient.Name;
            user.MobileNumber = patient.Phone;
            user.Email = patient.Email;
            user.HospitalId = patient.HospitalId;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Profile updated successfully."
        });
    }

    // =====================================================
    // 4. Appointment Booking Supporting APIs
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        if (patient.HospitalId == null)
        {
            return BadRequest(new { message = "Hospital is not assigned to the patient." });
        }

        var branches = await _context.Branches
            .Where(x => x.HospitalId == patient.HospitalId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                branchId = x.Id,
                branchName = x.Name,
                x.Address,
                x.Phone
            })
            .ToListAsync();

        return Ok(branches);
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("branches/{branchId}/departments")]
    public async Task<IActionResult> GetDepartments(int branchId)
    {
        var departments = await _context.Doctors
            .Where(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                x.Specialization != null)
            .Select(x => x.Specialization!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return Ok(departments);
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctors(
        [FromQuery] int branchId,
        [FromQuery] string? department)
    {
        var doctors = await _context.Doctors
            .Where(x =>
                x.BranchId == branchId &&
                x.IsActive &&
                (string.IsNullOrWhiteSpace(department) ||
                 x.Specialization == department))
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                doctorId = x.Id,
                doctorName = x.Name,
                department = x.Specialization,
                x.Qualification,
                x.Experience,
                consultationFee = x.Fees,

                availableToday = !_context.Appointments.Any(a =>
                    a.DoctorId == x.Id &&
                    a.Date.Date == DateTime.Today &&
                    a.Status != "Cancelled")
            })
            .ToListAsync();

        return Ok(doctors);
    }
    [Authorize(Roles = "Patient")]
    [HttpGet("doctors/{doctorId}/slots")]
    public async Task<IActionResult> GetSlots(
        int doctorId,
        [FromQuery] DateTime date)
    {
        // ==========================================
        // GET DOCTOR
        // ==========================================

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == doctorId &&
                x.IsActive);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found."
            });
        }

        // ==========================================
        // GET SCHEDULE
        // ==========================================

        var schedule = await _context.Schedules
            .FirstOrDefaultAsync(x =>
                x.DoctorId == doctorId &&
                x.HospitalId == doctor.HospitalId &&
                date.Date >= x.StartDate.Date &&
                date.Date <= x.EndDate.Date);

        if (schedule == null)
        {
            return Ok(new List<object>());
        }

        // ==========================================
        // CHECK DAY
        // ==========================================

        var days = schedule.Days.Split(',');

        if (!days.Contains(date.DayOfWeek.ToString()))
        {
            return Ok(new List<object>());
        }

        // ==========================================
        // SLOT SETTINGS
        // ==========================================

        var setting = await _context.ScheduleSettings
            .Where(x => x.HospitalId == doctor.HospitalId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        int slotDuration = setting?.SlotDuration ?? 30;

        // ==========================================
        // BOOKED SLOTS
        // ==========================================

        var bookedSlots = await _context.Appointments
            .Where(x =>
                x.DoctorId == doctorId &&
                x.Date.Date == date.Date &&
                x.Status != "Cancelled")
            .Select(x => x.StartTime)
            .ToListAsync();

        // ==========================================
        // GENERATE SLOTS
        // ==========================================

        var result = new List<object>();

        var current = schedule.WorkStart;

        while (current < schedule.WorkEnd)
        {
            // Break Time
            if (current >= schedule.BreakStart &&
                current < schedule.BreakEnd)
            {
                current = schedule.BreakEnd;
                continue;
            }

            var end = current.Add(
                TimeSpan.FromMinutes(slotDuration));

            // Hide expired slots for today
            if (date.Date == DateTime.Today)
            {
                var slotDateTime = date.Date.Add(current);

                if (slotDateTime <= DateTime.Now)
                {
                    current = end;
                    continue;
                }
            }

            bool booked = bookedSlots.Contains(current);

            result.Add(new
            {
                start = DateTime.Today
                 .Add(current)
                 .ToString("hh:mm tt"),

                end = DateTime.Today
                 .Add(end)
                 .ToString("hh:mm tt"),
            });

            current = end;
        }

        return Ok(result);
    }

    [Authorize(Roles = "Patient")]
    [HttpPost("appointments")]
    public async Task<IActionResult> BookAppointment(
        [FromBody] PatientPortalBookAppointmentDto dto)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        if (dto.BranchId <= 0 || dto.DoctorId <= 0)
        {
            return BadRequest(new { message = "Valid branch and doctor are required." });
        }

        if (dto.Date.Date < DateTime.Today)
        {
            return BadRequest(new { message = "Past appointment dates are not allowed." });
        }

        if (!TryParse12HourTime(dto.StartTime, out var appointmentStartTime))
        {
            return BadRequest(new
            {
                message = "Invalid StartTime. Use 12-hour format like 09:00 AM or 08:00 PM."
            });
        }

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == dto.DoctorId &&
                x.BranchId == dto.BranchId &&
                x.HospitalId == patient.HospitalId &&
                x.IsActive);

        if (doctor == null)
        {
            return BadRequest(new { message = "Doctor not found in selected branch." });
        }

        var schedule = await _context.Schedules
            .Where(x =>
                x.DoctorId == dto.DoctorId &&
                x.HospitalId == patient.HospitalId &&
                dto.Date.Date >= x.StartDate.Date &&
                dto.Date.Date <= x.EndDate.Date)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (schedule == null)
        {
            return BadRequest(new { message = "Doctor schedule not found for selected date." });
        }

        var workingDays = schedule.Days
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!workingDays.Contains(dto.Date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Doctor is not available on selected day." });
        }

        if (appointmentStartTime < schedule.WorkStart || appointmentStartTime >= schedule.WorkEnd)
        {
            return BadRequest(new { message = "Selected time is outside doctor working hours." });
        }

        if (appointmentStartTime >= schedule.BreakStart && appointmentStartTime < schedule.BreakEnd)
        {
            return BadRequest(new { message = "Selected time is during doctor break." });
        }

        if (dto.Date.Date.Add(appointmentStartTime) <= DateTime.Now)
        {
            return BadRequest(new { message = "Selected appointment time has already passed." });
        }

        var slotBooked = await _context.Appointments.AnyAsync(x =>
            x.DoctorId == dto.DoctorId &&
            x.Date.Date == dto.Date.Date &&
            x.StartTime == appointmentStartTime &&
            x.Status != "Cancelled");

        if (slotBooked)
        {
            return BadRequest(new { message = "Selected slot is already booked." });
        }

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = dto.DoctorId,
            HospitalId = patient.HospitalId!.Value,
            BranchId = dto.BranchId,
            Date = dto.Date.Date,
            StartTime = appointmentStartTime,
            ChiefComplaints = dto.ReasonForVisit,
            TokenNumber = $"APT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            BookingType = "Online",
            ConsultationFee = doctor.Fees,
            PaymentStatus = "Pending",
            Status = "Pending"
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        await CreateNotification(
            "Appointment Booked",
            $"Appointment {appointment.TokenNumber} has been booked successfully.");

        return Ok(new
        {
            message = "Please complete the consultation fee payment to confirm your appointment.",
            appointmentId = appointment.Id,
            patientId = patient.Id,
            hospitalId = patient.HospitalId,
            branchId = dto.BranchId,
            doctorId = dto.DoctorId,
            doctorName = doctor.Name,
            consultationFee = doctor.Fees,
            appointmentDate = appointment.Date.ToString("yyyy-MM-dd"),
            appointmentTime = appointment.Date.Date
                .Add(appointment.StartTime)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            reasonForVisit = dto.ReasonForVisit,
            status = appointment.Status
        });
    }

    // =====================================================
    // 5. Appointment Details
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var appointments = await _context.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Hospital)
            .Include(x => x.Branch)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                appointmentNumber = x.TokenNumber,

                hospital = x.Hospital.Name,
                branch = x.Branch != null ? x.Branch.Name : null,

                doctor = x.Doctor.Name,

                date = x.Date,
                time = x.Date.Date
                 .Add(x.StartTime)
                 .ToString("hh:mm tt"),
                bookingType = x.BookingType,


                reasonForVisit = x.ChiefComplaints,

                status = x.Status
            })
            .ToListAsync();

        return Ok(appointments);
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("appointments/{id}")]
    public async Task<IActionResult> GetAppointmentDetails(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var appointment = await _context.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Hospital)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        return Ok(new
        {
            appointmentId = appointment.Id,

            appointmentNumber = appointment.TokenNumber,

            hospital = appointment.Hospital.Name,

            branch = appointment.Branch?.Name,

            doctor = appointment.Doctor.Name,

            date = appointment.Date,

            time = appointment.Date.Date
                .Add(appointment.StartTime)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),

            reasonForVisit = appointment.ChiefComplaints,

            status = appointment.Status,

            actions = new[]
            {
            "Cancel Appointment",
            "Reschedule Appointment",
            "Download Confirmation"
        }
        });
    }

    // =====================================================
    // 6. Live token and queue tracking
    // The frontend can call this endpoint every 10-15 seconds.
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("appointments/{id}/queue-status")]
    public async Task<IActionResult> GetQueueStatus(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(x => x.Doctor)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        var tokenAvailable =
            appointment.PaymentStatus == "Paid" &&
            !string.IsNullOrWhiteSpace(appointment.TokenNumber) &&
            appointment.TokenNumber.StartsWith("TKN", StringComparison.OrdinalIgnoreCase);

        if (!tokenAvailable)
        {
            return Ok(new
            {
                appointmentId = appointment.Id,
                tokenAvailable = false,
                tokenNumber = (string?)null,
                currentToken = (string?)null,
                patientsAhead = 0,
                queuePosition = 0,
                estimatedWaitingMinutes = 0,
                status = appointment.Status,
                paymentStatus = appointment.PaymentStatus,
                message = appointment.PaymentStatus == "Paid"
                    ? "Your queue token is being generated."
                    : "Complete payment to receive your queue token.",
                lastUpdatedAt = DateTime.UtcNow
            });
        }

        var queue = await _context.Appointments
            .AsNoTracking()
            .Where(x =>
                x.DoctorId == appointment.DoctorId &&
                x.BranchId == appointment.BranchId &&
                x.Date.Date == appointment.Date.Date &&
                x.PaymentStatus == "Paid" &&
                x.Status != "Cancelled" &&
                x.Status != "NoShow" &&
                x.TokenNumber.StartsWith("TKN"))
            .Select(x => new
            {
                x.Id,
                x.TokenNumber,
                x.Status,
                x.StartTime,
                x.CreatedAt
            })
            .ToListAsync();

        var orderedQueue = queue
            .OrderBy(x => ParseTokenSequence(x.TokenNumber))
            .ThenBy(x => x.StartTime)
            .ThenBy(x => x.CreatedAt)
            .ToList();

        var currentServing = orderedQueue
            .FirstOrDefault(x => NormalizeQueueStatus(x.Status) == "InConsultation")
            ?? orderedQueue.FirstOrDefault(x => NormalizeQueueStatus(x.Status) == "Called");

        var patientIndex = orderedQueue.FindIndex(x => x.Id == appointment.Id);
        var patientsAhead = orderedQueue.Count(x =>
            x.Id != appointment.Id &&
            ParseTokenSequence(x.TokenNumber) < ParseTokenSequence(appointment.TokenNumber) &&
            NormalizeQueueStatus(x.Status) is "Waiting" or "Called" or "InConsultation");

        var setting = await _context.ScheduleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.HospitalId == appointment.HospitalId);

        var slotDuration = setting?.SlotDuration > 0
            ? setting.SlotDuration
            : 30;

        var estimatedWaitingMinutes = patientsAhead * slotDuration;
        var normalizedStatus = NormalizeQueueStatus(appointment.Status);

        var liveQueue = orderedQueue.Select(x => new
        {
            tokenNumber = x.TokenNumber,
            status = NormalizeQueueStatus(x.Status),
            isCurrentPatient = x.Id == appointment.Id
        });

        return Ok(new
        {
            appointmentId = appointment.Id,
            tokenAvailable = true,
            tokenNumber = appointment.TokenNumber,
            currentToken = currentServing?.TokenNumber,
            queuePosition = patientIndex >= 0 ? patientIndex + 1 : 0,
            patientsAhead,
            estimatedWaitingMinutes,
            slotDurationMinutes = slotDuration,
            status = normalizedStatus,
            appointmentStatus = appointment.Status,
            paymentStatus = appointment.PaymentStatus,
            doctorName = appointment.Doctor?.Name,
            branchName = appointment.Branch?.Name,
            appointmentDate = appointment.Date.ToString("yyyy-MM-dd"),
            appointmentTime = appointment.Date.Date
                .Add(appointment.StartTime)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            isAboutToBeCalled = patientsAhead <= 2 && normalizedStatus == "Waiting",
            message = normalizedStatus switch
            {
                "Called" => "Your token has been called.",
                "InConsultation" => "Your consultation is in progress.",
                "Completed" => "Your consultation is completed.",
                _ when patientsAhead == 0 => "You are next in the queue.",
                _ when patientsAhead <= 2 => "Your token is about to be called.",
                _ => $"There are {patientsAhead} patient(s) ahead of you."
            },
            queue = liveQueue,
            lastUpdatedAt = DateTime.UtcNow
        });
    }

    // Alias for clients that request only token tracking.
    [Authorize(Roles = "Patient")]
    [HttpGet("appointments/{id}/token")]
    public Task<IActionResult> GetAppointmentToken(int id)
    {
        return GetQueueStatus(id);
    }

    [Authorize(Roles = "Patient")]
    [HttpPatch("appointments/{id}/cancel")]
    public async Task<IActionResult> CancelAppointment(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (appointment.Status == "Completed")
        {
            return BadRequest(new
            {
                message = "Completed appointments cannot be cancelled."
            });
        }

        if (appointment.Status == "Cancelled")
        {
            return BadRequest(new
            {
                message = "Appointment is already cancelled."
            });
        }

        appointment.Status = "Cancelled";

        await _context.SaveChangesAsync();

        await CreateNotification(
            "Appointment Cancelled",
            $"Appointment {appointment.TokenNumber} has been cancelled.");

        return Ok(new
        {
            message = "Appointment cancelled successfully."
        });
    }

    [Authorize(Roles = "Patient")]
    [HttpPut("appointments/{id}/reschedule")]
    public async Task<IActionResult> RescheduleAppointment(
        int id,
        [FromBody] PatientPortalRescheduleDto dto)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (appointment.Status == "Cancelled")
        {
            return BadRequest(new { message = "Cancelled appointment cannot be rescheduled." });
        }

        if (!TryParse12HourTime(dto.StartTime, out var newStartTime))
        {
            return BadRequest(new
            {
                message = "Invalid StartTime. Use 12-hour format like 09:00 AM or 08:00 PM."
            });
        }

        if (dto.Date.Date.Add(newStartTime) <= DateTime.Now)
        {
            return BadRequest(new { message = "Selected appointment time has already passed." });
        }

        var slotBooked = await _context.Appointments.AnyAsync(x =>
            x.Id != id &&
            x.DoctorId == appointment.DoctorId &&
            x.Date.Date == dto.Date.Date &&
            x.StartTime == newStartTime &&
            x.Status != "Cancelled");

        if (slotBooked)
        {
            return BadRequest(new { message = "Selected slot is already booked." });
        }

        appointment.Date = dto.Date.Date;
        appointment.StartTime = newStartTime;
        appointment.Status = "Pending";

        await _context.SaveChangesAsync();

        await CreateNotification(
            "Appointment Rescheduled",
            $"Appointment {appointment.TokenNumber} has been rescheduled.");

        return Ok(new
        {
            message = "Appointment rescheduled successfully.",
            appointmentId = appointment.Id,
            appointmentNumber = appointment.TokenNumber,
            date = appointment.Date.ToString("yyyy-MM-dd"),
            time = appointment.Date.Date
                .Add(appointment.StartTime)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            status = appointment.Status
        });
    }

    // =====================================================
    // 6. Medical History
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("medical-history")]
    public async Task<IActionResult> GetMedicalHistory()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var data = await _context.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Hospital)
            .Include(x => x.Branch)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.Date)
            .Select(x => new
            {
                visitId = x.Id,

                hospital = x.Hospital.Name,
                branch = x.Branch != null ? x.Branch.Name : null,

                doctorName = x.Doctor.Name,

                date = x.Date,

                conditions = x.ChiefComplaints,

                reports = _context.AppointmentDocuments
                    .Where(d => d.AppointmentId == x.Id)
                    .Select(d => new
                    {
                        d.FileName,
                        d.FilePath
                    })
                    .ToList(),

                prescriptions = _context.Prescriptions
                    .Where(p => p.AppointmentId == x.Id)
                    .Select(p => new
                    {
                        p.Id,
                        p.Diagnosis,
                        p.Status
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(data);
    }

    // =====================================================
    // 7. Prescription
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("prescriptions")]
    public async Task<IActionResult> GetPrescriptions()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var data = await _context.Prescriptions
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Doctor)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Hospital)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Branch)
            .Include(x => x.Medicines)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                prescriptionId = x.Id,

                hospital = x.Appointment.Hospital.Name,
                branch = x.Appointment.Branch != null
                            ? x.Appointment.Branch.Name
                            : null,

                doctorName = x.Appointment.Doctor.Name,

                specialization = x.Appointment.Doctor.Specialization,

                x.Diagnosis,

                x.Instructions,

                x.FollowUpDate,

                x.Status,

                medicines = x.Medicines.Select(m => new
                {
                    m.MedicineName,
                    m.Dosage,
                    m.Frequency,
                    m.Duration,
                    instructions = m.Notes
                })
            })
            .ToListAsync();

        return Ok(data);
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("prescriptions/{id}")]
    public async Task<IActionResult> GetPrescriptionDetails(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        var prescription = await _context.Prescriptions
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Doctor)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Hospital)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Branch)
            .Include(x => x.Medicines)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id);

        if (prescription == null)
        {
            return NotFound(new { message = "Prescription not found." });
        }

        return Ok(new
        {
            prescriptionId = prescription.Id,

            hospital = prescription.Appointment.Hospital.Name,

            branch = prescription.Appointment.Branch != null
                        ? prescription.Appointment.Branch.Name
                        : null,

            doctorDetails = new
            {
                doctorId = prescription.Appointment.DoctorId,
                doctorName = prescription.Appointment.Doctor.Name,
                specialization = prescription.Appointment.Doctor.Specialization
            },

            prescription.Diagnosis,

            medicineList = prescription.Medicines.Select(m => new
            {
                m.MedicineName,
                m.Dosage,
                m.Frequency,
                m.Duration,
                instructions = m.Notes
            }),

            prescription.Instructions,

            prescription.FollowUpDate,

            prescription.Status,

            buttons = new[]
            {
            "Download PDF",
            "Print",
            "Share"
        }
        });
    }

    // =====================================================

    // 8. Billing & Payment

    // =====================================================

    [Authorize(Roles = "Patient")]

    [HttpGet("bills")]

    public async Task<IActionResult> GetBills()

    {

        var patient = await GetLoggedInPatient();

        if (patient == null)

        {

            return NotFound(new { message = "Patient profile not found" });

        }

        var bills = await _context.Billings

            .Include(x => x.Appointment)

            .Where(x => x.PatientId == patient.Id)

            .OrderByDescending(x => x.CreatedAt)

            .ToListAsync();

        var data = bills.Select(x => new

        {

            billId = x.Id,

            billNumber = "BILL-" + x.Id,

            appointmentNumber = x.Appointment == null ? "" : x.Appointment.TokenNumber,

            consultationFee = x.ConsultationCharge,

            labCharges = x.LabCharge,

            medicineCharges = x.MedicineCharge,

            gst = Math.Round(x.TotalAmount * 0.18M, 2),

            total = x.TotalAmount,

            paymentMode = x.PaymentMode,

            status = x.Status,

            buttons = new[] { "Pay Now", "Download Invoice" }

        });

        return Ok(data);

    }

    [Authorize(Roles = "Patient")]

    [HttpPost("bills/{id}/pay")]

    public async Task<IActionResult> PayBill(int id, PatientPortalPaymentDto dto)

    {

        var patient = await GetLoggedInPatient();

        if (patient == null)

        {

            return NotFound(new { message = "Patient profile not found" });

        }

        var bill = await _context.Billings

            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id);

        if (bill == null)

        {

            return NotFound(new { message = "Bill not found" });

        }

        bill.PaymentMode = string.IsNullOrWhiteSpace(dto.PaymentMode) ? "Online" : dto.PaymentMode;

        bill.Status = "Paid";

        await _context.SaveChangesAsync();

        await CreateNotification("Payment Completed", $"Payment completed for BILL-{bill.Id}.");

        return Ok(new

        {

            message = "Payment completed successfully",

            billId = bill.Id,

            status = bill.Status,

            paymentMode = bill.PaymentMode

        });

    }

    // =====================================================
    // 9. Notifications
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var notifications = await _context.Notifications
            .Where(x =>
                x.IsSent &&
                (x.PatientId == null || x.PatientId == patient.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Message,
                x.IsRead,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [Authorize(Roles = "Patient")]
    [HttpPatch("notifications/{id}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                (x.PatientId == null || x.PatientId == patient.Id));

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        notification.IsRead = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification marked as read."
        });
    }

    [Authorize(Roles = "Patient")]
    [HttpDelete("notifications/{id}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                (x.PatientId == null || x.PatientId == patient.Id));

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        _context.Notifications.Remove(notification);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Notification deleted successfully."
        });

    }
    [Authorize(Roles = "Patient")]
    [HttpGet("doctor/{doctorId}/consultation-fee")]
    public async Task<IActionResult> GetConsultationFee(int doctorId)
    {
        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x => x.Id == doctorId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found."
            });
        }

        return Ok(new
        {
            doctorId = doctor.Id,
            doctorName = doctor.Name,
            consultationFee = doctor.Fees
        });
    }
}
