using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[RequirePermission("Schedule")]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly AppDbContext _context;

    public ScheduleController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // TOKEN HELPERS
    // =====================================================

    private int GetHospitalId()
    {
        var claim = User.Claims.FirstOrDefault(x => x.Type == "HospitalId");

        return claim != null && int.TryParse(claim.Value, out var hospitalId)
            ? hospitalId
            : 0;
    }

    private int GetDoctorId()
    {
        var claim = User.Claims.FirstOrDefault(x => x.Type == "DoctorId");

        return claim != null && int.TryParse(claim.Value, out var doctorId)
            ? doctorId
            : 0;
    }

    private string GetCurrentRole()
    {
        return User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value
            ?? User.Claims.FirstOrDefault(x => x.Type == "role")?.Value
            ?? string.Empty;
    }

    // =====================================================
    // TIME PARSING
    // Accepts both 12-hour and 24-hour input.
    // Examples: 09:00 AM, 9:00 AM, 17:30
    // =====================================================

    private static bool TryParseTime(string? input, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var acceptedFormats = new[]
        {
            "hh:mm tt",
            "h:mm tt",
            "HH:mm",
            "H:mm"
        };

        var success = DateTime.TryParseExact(
            input.Trim(),
            acceptedFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsedDateTime);

        if (!success)
        {
            return false;
        }

        time = parsedDateTime.TimeOfDay;
        return true;
    }

    private static List<string> NormalizeDays(IEnumerable<string> days)
    {
        return days
            .Where(day => !string.IsNullOrWhiteSpace(day))
            .Select(day => CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(day.Trim().ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetInvalidDays(IEnumerable<string> days)
    {
        var validDayNames = new HashSet<string>(
            Enum.GetNames<DayOfWeek>(),
            StringComparer.OrdinalIgnoreCase);

        return days
            .Where(day => string.IsNullOrWhiteSpace(day) ||
                          !validDayNames.Contains(day.Trim()))
            .ToList();
    }

    private async Task<bool> DoctorIsAssignedToBranchAsync(
        Doctor doctor,
        int branchId)
    {
        if (doctor.BranchId == branchId)
        {
            return true;
        }

        return await _context.DoctorBranches.AnyAsync(x =>
            x.DoctorId == doctor.Id &&
            x.BranchId == branchId &&
            x.IsActive);
    }

    // =====================================================
    // CREATE SCHEDULE
    // Admin can create for any doctor in the same hospital.
    // Doctor can create only their own schedule.
    // =====================================================

    [Authorize(Roles = "Admin,Doctor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Invalid HospitalId in authentication token."
            });
        }

        var role = GetCurrentRole();
        var doctorId = dto.DoctorId;

        if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            doctorId = GetDoctorId();

            if (doctorId <= 0)
            {
                return Unauthorized(new
                {
                    message = "DoctorId is missing from the authentication token."
                });
            }
        }

        if (dto.BranchId == null || dto.BranchId <= 0)
        {
            return BadRequest(new
            {
                message = "Please select a valid branch."
            });
        }

        if (doctorId <= 0)
        {
            return BadRequest(new
            {
                message = "Please select a valid doctor."
            });
        }

        if (dto.Days == null || dto.Days.Count == 0)
        {
            return BadRequest(new
            {
                message = "Please select at least one working day."
            });
        }

        if (dto.StartDate.Date > dto.EndDate.Date)
        {
            return BadRequest(new
            {
                message = "StartDate cannot be greater than EndDate."
            });
        }

        var invalidDays = GetInvalidDays(dto.Days);

        if (invalidDays.Count > 0)
        {
            return BadRequest(new
            {
                message = "Invalid day name. Use Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, or Saturday.",
                invalidDays
            });
        }

        if (!TryParseTime(dto.WorkStart, out var workStart))
        {
            return BadRequest(new
            {
                message = "Invalid WorkStart. Use a format such as 09:00 AM or 09:00."
            });
        }

        if (!TryParseTime(dto.WorkEnd, out var workEnd))
        {
            return BadRequest(new
            {
                message = "Invalid WorkEnd. Use a format such as 05:00 PM or 17:00."
            });
        }

        if (!TryParseTime(dto.BreakStart, out var breakStart))
        {
            return BadRequest(new
            {
                message = "Invalid BreakStart. Use a format such as 01:00 PM or 13:00."
            });
        }

        if (!TryParseTime(dto.BreakEnd, out var breakEnd))
        {
            return BadRequest(new
            {
                message = "Invalid BreakEnd. Use a format such as 02:00 PM or 14:00."
            });
        }

        if (workStart >= workEnd)
        {
            return BadRequest(new
            {
                message = "WorkStart must be earlier than WorkEnd."
            });
        }

        if (breakStart >= breakEnd)
        {
            return BadRequest(new
            {
                message = "BreakStart must be earlier than BreakEnd."
            });
        }

        if (breakStart < workStart || breakEnd > workEnd)
        {
            return BadRequest(new
            {
                message = "Break time must be within doctor working hours."
            });
        }

        var branchId = dto.BranchId.Value;

        var branch = await _context.Branches.FirstOrDefaultAsync(x =>
            x.Id == branchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (branch == null)
        {
            return BadRequest(new
            {
                message = "Invalid branch or branch is inactive."
            });
        }

        var doctor = await _context.Doctors.FirstOrDefaultAsync(x =>
            x.Id == doctorId &&
            x.HospitalId == hospitalId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found."
            });
        }

        if (!await DoctorIsAssignedToBranchAsync(doctor, branchId))
        {
            return BadRequest(new
            {
                message = "Selected doctor is not assigned to the selected branch."
            });
        }

        var normalizedDays = NormalizeDays(dto.Days);

        var overlappingSchedule = await _context.Schedules.AnyAsync(x =>
            x.HospitalId == hospitalId &&
            x.DoctorId == doctorId &&
            x.BranchId == branchId &&
            dto.StartDate.Date <= x.EndDate.Date &&
            dto.EndDate.Date >= x.StartDate.Date);

        if (overlappingSchedule)
        {
            return Conflict(new
            {
                message = "An overlapping schedule already exists for this doctor and branch. Update the existing schedule instead."
            });
        }

        var schedule = new Schedule
        {
            DoctorId = doctorId,
            BranchId = branchId,
            HospitalId = hospitalId,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            Days = string.Join(",", normalizedDays),
            WorkStart = workStart,
            WorkEnd = workEnd,
            BreakStart = breakStart,
            BreakEnd = breakEnd
        };

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Schedule saved successfully.",
            scheduleId = schedule.Id,
            schedule.BranchId,
            schedule.DoctorId,
            days = normalizedDays,
            startDate = schedule.StartDate.ToString("yyyy-MM-dd"),
            endDate = schedule.EndDate.ToString("yyyy-MM-dd"),
            workStart = DateTime.Today.Add(schedule.WorkStart)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            workEnd = DateTime.Today.Add(schedule.WorkEnd)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            breakStart = DateTime.Today.Add(schedule.BreakStart)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            breakEnd = DateTime.Today.Add(schedule.BreakEnd)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture)
        });
    }

    // =====================================================
    // GET DAY SLOTS
    // =====================================================

    [HttpGet("day-slots")]
    public async Task<IActionResult> GetSlots(int doctorId, DateTime date)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Invalid HospitalId in authentication token."
            });
        }

        if (doctorId <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid doctorId."
            });
        }

        var holiday = await _context.Holidays.FirstOrDefaultAsync(x =>
            x.HospitalId == hospitalId &&
            x.Date.Date == date.Date);

        if (holiday != null)
        {
            return Ok(new
            {
                isHoliday = true,
                message = $"Clinic Holiday - {holiday.Name}",
                slots = new List<object>()
            });
        }

        var schedule = await _context.Schedules
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.DoctorId == doctorId &&
                date.Date >= x.StartDate.Date &&
                date.Date <= x.EndDate.Date)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (schedule == null)
        {
            return NotFound(new
            {
                message = "No schedule found."
            });
        }

        var setting = await _context.ScheduleSettings
            .Where(x => x.HospitalId == hospitalId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var slotDuration = setting?.SlotDuration ?? 30;

        if (slotDuration <= 0)
        {
            slotDuration = 30;
        }

        var validDays = schedule.Days.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        var requestedDay = date.DayOfWeek.ToString();

        if (!validDays.Contains(requestedDay, StringComparer.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                isHoliday = false,
                message = "Doctor is not scheduled for the selected day.",
                slots = new List<object>()
            });
        }

        var bookings = await _context.Appointments
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.DoctorId == doctorId &&
                x.Date.Date == date.Date)
            .ToListAsync();

        var result = new List<object>();
        var current = schedule.WorkStart;

        while (current < schedule.WorkEnd)
        {
            if (current >= schedule.BreakStart && current < schedule.BreakEnd)
            {
                current = schedule.BreakEnd;
                continue;
            }

            var end = current.Add(TimeSpan.FromMinutes(slotDuration));

            if (end > schedule.WorkEnd)
            {
                break;
            }

            if (current < schedule.BreakStart && end > schedule.BreakStart)
            {
                current = schedule.BreakEnd;
                continue;
            }

            var slotDateTime = date.Date.Add(current);

            if (date.Date == DateTime.Today && slotDateTime <= DateTime.Now)
            {
                current = end;
                continue;
            }

            var isBooked = bookings.Any(booking =>
                booking.StartTime == current &&
                booking.Status != "Cancelled");

            result.Add(new
            {
                start = date.Date.Add(current)
                    .ToString("hh:mm tt", CultureInfo.InvariantCulture),
                end = date.Date.Add(end)
                    .ToString("hh:mm tt", CultureInfo.InvariantCulture),
                status = isBooked ? "Booked" : "Available"
            });

            current = end;
        }

        return Ok(new
        {
            isHoliday = false,
            doctorId,
            date = date.Date.ToString("yyyy-MM-dd"),
            workStart = date.Date.Add(schedule.WorkStart)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            workEnd = date.Date.Add(schedule.WorkEnd)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            breakStart = date.Date.Add(schedule.BreakStart)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            breakEnd = date.Date.Add(schedule.BreakEnd)
                .ToString("hh:mm tt", CultureInfo.InvariantCulture),
            slotDuration,
            slots = result
        });
    }

    // =====================================================
    // UPDATE SCHEDULE
    // Doctor can update only their own schedule.
    // =====================================================

    [Authorize(Roles = "Admin,Doctor")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSchedule(
        int id,
        [FromBody] UpdateScheduleDto dto)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Invalid HospitalId in authentication token."
            });
        }

        var schedule = await _context.Schedules.FirstOrDefaultAsync(x =>
            x.Id == id &&
            x.HospitalId == hospitalId);

        if (schedule == null)
        {
            return NotFound(new
            {
                message = "Schedule not found."
            });
        }

        var role = GetCurrentRole();
        var targetDoctorId = dto.DoctorId;

        if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            var loggedDoctorId = GetDoctorId();

            if (loggedDoctorId <= 0)
            {
                return Unauthorized(new
                {
                    message = "DoctorId is missing from the authentication token."
                });
            }

            if (schedule.DoctorId != loggedDoctorId)
            {
                return Forbid();
            }

            targetDoctorId = loggedDoctorId;
        }

        if (dto.BranchId == null || dto.BranchId <= 0)
        {
            return BadRequest(new
            {
                message = "Please select a valid branch."
            });
        }

        if (targetDoctorId <= 0)
        {
            return BadRequest(new
            {
                message = "Please select a valid doctor."
            });
        }

        if (dto.Days == null || dto.Days.Count == 0)
        {
            return BadRequest(new
            {
                message = "Please select at least one working day."
            });
        }

        var invalidDays = GetInvalidDays(dto.Days);

        if (invalidDays.Count > 0)
        {
            return BadRequest(new
            {
                message = "Invalid day name. Use Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, or Saturday.",
                invalidDays
            });
        }

        if (!TryParseTime(dto.WorkStart, out var workStart) ||
            !TryParseTime(dto.WorkEnd, out var workEnd) ||
            !TryParseTime(dto.BreakStart, out var breakStart) ||
            !TryParseTime(dto.BreakEnd, out var breakEnd))
        {
            return BadRequest(new
            {
                message = "Use a time format such as 09:00 AM or 09:00."
            });
        }

        if (dto.StartDate.Date > dto.EndDate.Date)
        {
            return BadRequest(new
            {
                message = "StartDate cannot be greater than EndDate."
            });
        }

        if (workStart >= workEnd)
        {
            return BadRequest(new
            {
                message = "WorkStart must be earlier than WorkEnd."
            });
        }

        if (breakStart >= breakEnd)
        {
            return BadRequest(new
            {
                message = "BreakStart must be earlier than BreakEnd."
            });
        }

        if (breakStart < workStart || breakEnd > workEnd)
        {
            return BadRequest(new
            {
                message = "Break time must be within doctor working hours."
            });
        }

        var branchId = dto.BranchId.Value;

        var branchOk = await _context.Branches.AnyAsync(x =>
            x.Id == branchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchOk)
        {
            return BadRequest(new
            {
                message = "Invalid branch or branch is inactive."
            });
        }

        var doctor = await _context.Doctors.FirstOrDefaultAsync(x =>
            x.Id == targetDoctorId &&
            x.HospitalId == hospitalId);

        if (doctor == null)
        {
            return NotFound(new
            {
                message = "Doctor not found."
            });
        }

        if (!await DoctorIsAssignedToBranchAsync(doctor, branchId))
        {
            return BadRequest(new
            {
                message = "Selected doctor is not assigned to the selected branch."
            });
        }

        var overlappingSchedule = await _context.Schedules.AnyAsync(x =>
            x.Id != id &&
            x.HospitalId == hospitalId &&
            x.DoctorId == targetDoctorId &&
            x.BranchId == branchId &&
            dto.StartDate.Date <= x.EndDate.Date &&
            dto.EndDate.Date >= x.StartDate.Date);

        if (overlappingSchedule)
        {
            return Conflict(new
            {
                message = "Another overlapping schedule already exists for this doctor and branch."
            });
        }

        schedule.DoctorId = targetDoctorId;
        schedule.BranchId = branchId;
        schedule.StartDate = dto.StartDate.Date;
        schedule.EndDate = dto.EndDate.Date;
        schedule.Days = string.Join(",", NormalizeDays(dto.Days));
        schedule.WorkStart = workStart;
        schedule.WorkEnd = workEnd;
        schedule.BreakStart = breakStart;
        schedule.BreakEnd = breakEnd;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Schedule updated successfully.",
            schedule.Id,
            schedule.DoctorId,
            schedule.BranchId
        });
    }

    // =====================================================
    // DELETE SCHEDULE
    // Doctor can delete only their own schedule.
    // =====================================================

    [Authorize(Roles = "Admin,Doctor")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Invalid HospitalId in authentication token."
            });
        }

        var schedule = await _context.Schedules.FirstOrDefaultAsync(x =>
            x.Id == id &&
            x.HospitalId == hospitalId);

        if (schedule == null)
        {
            return NotFound(new
            {
                message = "Schedule not found."
            });
        }

        var role = GetCurrentRole();

        if (string.Equals(role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            var loggedDoctorId = GetDoctorId();

            if (loggedDoctorId <= 0)
            {
                return Unauthorized(new
                {
                    message = "DoctorId is missing from the authentication token."
                });
            }

            if (schedule.DoctorId != loggedDoctorId)
            {
                return Forbid();
            }
        }

        var hasFutureAppointments = await _context.Appointments.AnyAsync(x =>
            x.HospitalId == hospitalId &&
            x.DoctorId == schedule.DoctorId &&
            x.BranchId == schedule.BranchId &&
            x.Date.Date >= DateTime.Today &&
            x.Status != "Cancelled");

        if (hasFutureAppointments)
        {
            return Conflict(new
            {
                message = "Cannot delete schedule because future appointments exist."
            });
        }

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Schedule deleted successfully."
        });
    }
}
