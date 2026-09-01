using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AuthDemo.Helpers;

public sealed record DoctorAvailabilityWindow(
    int BranchId,
    TimeSpan Start,
    TimeSpan End,
    TimeSpan? BreakStart,
    TimeSpan? BreakEnd,
    string Source,
    int? ScheduleId = null,
    int? OverrideId = null);

public static class ScheduleAvailabilityHelper
{
    public static bool TryParseTime(string? input, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var formats = new[] { "hh:mm tt", "h:mm tt", "HH:mm", "H:mm", "hh:mm:ss tt", "h:mm:ss tt" };
        if (!DateTime.TryParseExact(input.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed)) return false;
        time = parsed.TimeOfDay;
        return true;
    }

    public static bool TimeRangesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        => aStart < bEnd && bStart < aEnd;

    public static async Task<List<DoctorAvailabilityWindow>> GetEffectiveWindowsAsync(
        AppDbContext context,
        int hospitalId,
        int doctorId,
        DateTime date)
    {
        date = date.Date;
        var day = date.DayOfWeek.ToString();

        var baseSchedules = await context.Schedules.AsNoTracking()
            .Where(s => s.HospitalId == hospitalId && s.DoctorId == doctorId &&
                        date >= s.StartDate.Date && date <= s.EndDate.Date)
            .ToListAsync();

        var windows = baseSchedules
            .Where(s => (s.Days ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(day, StringComparer.OrdinalIgnoreCase) && s.BranchId.HasValue)
            .Select(s => new DoctorAvailabilityWindow(
                s.BranchId!.Value, s.WorkStart, s.WorkEnd, s.BreakStart, s.BreakEnd, "Schedule", s.Id, null))
            .ToList();

        var overrides = await context.ScheduleOverrides.AsNoTracking()
            .Where(o => o.HospitalId == hospitalId && o.DoctorId == doctorId &&
                        o.Date.Date == date && o.IsActive)
            .OrderBy(o => o.Id)
            .ToListAsync();

        // Branch-specific leave removes that branch; BranchId NULL means full-day leave.
        foreach (var leave in overrides.Where(o => o.OverrideType == "Leave"))
        {
            if (leave.BranchId.HasValue)
                windows.RemoveAll(w => w.BranchId == leave.BranchId.Value);
            else
                windows.Clear();
        }

        // A time change replaces the normal window for that branch for this date.
        foreach (var change in overrides.Where(o => o.OverrideType == "TimeChange" && o.BranchId.HasValue))
        {
            windows.RemoveAll(w => w.BranchId == change.BranchId.Value);
            if (change.WorkStart.HasValue && change.WorkEnd.HasValue)
            {
                windows.Add(new DoctorAvailabilityWindow(
                    change.BranchId.Value,
                    change.WorkStart.Value,
                    change.WorkEnd.Value,
                    change.BreakStart,
                    change.BreakEnd,
                    "TimeChange",
                    null,
                    change.Id));
            }
        }

        // BranchShift removes the shifted interval from the source branch and adds it to target branch.
        foreach (var shift in overrides.Where(o => o.OverrideType == "BranchShift" && o.BranchId.HasValue &&
                                                    o.SourceBranchId.HasValue && o.WorkStart.HasValue && o.WorkEnd.HasValue))
        {
            var sourceId = shift.SourceBranchId!.Value;
            var start = shift.WorkStart!.Value;
            var end = shift.WorkEnd!.Value;
            var sourceWindows = windows.Where(w => w.BranchId == sourceId).ToList();
            windows.RemoveAll(w => w.BranchId == sourceId);

            foreach (var w in sourceWindows)
            {
                if (!TimeRangesOverlap(w.Start, w.End, start, end))
                {
                    windows.Add(w);
                    continue;
                }
                if (w.Start < start)
                    windows.Add(w with { End = start, BreakStart = null, BreakEnd = null, Source = "ScheduleAfterShift" });
                if (end < w.End)
                    windows.Add(w with { Start = end, BreakStart = null, BreakEnd = null, Source = "ScheduleAfterShift" });
            }

            windows.Add(new DoctorAvailabilityWindow(
                shift.BranchId.Value, start, end, shift.BreakStart, shift.BreakEnd,
                "BranchShift", null, shift.Id));
        }

        return windows.OrderBy(w => w.Start).ThenBy(w => w.BranchId).ToList();
    }

    public static async Task<(bool ok, string message)> ValidateBookableSlotAsync(
        AppDbContext context,
        int hospitalId,
        int doctorId,
        int branchId,
        DateTime date,
        TimeSpan startTime,
        int slotDuration)
    {
        if (date.Date.Add(startTime) <= DateTime.Now)
            return (false, "Selected appointment time has already passed.");

        var windows = await GetEffectiveWindowsAsync(context, hospitalId, doctorId, date);
        var endTime = startTime.Add(TimeSpan.FromMinutes(Math.Max(1, slotDuration)));
        var matching = windows.FirstOrDefault(w =>
            w.BranchId == branchId && startTime >= w.Start && endTime <= w.End &&
            !(w.BreakStart.HasValue && w.BreakEnd.HasValue && TimeRangesOverlap(startTime, endTime, w.BreakStart.Value, w.BreakEnd.Value)));

        if (matching == null)
            return (false, "Doctor is not available in the selected branch for this slot. The schedule may have been changed, shifted to another branch, or marked as leave.");

        var booked = await context.Appointments.AnyAsync(x =>
            x.HospitalId == hospitalId && x.DoctorId == doctorId && x.Date.Date == date.Date &&
            x.StartTime == startTime && x.Status != "Cancelled");
        if (booked) return (false, "Selected slot is already booked.");

        return (true, string.Empty);
    }
}
