using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
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

    public ScheduleController(AppDbContext context) => _context = context;

    private int GetHospitalId() =>
        int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    private int GetDoctorId() =>
        int.TryParse(User.FindFirstValue("DoctorId"), out var id) ? id : 0;

    private string GetCurrentRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;

    private async Task<bool> DoctorIsAssignedToBranchAsync(Doctor doctor, int branchId)
    {
        if (doctor.BranchId == branchId) return true;
        return await _context.DoctorBranches.AnyAsync(x =>
            x.DoctorId == doctor.Id && x.BranchId == branchId &&
            x.HospitalId == doctor.HospitalId && x.IsActive);
    }

    private static List<string> NormalizeDays(IEnumerable<string> days) =>
        days.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x.Trim().ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static bool ScheduleDaysIntersect(string? existingDays, IEnumerable<string> requested)
    {
        var set = new HashSet<string>((existingDays ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        return requested.Any(set.Contains);
    }

    private static object FormatSchedule(Schedule s) => new
    {
        exists = true,
        id = s.Id,
        doctorId = s.DoctorId,
        branchId = s.BranchId,
        days = (s.Days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        startDate = s.StartDate.ToString("yyyy-MM-dd"),
        endDate = s.EndDate.ToString("yyyy-MM-dd"),
        workStart = DateTime.Today.Add(s.WorkStart).ToString("hh:mm tt", CultureInfo.InvariantCulture),
        workEnd = DateTime.Today.Add(s.WorkEnd).ToString("hh:mm tt", CultureInfo.InvariantCulture),
        breakStart = DateTime.Today.Add(s.BreakStart).ToString("hh:mm tt", CultureInfo.InvariantCulture),
        breakEnd = DateTime.Today.Add(s.BreakEnd).ToString("hh:mm tt", CultureInfo.InvariantCulture)
    };

    private async Task<(Doctor? doctor, IActionResult? error)> ResolveDoctorAsync(int requestedDoctorId)
    {
        var hospitalId = GetHospitalId();
        if (hospitalId <= 0)
            return (null, Unauthorized(new { message = "Invalid HospitalId in authentication token." }));

        var doctorId = requestedDoctorId;
        if (string.Equals(GetCurrentRole(), "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            doctorId = GetDoctorId();
            if (doctorId <= 0)
                return (null, Unauthorized(new { message = "DoctorId is missing from authentication token." }));
        }

        var doctor = await _context.Doctors.FirstOrDefaultAsync(x =>
            x.Id == doctorId && x.HospitalId == hospitalId && x.IsActive);
        return doctor == null
            ? (null, NotFound(new { message = "Doctor not found." }))
            : (doctor, null);
    }

    private async Task<IActionResult?> ValidateBaseScheduleAsync(
        CreateScheduleDto dto, Doctor doctor, int? excludeScheduleId = null)
    {
        var hospitalId = GetHospitalId();
        if (!dto.BranchId.HasValue || dto.BranchId <= 0)
            return BadRequest(new { message = "Please select a valid branch." });
        if (dto.Days == null || dto.Days.Count == 0)
            return BadRequest(new { message = "Please select at least one working day." });
        if (dto.StartDate.Date > dto.EndDate.Date)
            return BadRequest(new { message = "StartDate cannot be greater than EndDate." });

        var validDays = new HashSet<string>(Enum.GetNames<DayOfWeek>(), StringComparer.OrdinalIgnoreCase);
        var normalizedDays = NormalizeDays(dto.Days);
        if (normalizedDays.Any(x => !validDays.Contains(x)))
            return BadRequest(new { message = "Invalid working day name." });

        if (!ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var workStart) ||
            !ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var workEnd) ||
            !ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var breakStart) ||
            !ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var breakEnd))
            return BadRequest(new { message = "Use valid times such as 09:00 AM and 06:00 PM." });

        if (workStart >= workEnd)
            return BadRequest(new { message = "WorkStart must be earlier than WorkEnd." });
        if (breakStart >= breakEnd || breakStart < workStart || breakEnd > workEnd)
            return BadRequest(new { message = "Break time must be inside doctor working hours." });

        var branch = await _context.Branches.FirstOrDefaultAsync(x =>
            x.Id == dto.BranchId.Value && x.HospitalId == hospitalId && x.IsActive);
        if (branch == null) return BadRequest(new { message = "Invalid branch or branch is inactive." });
        if (!await DoctorIsAssignedToBranchAsync(doctor, dto.BranchId.Value))
            return BadRequest(new { message = "Selected doctor is not assigned to the selected branch." });

        // Prevent same doctor working in two branches at the same time.
        var candidates = await _context.Schedules.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.DoctorId == doctor.Id &&
                        (!excludeScheduleId.HasValue || x.Id != excludeScheduleId.Value) &&
                        dto.StartDate.Date <= x.EndDate.Date && dto.EndDate.Date >= x.StartDate.Date)
            .ToListAsync();

        var conflict = candidates.FirstOrDefault(x =>
            ScheduleDaysIntersect(x.Days, normalizedDays) &&
            ScheduleAvailabilityHelper.TimeRangesOverlap(workStart, workEnd, x.WorkStart, x.WorkEnd));
        if (conflict != null)
        {
            var branchName = await _context.Branches.Where(x => x.Id == conflict.BranchId)
                .Select(x => x.Name).FirstOrDefaultAsync();
            return Conflict(new
            {
                message = $"Schedule conflicts with an existing doctor schedule in {branchName ?? $"Branch {conflict.BranchId}"} from {DateTime.Today.Add(conflict.WorkStart):hh:mm tt} to {DateTime.Today.Add(conflict.WorkEnd):hh:mm tt}."
            });
        }
        return null;
    }

    // =====================================================
    // BASE RECURRING SCHEDULE
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduleDto dto)
    {
        var resolved = await ResolveDoctorAsync(dto.DoctorId);
        if (resolved.error != null) return resolved.error;
        var doctor = resolved.doctor!;
        dto.DoctorId = doctor.Id;

        var validation = await ValidateBaseScheduleAsync(dto, doctor);
        if (validation != null) return validation;

        ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var ws);
        ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var we);
        ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var bs);
        ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var be);

        var schedule = new Schedule
        {
            DoctorId = doctor.Id,
            BranchId = dto.BranchId,
            HospitalId = GetHospitalId(),
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            Days = string.Join(',', NormalizeDays(dto.Days!)),
            WorkStart = ws,
            WorkEnd = we,
            BreakStart = bs,
            BreakEnd = be
        };
        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Schedule saved successfully.", scheduleId = schedule.Id });
    }

    [Authorize(Roles = "Admin,Doctor")]
    [HttpGet("doctor/{doctorId:int}")]
    public async Task<IActionResult> GetDoctorSchedule(int doctorId, [FromQuery] int branchId)
    {
        var resolved = await ResolveDoctorAsync(doctorId);
        if (resolved.error != null) return resolved.error;
        var actualDoctorId = resolved.doctor!.Id;

        var schedule = await _context.Schedules.AsNoTracking()
            .Where(x => x.HospitalId == GetHospitalId() && x.DoctorId == actualDoctorId && x.BranchId == branchId)
            .OrderByDescending(x => x.EndDate).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (schedule == null)
            return Ok(new { exists = false, doctorId = actualDoctorId, branchId, message = "No recurring schedule has been created yet." });
        return Ok(FormatSchedule(schedule));
    }

    [Authorize(Roles = "Admin,Doctor")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] UpdateScheduleDto dto)
    {
        var hospitalId = GetHospitalId();
        var schedule = await _context.Schedules.FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        if (schedule == null) return NotFound(new { message = "Schedule not found." });

        var resolved = await ResolveDoctorAsync(dto.DoctorId <= 0 ? schedule.DoctorId : dto.DoctorId);
        if (resolved.error != null) return resolved.error;
        var doctor = resolved.doctor!;
        if (User.IsInRole("Doctor") && schedule.DoctorId != doctor.Id) return Forbid();
        dto.DoctorId = doctor.Id;

        var validation = await ValidateBaseScheduleAsync(dto, doctor, id);
        if (validation != null) return validation;
        ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var ws);
        ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var we);
        ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var bs);
        ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var be);

        // Never silently invalidate already-booked future appointments when editing
        // the recurring schedule. Use date-specific overrides after rescheduling/cancelling.
        var normalizedDays = NormalizeDays(dto.Days!);
        var futureBooked = await _context.Appointments.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.DoctorId == schedule.DoctorId &&
                        x.BranchId == schedule.BranchId && x.Date.Date >= DateTime.Today &&
                        x.Status != "Cancelled")
            .ToListAsync();
        var invalidBooked = futureBooked.FirstOrDefault(a =>
            a.Date.Date < dto.StartDate.Date || a.Date.Date > dto.EndDate.Date ||
            !normalizedDays.Contains(a.Date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase) ||
            a.StartTime < ws || a.StartTime >= we ||
            (a.StartTime >= bs && a.StartTime < be) ||
            dto.BranchId != schedule.BranchId);
        if (invalidBooked != null)
            return Conflict(new
            {
                message = $"Cannot update this recurring schedule because appointment {invalidBooked.Id} on {invalidBooked.Date:yyyy-MM-dd} at {DateTime.Today.Add(invalidBooked.StartTime):hh:mm tt} would become invalid. Reschedule/cancel that appointment first, or use a date-specific override that does not affect booked slots."
            });

        schedule.DoctorId = doctor.Id;
        schedule.BranchId = dto.BranchId;
        schedule.StartDate = dto.StartDate.Date;
        schedule.EndDate = dto.EndDate.Date;
        schedule.Days = string.Join(',', NormalizeDays(dto.Days!));
        schedule.WorkStart = ws; schedule.WorkEnd = we; schedule.BreakStart = bs; schedule.BreakEnd = be;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Schedule updated successfully.", scheduleId = schedule.Id });
    }

    [Authorize(Roles = "Admin,Doctor")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var hospitalId = GetHospitalId();
        var schedule = await _context.Schedules.FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        if (schedule == null) return NotFound(new { message = "Schedule not found." });
        if (User.IsInRole("Doctor") && schedule.DoctorId != GetDoctorId()) return Forbid();

        var hasFutureAppointments = await _context.Appointments.AnyAsync(x =>
            x.HospitalId == hospitalId && x.DoctorId == schedule.DoctorId && x.BranchId == schedule.BranchId &&
            x.Date.Date >= DateTime.Today && x.Status != "Cancelled");
        if (hasFutureAppointments)
            return Conflict(new { message = "Cannot delete this recurring schedule while future appointments exist. Use a time change/leave override for unbooked future slots instead." });

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Schedule deleted successfully." });
    }

    // =====================================================
    // DATE-SPECIFIC OVERRIDES: LEAVE / TIME CHANGE / BRANCH SHIFT
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpGet("overrides")]
    public async Task<IActionResult> GetOverrides([FromQuery] int doctorId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var resolved = await ResolveDoctorAsync(doctorId);
        if (resolved.error != null) return resolved.error;
        doctorId = resolved.doctor!.Id;
        var start = (from ?? DateTime.Today).Date;
        var end = (to ?? start.AddMonths(2)).Date;
        var data = await _context.ScheduleOverrides.AsNoTracking()
            .Where(x => x.HospitalId == GetHospitalId() && x.DoctorId == doctorId && x.IsActive && x.Date.Date >= start && x.Date.Date <= end)
            .OrderBy(x => x.Date).ThenBy(x => x.WorkStart)
            .Select(x => new
            {
                x.Id,
                x.DoctorId,
                x.BranchId,
                x.SourceBranchId,
                date = x.Date.ToString("yyyy-MM-dd"),
                x.OverrideType,
                workStart = x.WorkStart.HasValue ? DateTime.Today.Add(x.WorkStart.Value).ToString("hh:mm tt") : null,
                workEnd = x.WorkEnd.HasValue ? DateTime.Today.Add(x.WorkEnd.Value).ToString("hh:mm tt") : null,
                breakStart = x.BreakStart.HasValue ? DateTime.Today.Add(x.BreakStart.Value).ToString("hh:mm tt") : null,
                breakEnd = x.BreakEnd.HasValue ? DateTime.Today.Add(x.BreakEnd.Value).ToString("hh:mm tt") : null,
                x.Reason
            }).ToListAsync();
        return Ok(data);
    }

    private async Task<IActionResult?> ValidateOverrideAsync(ScheduleOverrideDto dto, Doctor doctor, int? excludeId = null)
    {
        var hospitalId = GetHospitalId();
        var type = (dto.OverrideType ?? string.Empty).Trim();
        if (!new[] { "Leave", "TimeChange", "BranchShift" }.Contains(type, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "OverrideType must be Leave, TimeChange, or BranchShift." });
        dto.OverrideType = type.Equals("Leave", StringComparison.OrdinalIgnoreCase)
            ? "Leave"
            : type.Equals("TimeChange", StringComparison.OrdinalIgnoreCase)
                ? "TimeChange"
                : "BranchShift";
        if (dto.Date.Date < DateTime.Today)
            return BadRequest(new { message = "Past dates cannot be changed." });
        if (dto.BranchId.HasValue && !await DoctorIsAssignedToBranchAsync(doctor, dto.BranchId.Value))
            return BadRequest(new { message = "Doctor is not assigned to the selected branch." });

        if (dto.OverrideType == "Leave")
        {
            var booked = await _context.Appointments.AnyAsync(x =>
                x.HospitalId == hospitalId && x.DoctorId == doctor.Id && x.Date.Date == dto.Date.Date &&
                (!dto.BranchId.HasValue || x.BranchId == dto.BranchId) && x.Status != "Cancelled");
            if (booked) return Conflict(new { message = "Leave cannot be applied because booked appointments exist for this date/branch. Reschedule or cancel those appointments first." });
            return null;
        }

        if (!ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var ws) ||
            !ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var we) || ws >= we)
            return BadRequest(new { message = "Valid WorkStart and WorkEnd are required." });

        TimeSpan? bs = null, be = null;
        if (!string.IsNullOrWhiteSpace(dto.BreakStart) || !string.IsNullOrWhiteSpace(dto.BreakEnd))
        {
            if (!ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var bsv) ||
                !ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var bev) || bsv >= bev || bsv < ws || bev > we)
                return BadRequest(new { message = "Break time must be valid and inside working hours." });
            bs = bsv; be = bev;
        }

        if (dto.OverrideType == "TimeChange" && dto.BranchId.HasValue)
        {
            var bookedForBranch = await _context.Appointments.AsNoTracking()
                .Where(x => x.HospitalId == hospitalId && x.DoctorId == doctor.Id &&
                            x.BranchId == dto.BranchId && x.Date.Date == dto.Date.Date &&
                            x.Status != "Cancelled")
                .ToListAsync();
            var invalidBooked = bookedForBranch.FirstOrDefault(a =>
                a.StartTime < ws || a.StartTime >= we ||
                (bs.HasValue && be.HasValue && a.StartTime >= bs.Value && a.StartTime < be.Value));
            if (invalidBooked != null)
                return Conflict(new { message = "The new hours would remove an already-booked appointment. Reschedule/cancel booked appointments first." });
        }

        if (dto.OverrideType == "BranchShift")
        {
            if (!dto.SourceBranchId.HasValue || !dto.BranchId.HasValue || dto.SourceBranchId == dto.BranchId)
                return BadRequest(new { message = "BranchShift requires different SourceBranchId and BranchId." });
            if (!await DoctorIsAssignedToBranchAsync(doctor, dto.SourceBranchId.Value))
                return BadRequest(new { message = "Doctor is not assigned to the source branch." });

            var effectiveBeforeShift = await ScheduleAvailabilityHelper.GetEffectiveWindowsAsync(
                _context, hospitalId, doctor.Id, dto.Date.Date);
            var sourceContainsRange = effectiveBeforeShift.Any(w =>
                w.BranchId == dto.SourceBranchId.Value && ws >= w.Start && we <= w.End);
            if (!sourceContainsRange)
                return BadRequest(new { message = "The selected shift range is not part of the doctor's current source-branch availability." });

            // Only future/unbooked source slots may be shifted. Completed time can remain in source branch.
            var bookedInShift = await _context.Appointments.AnyAsync(x =>
                x.HospitalId == hospitalId && x.DoctorId == doctor.Id && x.Date.Date == dto.Date.Date &&
                x.BranchId == dto.SourceBranchId && x.Status != "Cancelled" && x.StartTime >= ws && x.StartTime < we);
            if (bookedInShift)
                return Conflict(new { message = "Cannot shift this time range because booked appointments exist in the source branch." });
            if (dto.Date.Date == DateTime.Today && we <= DateTime.Now.TimeOfDay)
                return BadRequest(new { message = "The entire shift range has already completed." });
            if (dto.Date.Date == DateTime.Today && ws < DateTime.Now.TimeOfDay)
                return BadRequest(new { message = "For today's branch shift, start time must be after the current time. Completed source-branch slots remain unchanged." });
        }

        // Prevent two active date overrides from placing the doctor in two branches at the same time.
        var existing = await _context.ScheduleOverrides.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.DoctorId == doctor.Id && x.Date.Date == dto.Date.Date && x.IsActive &&
                        (!excludeId.HasValue || x.Id != excludeId.Value) && x.OverrideType != "Leave")
            .ToListAsync();
        var overrideConflict = existing.FirstOrDefault(x => x.WorkStart.HasValue && x.WorkEnd.HasValue &&
            x.BranchId != dto.BranchId && ScheduleAvailabilityHelper.TimeRangesOverlap(ws, we, x.WorkStart.Value, x.WorkEnd.Value));
        if (overrideConflict != null)
            return Conflict(new { message = "This override overlaps another branch/time override for the same doctor." });

        // Preserve existing booked appointments in the target time range too.
        var bookedTarget = await _context.Appointments.AnyAsync(x =>
            x.HospitalId == hospitalId && x.DoctorId == doctor.Id && x.Date.Date == dto.Date.Date &&
            x.Status != "Cancelled" && x.StartTime >= ws && x.StartTime < we && x.BranchId != dto.BranchId);
        if (bookedTarget)
            return Conflict(new { message = "Doctor already has a booked appointment in another branch during this time range." });

        return null;
    }

    // =====================================================
    // CREATE LEAVE
    // POST /api/Schedule/overrides/leave
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpPost("overrides/leave")]
    public async Task<IActionResult> CreateLeave([FromBody] LeaveScheduleOverrideDto request)
    {
        var dto = new ScheduleOverrideDto
        {
            DoctorId = request.DoctorId,
            BranchId = request.BranchId,
            Date = request.Date,
            OverrideType = "Leave",
            Reason = request.Reason
        };

        var resolved = await ResolveDoctorAsync(dto.DoctorId);
        if (resolved.error != null) return resolved.error;
        var doctor = resolved.doctor!;
        dto.DoctorId = doctor.Id;

        var validation = await ValidateOverrideAsync(dto, doctor);
        if (validation != null) return validation;

        var entity = new ScheduleOverride
        {
            HospitalId = GetHospitalId(),
            DoctorId = doctor.Id,
            BranchId = dto.BranchId,
            Date = dto.Date.Date,
            OverrideType = "Leave",
            Reason = dto.Reason?.Trim()
        };

        _context.ScheduleOverrides.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Leave saved successfully.", overrideId = entity.Id });
    }

    // =====================================================
    // UPDATE LEAVE
    // PUT /api/Schedule/overrides/leave/{id}
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpPut("overrides/leave/{id:int}")]
    public async Task<IActionResult> UpdateLeave(int id, [FromBody] LeaveScheduleOverrideDto request)
    {
        var entity = await _context.ScheduleOverrides.FirstOrDefaultAsync(x =>
            x.Id == id && x.HospitalId == GetHospitalId() && x.IsActive && x.OverrideType == "Leave");
        if (entity == null) return NotFound(new { message = "Leave override not found." });
        if (User.IsInRole("Doctor") && entity.DoctorId != GetDoctorId()) return Forbid();

        var resolved = await ResolveDoctorAsync(entity.DoctorId);
        if (resolved.error != null) return resolved.error;

        var dto = new ScheduleOverrideDto
        {
            DoctorId = entity.DoctorId,
            BranchId = request.BranchId,
            Date = request.Date,
            OverrideType = "Leave",
            Reason = request.Reason
        };

        var validation = await ValidateOverrideAsync(dto, resolved.doctor!, id);
        if (validation != null) return validation;

        entity.BranchId = dto.BranchId;
        entity.Date = dto.Date.Date;
        entity.Reason = dto.Reason?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Leave updated successfully.", overrideId = entity.Id });
    }

    // =====================================================
    // DELETE LEAVE
    // DELETE /api/Schedule/overrides/leave/{id}
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpDelete("overrides/leave/{id:int}")]
    public async Task<IActionResult> DeleteLeave(int id)
    {
        var entity = await _context.ScheduleOverrides.FirstOrDefaultAsync(x =>
            x.Id == id && x.HospitalId == GetHospitalId() && x.IsActive && x.OverrideType == "Leave");
        if (entity == null) return NotFound(new { message = "Leave override not found." });
        if (User.IsInRole("Doctor") && entity.DoctorId != GetDoctorId()) return Forbid();

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Leave removed successfully.", overrideId = entity.Id });
    }

    // =====================================================
    // CREATE TIME CHANGE
    // POST /api/Schedule/overrides/time-change
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpPost("overrides/time-change")]
    public async Task<IActionResult> CreateTimeChange([FromBody] TimeChangeScheduleOverrideDto request)
    {
        var dto = new ScheduleOverrideDto
        {
            DoctorId = request.DoctorId,
            BranchId = request.BranchId,
            Date = request.Date,
            OverrideType = "TimeChange",
            WorkStart = request.WorkStart,
            WorkEnd = request.WorkEnd,
            BreakStart = request.BreakStart,
            BreakEnd = request.BreakEnd,
            Reason = request.Reason
        };

        var resolved = await ResolveDoctorAsync(dto.DoctorId);
        if (resolved.error != null) return resolved.error;
        var doctor = resolved.doctor!;
        dto.DoctorId = doctor.Id;

        var validation = await ValidateOverrideAsync(dto, doctor);
        if (validation != null) return validation;

        ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var ws);
        ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var we);
        TimeSpan? bs = ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var bsv) ? bsv : null;
        TimeSpan? be = ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var bev) ? bev : null;

        var entity = new ScheduleOverride
        {
            HospitalId = GetHospitalId(),
            DoctorId = doctor.Id,
            BranchId = dto.BranchId,
            Date = dto.Date.Date,
            OverrideType = "TimeChange",
            WorkStart = ws,
            WorkEnd = we,
            BreakStart = bs,
            BreakEnd = be,
            Reason = dto.Reason?.Trim()
        };

        _context.ScheduleOverrides.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Time change saved successfully.", overrideId = entity.Id });
    }

    // =====================================================
    // CREATE BRANCH SHIFT
    // POST /api/Schedule/overrides/branch-shift
    // =====================================================
    [Authorize(Roles = "Admin,Doctor")]
    [HttpPost("overrides/branch-shift")]
    public async Task<IActionResult> CreateBranchShift([FromBody] BranchShiftScheduleOverrideDto request)
    {
        var dto = new ScheduleOverrideDto
        {
            DoctorId = request.DoctorId,
            SourceBranchId = request.SourceBranchId,
            BranchId = request.TargetBranchId,
            Date = request.Date,
            OverrideType = "BranchShift",
            WorkStart = request.WorkStart,
            WorkEnd = request.WorkEnd,
            BreakStart = request.BreakStart,
            BreakEnd = request.BreakEnd,
            Reason = request.Reason
        };

        var resolved = await ResolveDoctorAsync(dto.DoctorId);
        if (resolved.error != null) return resolved.error;
        var doctor = resolved.doctor!;
        dto.DoctorId = doctor.Id;

        var validation = await ValidateOverrideAsync(dto, doctor);
        if (validation != null) return validation;

        ScheduleAvailabilityHelper.TryParseTime(dto.WorkStart, out var ws);
        ScheduleAvailabilityHelper.TryParseTime(dto.WorkEnd, out var we);
        TimeSpan? bs = ScheduleAvailabilityHelper.TryParseTime(dto.BreakStart, out var bsv) ? bsv : null;
        TimeSpan? be = ScheduleAvailabilityHelper.TryParseTime(dto.BreakEnd, out var bev) ? bev : null;

        var entity = new ScheduleOverride
        {
            HospitalId = GetHospitalId(),
            DoctorId = doctor.Id,
            SourceBranchId = dto.SourceBranchId,
            BranchId = dto.BranchId,
            Date = dto.Date.Date,
            OverrideType = "BranchShift",
            WorkStart = ws,
            WorkEnd = we,
            BreakStart = bs,
            BreakEnd = be,
            Reason = dto.Reason?.Trim()
        };

        _context.ScheduleOverrides.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "Branch shift saved successfully.",
            overrideId = entity.Id,
            sourceBranchId = entity.SourceBranchId,
            targetBranchId = entity.BranchId
        });
    }

    [Authorize(Roles = "Admin,Doctor")]
    [HttpDelete("overrides/{id:int}")]
    public async Task<IActionResult> DeleteOverride(int id)
    {
        var entity = await _context.ScheduleOverrides.FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == GetHospitalId() && x.IsActive);
        if (entity == null) return NotFound(new { message = "Schedule override not found." });
        if (User.IsInRole("Doctor") && entity.DoctorId != GetDoctorId()) return Forbid();
        entity.IsActive = false; entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Schedule override removed successfully." });
    }

    // =====================================================
    // EFFECTIVE SLOTS (base schedule + leave/time/branch shift + bookings)
    // =====================================================
    [HttpGet("day-slots")]
    public async Task<IActionResult> GetSlots([FromQuery] int doctorId, [FromQuery] int branchId, [FromQuery] DateTime date)
    {
        var hospitalId = GetHospitalId();
        if (hospitalId <= 0) return Unauthorized(new { message = "Invalid HospitalId in authentication token." });
        var resolved = await ResolveDoctorAsync(doctorId);
        if (resolved.error != null) return resolved.error;
        doctorId = resolved.doctor!.Id;
        if (branchId <= 0) return BadRequest(new { message = "Invalid branchId." });

        var holiday = await _context.Holidays.AsNoTracking().FirstOrDefaultAsync(x => x.HospitalId == hospitalId && x.Date.Date == date.Date);
        if (holiday != null)
            return Ok(new { isHoliday = true, doctorId, branchId, date = date.ToString("yyyy-MM-dd"), message = $"Clinic Holiday - {holiday.Name}", slots = Array.Empty<object>() });

        var windows = await ScheduleAvailabilityHelper.GetEffectiveWindowsAsync(_context, hospitalId, doctorId, date);
        var branchWindows = windows.Where(x => x.BranchId == branchId).OrderBy(x => x.Start).ToList();
        if (branchWindows.Count == 0)
            return Ok(new { isHoliday = false, doctorId, branchId, date = date.ToString("yyyy-MM-dd"), message = "Doctor is not available in this branch on the selected date.", slots = Array.Empty<object>() });

        var setting = await _context.ScheduleSettings.AsNoTracking().Where(x => x.HospitalId == hospitalId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        var slotDuration = setting?.SlotDuration ?? 30;
        if (slotDuration <= 0) slotDuration = 30;
        var bookings = await _context.Appointments.AsNoTracking().Where(x =>
            x.HospitalId == hospitalId && x.DoctorId == doctorId && x.Date.Date == date.Date && x.Status != "Cancelled").ToListAsync();

        var result = new List<object>();
        foreach (var window in branchWindows)
        {
            var current = window.Start;
            while (current.Add(TimeSpan.FromMinutes(slotDuration)) <= window.End)
            {
                var end = current.Add(TimeSpan.FromMinutes(slotDuration));
                if (window.BreakStart.HasValue && window.BreakEnd.HasValue &&
                    ScheduleAvailabilityHelper.TimeRangesOverlap(current, end, window.BreakStart.Value, window.BreakEnd.Value))
                {
                    current = window.BreakEnd.Value;
                    continue;
                }
                if (date.Date == DateTime.Today && date.Date.Add(current) <= DateTime.Now)
                {
                    current = end; continue;
                }
                var booked = bookings.Any(x => x.StartTime == current);
                result.Add(new
                {
                    start = date.Date.Add(current).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    end = date.Date.Add(end).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                    status = booked ? "Booked" : "Available",
                    isBooked = booked,
                    branchId,
                    source = window.Source,
                    scheduleId = window.ScheduleId,
                    overrideId = window.OverrideId
                });
                current = end;
            }
        }

        return Ok(new
        {
            isHoliday = false,
            doctorId,
            branchId,
            date = date.ToString("yyyy-MM-dd"),
            slotDuration,
            windows = branchWindows.Select(x => new
            {
                start = DateTime.Today.Add(x.Start).ToString("hh:mm tt"),
                end = DateTime.Today.Add(x.End).ToString("hh:mm tt"),
                source = x.Source,
                x.OverrideId
            }),
            slots = result
        });
    }
}
