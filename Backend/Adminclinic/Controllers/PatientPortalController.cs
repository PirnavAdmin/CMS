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

    private async Task CreateNotification(int patientId, string title, string message)

    {

        _context.Notifications.Add(new Notification

        {

            PatientId = patientId,

            Title = title,

            Message = message,

            IsRead = false,

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
                .Where(x =>
                    x.IsSent &&
                    x.PatientId == patient.Id)
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
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        if (patient.HospitalId == null)
        {
            return BadRequest(new { message = "Hospital is not assigned to the patient." });
        }

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == branchId &&
            x.HospitalId == patient.HospitalId.Value &&
            x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new { message = "Invalid branch or branch is inactive." });
        }

        var departments = await _context.Doctors
            .Where(x =>
                x.HospitalId == patient.HospitalId.Value &&
                x.IsActive &&
                x.Specialization != null &&
                (
                    x.BranchId == branchId ||
                    _context.DoctorBranches.Any(db =>
                        db.DoctorId == x.Id &&
                        db.BranchId == branchId &&
                        db.HospitalId == patient.HospitalId.Value &&
                        db.IsActive)
                ))
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
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new { message = "Patient profile not found." });
        }

        if (patient.HospitalId == null)
        {
            return BadRequest(new { message = "Hospital is not assigned to the patient." });
        }

        if (branchId <= 0)
        {
            return BadRequest(new { message = "A valid branchId is required." });
        }

        var hospitalId = patient.HospitalId.Value;

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == branchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new { message = "Invalid branch or branch is inactive." });
        }

        var normalizedDepartment = string.IsNullOrWhiteSpace(department)
            ? null
            : department.Trim().ToLower();

        var today = DateTime.Today;
        var todayName = today.DayOfWeek.ToString();

        var doctorRows = await _context.Doctors
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.IsActive &&
                (
                    x.BranchId == branchId ||
                    _context.DoctorBranches.Any(db =>
                        db.DoctorId == x.Id &&
                        db.BranchId == branchId &&
                        db.HospitalId == hospitalId &&
                        db.IsActive)
                ) &&
                (
                    normalizedDepartment == null ||
                    (x.Specialization != null &&
                     x.Specialization.Trim().ToLower() == normalizedDepartment)
                ))
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Specialization,
                x.Qualification,
                x.Experience,
                x.Fees,
                x.Image
            })
            .ToListAsync();

        var doctorAvailability = new Dictionary<int, bool>();
        foreach (var row in doctorRows)
        {
            var windows = await ScheduleAvailabilityHelper.GetEffectiveWindowsAsync(
                _context, hospitalId, row.Id, today);
            doctorAvailability[row.Id] = windows.Any(w => w.BranchId == branchId && w.End > DateTime.Now.TimeOfDay);
        }

        var doctors = doctorRows.Select(x => new
        {
            doctorId = x.Id,
            doctorName = x.Name,
            department = x.Specialization,
            specialization = x.Specialization,
            qualification = x.Qualification,
            experience = x.Experience,
            consultationFee = x.Fees,
            fees = x.Fees,
            image = x.Image,
            branchId,
            availableToday = doctorAvailability.TryGetValue(x.Id, out var available) && available
        });

        return Ok(doctors);
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("doctors/{doctorId}/slots")]
    public async Task<IActionResult> GetSlots(
        int doctorId,
        [FromQuery] int branchId,
        [FromQuery] DateTime date)
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

        if (doctorId <= 0 || branchId <= 0)
        {
            return BadRequest(new { message = "Valid doctorId and branchId are required." });
        }

        if (date.Date < DateTime.Today)
        {
            return BadRequest(new { message = "Past dates are not allowed." });
        }

        var hospitalId = patient.HospitalId.Value;

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == branchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new { message = "Invalid branch or branch is inactive." });
        }

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == doctorId &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (doctor == null)
        {
            return NotFound(new { message = "Doctor not found." });
        }

        var doctorAssignedToBranch =
            doctor.BranchId == branchId ||
            await _context.DoctorBranches.AnyAsync(db =>
                db.DoctorId == doctorId &&
                db.BranchId == branchId &&
                db.HospitalId == hospitalId &&
                db.IsActive);

        if (!doctorAssignedToBranch)
        {
            return BadRequest(new { message = "Doctor is not assigned to the selected branch." });
        }

        var setting = await _context.ScheduleSettings
            .Where(x => x.HospitalId == hospitalId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var slotDuration = setting?.SlotDuration ?? 30;
        if (slotDuration <= 0) slotDuration = 30;

        var windows = await ScheduleAvailabilityHelper.GetEffectiveWindowsAsync(
            _context, hospitalId, doctorId, date.Date);
        var branchWindows = windows.Where(x => x.BranchId == branchId).OrderBy(x => x.Start).ToList();
        if (branchWindows.Count == 0)
        {
            return Ok(new List<object>());
        }

        var bookedSlots = await _context.Appointments
            .Where(x =>
                x.DoctorId == doctorId &&
                x.HospitalId == hospitalId &&
                x.Date.Date == date.Date &&
                x.Status != "Cancelled")
            .Select(x => x.StartTime)
            .ToListAsync();

        var result = new List<object>();
        foreach (var window in branchWindows)
        {
            var current = window.Start;
            while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= window.End)
            {
                var slotEnd = current.Add(TimeSpan.FromMinutes(slotDuration));
                if (window.BreakStart.HasValue && window.BreakEnd.HasValue &&
                    ScheduleAvailabilityHelper.TimeRangesOverlap(current, slotEnd, window.BreakStart.Value, window.BreakEnd.Value))
                {
                    current = window.BreakEnd.Value;
                    continue;
                }

                if (date.Date == DateTime.Today && date.Date.Add(current) <= DateTime.Now)
                {
                    current = slotEnd;
                    continue;
                }

                var booked = bookedSlots.Contains(current);
                result.Add(new
                {
                    start = date.Date.Add(current).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    end = date.Date.Add(slotEnd).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    status = booked ? "Booked" : "Available",
                    branchId,
                    source = window.Source
                });
                current = slotEnd;
            }
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

        if (patient.HospitalId == null)
        {
            return BadRequest(new { message = "Hospital is not assigned to the patient." });
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

        var hospitalId = patient.HospitalId.Value;

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == dto.BranchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new { message = "Invalid branch or branch is inactive." });
        }

        var doctor = await _context.Doctors
            .FirstOrDefaultAsync(x =>
                x.Id == dto.DoctorId &&
                x.HospitalId == hospitalId &&
                x.IsActive &&
                (
                    x.BranchId == dto.BranchId ||
                    _context.DoctorBranches.Any(db =>
                        db.DoctorId == x.Id &&
                        db.BranchId == dto.BranchId &&
                        db.HospitalId == hospitalId &&
                        db.IsActive)
                ));

        if (doctor == null)
        {
            return BadRequest(new { message = "Doctor not found in selected branch." });
        }

        var setting = await _context.ScheduleSettings
            .Where(x => x.HospitalId == hospitalId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var slotDuration = setting?.SlotDuration ?? 30;
        if (slotDuration <= 0) slotDuration = 30;

        var availability = await ScheduleAvailabilityHelper.ValidateBookableSlotAsync(
            _context,
            hospitalId,
            dto.DoctorId,
            dto.BranchId,
            dto.Date.Date,
            appointmentStartTime,
            slotDuration);

        if (!availability.ok)
        {
            return BadRequest(new { message = availability.message });
        }

        var slotBooked = await _context.Appointments.AnyAsync(x =>
            x.DoctorId == dto.DoctorId &&
            x.BranchId == dto.BranchId &&
            x.HospitalId == hospitalId &&
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
            HospitalId = hospitalId,
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
            patient.Id,
            "Appointment Booked",
            $"Appointment {appointment.TokenNumber} has been booked successfully.");

        return Ok(new
        {
            message = "Please complete the consultation fee payment to confirm your appointment.",
            appointmentId = appointment.Id,
            patientId = patient.Id,
            hospitalId,
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
            patient.Id,
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
            x.BranchId == appointment.BranchId &&
            x.HospitalId == appointment.HospitalId &&
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
            patient.Id,
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

    // =====================================================
    // 8. Billing & Payment
    // =====================================================

    private static string? NormalizeBillingType(string? billingType)
    {
        if (string.IsNullOrWhiteSpace(billingType))
        {
            return null;
        }

        var allowedTypes = new[]
        {
        "OP",
        "Lab",
        "Pharmacy"
    };

        return allowedTypes.FirstOrDefault(x =>
            x.Equals(
                billingType.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    // =====================================================
    // GET PATIENT BILLS
    // Supports OP, Lab and Pharmacy filters
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("bills")]
    public async Task<IActionResult> GetBills(
        [FromQuery] string? billingType = null,
        [FromQuery] string? status = null)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var normalizedType =
            NormalizeBillingType(billingType);

        if (!string.IsNullOrWhiteSpace(billingType) &&
            normalizedType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Appointment)
            .Include(x => x.Doctor)
            .Include(x => x.Branch)
            .Where(x =>
                x.PatientId == patient.Id &&
                x.HospitalId == patient.HospitalId);

        if (normalizedType != null)
        {
            query = query.Where(x =>
                x.BillingType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim().ToLower();

            query = query.Where(x =>
                x.Status.ToLower() ==
                normalizedStatus);
        }

        var bills = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                billId = x.Id,
                billNumber = "BILL-" + x.Id,

                billingType = x.BillingType,

                appointmentId = x.AppointmentId,

                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                doctorId = x.DoctorId,

                doctorName =
                    x.Doctor != null
                        ? x.Doctor.Name
                        : null,

                hospitalId = x.HospitalId,

                branchId = x.BranchId,

                branchName =
                    x.Branch != null
                        ? x.Branch.Name
                        : null,

                consultationCharge =
                    x.ConsultationCharge,

                labCharge =
                    x.LabCharge,

                medicineCharge =
                    x.MedicineCharge,

                subTotal =
                    x.SubTotal,

                gstPercentage =
                    x.GstPercentage,

                gstAmount =
                    x.GstAmount,

                totalAmount =
                    x.TotalAmount,

                paymentMode =
                    x.PaymentMode,

                status =
                    x.Status,

                createdAt =
                    x.CreatedAt,

                buttons = x.Status == "Paid"
                    ? new[]
                    {
                    "Download Invoice"
                    }
                    : new[]
                    {
                    "Pay Now",
                    "Download Invoice"
                    }
            })
            .ToListAsync();

        return Ok(bills);
    }

    // =====================================================
    // GET SINGLE BILL DETAILS
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpGet("bills/{id:int}")]
    public async Task<IActionResult> GetBillDetails(int id)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var bill = await _context.Billings
            .AsNoTracking()
            .Include(x => x.Appointment)
            .Include(x => x.Doctor)
            .Include(x => x.Hospital)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id &&
                x.HospitalId == patient.HospitalId);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Bill not found."
            });
        }

        return Ok(new
        {
            billId = bill.Id,
            billNumber = "BILL-" + bill.Id,

            billingType = bill.BillingType,

            appointmentId = bill.AppointmentId,

            appointmentNumber =
                bill.Appointment?.TokenNumber,

            patientId = bill.PatientId,

            doctorId = bill.DoctorId,

            doctorName =
                bill.Doctor?.Name,

            hospitalId = bill.HospitalId,

            hospitalName =
                bill.Hospital?.Name,

            branchId = bill.BranchId,

            branchName =
                bill.Branch?.Name,

            consultationCharge =
                bill.ConsultationCharge,

            labCharge =
                bill.LabCharge,

            medicineCharge =
                bill.MedicineCharge,

            subTotal =
                bill.SubTotal,

            gstPercentage =
                bill.GstPercentage,

            gstAmount =
                bill.GstAmount,

            totalAmount =
                bill.TotalAmount,

            paymentMode =
                bill.PaymentMode,

            status =
                bill.Status,

            createdAt =
                bill.CreatedAt
        });
    }

    // =====================================================
    // PAY PATIENT BILL
    // =====================================================

    [Authorize(Roles = "Patient")]
    [HttpPost("bills/{id:int}/pay")]
    public async Task<IActionResult> PayBill(
        int id,
        [FromBody] PatientPortalPaymentDto dto)
    {
        var patient = await GetLoggedInPatient();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Patient profile not found."
            });
        }

        var bill = await _context.Billings
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.PatientId == patient.Id &&
                x.HospitalId == patient.HospitalId);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Bill not found."
            });
        }

        if (bill.Status.Equals(
                "Paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Bill is already paid."
            });
        }

        var allowedModes = new[]
        {
        "Cash",
        "Card",
        "UPI",
        "Online"
    };

        var paymentMode =
            allowedModes.FirstOrDefault(x =>
                x.Equals(
                    dto.PaymentMode?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (paymentMode == null)
        {
            return BadRequest(new
            {
                message =
                    "PaymentMode must be Cash, Card, UPI, or Online."
            });
        }

        bill.PaymentMode =
            paymentMode;

        bill.Status =
            "Paid";

        // OP payment confirms the appointment.
        if (bill.BillingType.Equals(
                "OP",
                StringComparison.OrdinalIgnoreCase) &&
            bill.Appointment != null)
        {
            bill.Appointment.PaymentStatus =
                "Paid";

            if (bill.Appointment.Status == "Pending")
            {
                bill.Appointment.Status =
                    "Confirmed";
            }

            // Generate queue token after successful OP payment.
            if (string.IsNullOrWhiteSpace(
                    bill.Appointment.TokenNumber) ||
                !bill.Appointment.TokenNumber.StartsWith(
                    "TKN",
                    StringComparison.OrdinalIgnoreCase))
            {
                var date = bill.Appointment.Date.Date;

                var tokenCount =
                    await _context.Appointments.CountAsync(x =>
                        x.DoctorId ==
                            bill.Appointment.DoctorId &&
                        x.BranchId ==
                            bill.Appointment.BranchId &&
                        x.Date.Date == date &&
                        x.PaymentStatus == "Paid" &&
                        x.Id != bill.Appointment.Id);

                bill.Appointment.TokenNumber =
                    $"TKN-{tokenCount + 1:D3}";
            }
        }

        await _context.SaveChangesAsync();

        await CreateNotification(
            patient.Id,
            "Payment Completed",
            $"{bill.BillingType} payment completed for BILL-{bill.Id}.");

        return Ok(new
        {
            message =
                "Payment completed successfully.",

            billId =
                bill.Id,

            billingType =
                bill.BillingType,

            totalAmount =
                bill.TotalAmount,

            status =
                bill.Status,

            paymentMode =
                bill.PaymentMode,

            appointmentId =
                bill.AppointmentId,

            appointmentStatus =
                bill.Appointment?.Status,

            appointmentPaymentStatus =
                bill.Appointment?.PaymentStatus,

            tokenNumber =
                bill.Appointment?.TokenNumber
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
                x.PatientId == patient.Id)
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
                x.PatientId == patient.Id);

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
                x.PatientId == patient.Id);

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
