using AuthDemo.Helpers;
using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using AuthDemo.Data;
using AuthDemo.DTOs;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DashboardController
    : ControllerBase
{
    private readonly AppDbContext
        _context;

    public DashboardController(
        AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET HOSPITAL ID
    // =====================================================

    private int GetHospitalId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "HospitalId"
            );

        if (claim == null)
        {
            return 0;
        }

        return int.Parse(
            claim.Value
        );
    }

    // =====================================================
    // BUILD LOCATION
    // =====================================================

    private static string? BuildLocation(params string?[] parts)
    {
        var validParts = parts
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        return validParts.Count == 0
            ? null
            : string.Join(", ", validParts);
    }

    // =====================================================
    // ADMIN DASHBOARD
    // =====================================================

    [HttpGet]
    public async Task<IActionResult>
        GetDashboard()
    {
        var hospitalId =
            GetHospitalId();

        var today =
            DateTime.Today;

        // =================================================
        // TOTAL DOCTORS
        // =================================================

        var totalDoctors =
            await _context.Doctors

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId
                );
        // =================================================
        // TOTAL BRANCHES
        // =================================================

        var totalBranches =
            await _context.Branches
                .CountAsync(x =>
                    x.HospitalId == hospitalId &&
                    x.IsActive);

        // =================================================
        // TOTAL PATIENTS
        // =================================================

        var totalPatients =
            await _context.Patients

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId
                );

        // =================================================
        // TOTAL APPOINTMENTS
        // =================================================

        var totalAppointments =
            await _context.Appointments

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId
                );

        // =================================================
        // TODAY APPOINTMENTS
        // =================================================

        var todayAppointments =
            await _context.Appointments

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.Date.Date ==
                    today
                );

        // =================================================
        // COMPLETED APPOINTMENTS
        // =================================================

        var completedAppointments =
            await _context.Appointments

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.Status ==
                    "Completed"
                );

        // =================================================
        // WAITING APPOINTMENTS
        // =================================================

        var waitingAppointments =
            await _context.Appointments

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.Status ==
                    "Waiting"
                );

        // =================================================
        // TOTAL REVENUE
        // =================================================

        var paidBillsQuery = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Paid");

        var totalRevenue = await paidBillsQuery
            .SumAsync(x => x.TotalAmount);

        var opRevenue = await paidBillsQuery
            .Where(x => x.BillingType == "OP")
            .SumAsync(x => x.TotalAmount);

        var labRevenue = await paidBillsQuery
            .Where(x => x.BillingType == "Lab")
            .SumAsync(x => x.TotalAmount);

        var pharmacyRevenue = await paidBillsQuery
            .Where(x => x.BillingType == "Pharmacy")
            .SumAsync(x => x.TotalAmount);

        var pendingAmount = await _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Pending")
            .SumAsync(x => x.TotalAmount);

        var branchBillingSummary = await paidBillsQuery
            .GroupBy(x => x.BranchId)
            .Select(g => new
            {
                BranchId = g.Key,
                OverallRevenue = g.Sum(x => x.TotalAmount),
                OpRevenue = g
                    .Where(x => x.BillingType == "OP")
                    .Sum(x => x.TotalAmount),
                LabRevenue = g
                    .Where(x => x.BillingType == "Lab")
                    .Sum(x => x.TotalAmount),
                PharmacyRevenue = g
                    .Where(x => x.BillingType == "Pharmacy")
                    .Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var hospitalBranches = await _context.Branches
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsActive
            })
            .ToListAsync();

        var branchRevenue = hospitalBranches
            .Select(branch =>
            {
                var summary = branchBillingSummary
                    .FirstOrDefault(x => x.BranchId == branch.Id);

                return new
                {
                    branchId = branch.Id,
                    branchName = branch.Name,
                    isActive = branch.IsActive,
                    overallRevenue = summary?.OverallRevenue ?? 0m,
                    opRevenue = summary?.OpRevenue ?? 0m,
                    labRevenue = summary?.LabRevenue ?? 0m,
                    pharmacyRevenue = summary?.PharmacyRevenue ?? 0m
                };
            })
            .OrderByDescending(x => x.overallRevenue)
            .ToList();
        // =================================================
        // GROWTH CHART
        // =================================================

        var patientData =
            await _context.Patients

                .Where(x =>
                    x.HospitalId ==
                    hospitalId
                )

                .GroupBy(x =>
                    new
                    {
                        x.CreatedAt.Year,
                        x.CreatedAt.Month
                    })

                .Select(g =>
                    new
                    {
                        g.Key.Year,
                        g.Key.Month,

                        Patients =
                            g.Count()
                    })

                .ToListAsync();

        var appointmentData =
            await _context.Appointments

                .Where(x =>
                    x.HospitalId ==
                    hospitalId
                )

                .GroupBy(x =>
                    new
                    {
                        x.CreatedAt.Year,
                        x.CreatedAt.Month
                    })

                .Select(g =>
                    new
                    {
                        g.Key.Year,
                        g.Key.Month,

                        Appointments =
                            g.Count()
                    })

                .ToListAsync();

        // =================================================
        // MERGE MONTHS
        // =================================================

        var allMonths =
            patientData

                .Select(x =>
                    new
                    {
                        x.Year,
                        x.Month
                    })

                .Union(
                    appointmentData
                        .Select(x =>
                            new
                            {
                                x.Year,
                                x.Month
                            })
                )

                .Distinct()

                .OrderBy(x =>
                    x.Year
                )

                .ThenBy(x =>
                    x.Month
                )

                .ToList();

        // =================================================
        // FINAL GROWTH CHART
        // =================================================

        var growthChart =
            allMonths

                .Select(m =>
                {
                    var patient =
                        patientData
                            .FirstOrDefault(x =>

                                x.Year ==
                                m.Year &&

                                x.Month ==
                                m.Month
                            );

                    var appointment =
                        appointmentData
                            .FirstOrDefault(x =>

                                x.Year ==
                                m.Year &&

                                x.Month ==
                                m.Month
                            );

                    return new ChartDto
                    {
                        Month =
                            new DateTime(
                                m.Year,
                                m.Month,
                                1
                            ).ToString("MMM"),

                        Patients =
                            patient?.Patients
                            ?? 0,

                        Appointments =
                            appointment?.Appointments
                            ?? 0
                    };
                })

                .ToList();

        // =================================================
        // REVENUE TREND
        // =================================================
        var revenueData =
    await _context.Billings

        .Where(x =>

            x.HospitalId ==
            hospitalId &&

            x.Status ==
            "Paid"
        )

        .GroupBy(x =>
            new
            {
                x.CreatedAt.Year,
                x.CreatedAt.Month
            })

        .Select(g =>
            new
            {
                g.Key.Year,
                g.Key.Month,

                Revenue =
                    g.Sum(x =>
                        x.TotalAmount)
            })

        .OrderBy(x =>
            x.Year)

        .ThenBy(x =>
            x.Month)

        .ToListAsync();
        var revenueTrend =
    revenueData
        .Select(r =>
            new RevenueTrendDto
            {
                Month =
                    new DateTime(
                        r.Year,
                        r.Month,
                        1
                    ).ToString("MMM"),

                Revenue =
                    r.Revenue
            })
        .ToList();

        // =================================================
        // DOCTOR REPORTS
        // =================================================

        var doctorReports =
            await _context.Appointments

                .Include(x =>
                    x.Doctor)

                .Where(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.Status ==
                    "Completed"
                )

                .GroupBy(x => new
                {
                    x.DoctorId,

                    doctorName =
                        x.Doctor == null
                            ? ""
                            : x.Doctor.Name,

                    specialization =
                        x.Doctor == null
                            ? ""
                            : x.Doctor.Specialization
                })

                .Select(g =>
                    new
                    {
                        doctorId =
                            g.Key.DoctorId,

                        doctorName =
                            g.Key.doctorName,

                        specialization =
                            g.Key.specialization,

                        completedAppointments =
                            g.Count(),

                        revenue =
                            g.Sum(x =>
                                x.Doctor == null
                                    ? 0
                                    : x.Doctor.Fees)
                    })

                .OrderByDescending(x =>
                    x.completedAppointments)

                .ToListAsync();

        // =================================================
        // CLINIC STATUS
        // =================================================

        var availableDoctors =
            await _context.Doctors

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.IsActive
                );

        var onLeaveDoctors =
            await _context.Doctors

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    !x.IsActive
                );

        var busyDoctors =
            await _context.Appointments

                .CountAsync(x =>

                    x.HospitalId ==
                    hospitalId &&

                    x.Date.Date ==
                    today &&

                    x.Status ==
                    "InProgress"
                );

        var clinicStatus =
            new ClinicStatusDto
            {
                Available =
                    availableDoctors,

                Busy =
                    busyDoctors,

                OnLeave =
                    onLeaveDoctors
            };

        // =================================================
        // RECENT ACTIVITIES
        // =================================================

        var recentAppointments =
            await _context.Appointments

                .Include(x =>
                    x.Patient)

                .Include(x =>
                    x.Doctor)

                .Where(x =>
                    x.HospitalId ==
                    hospitalId
                )

                .OrderByDescending(x =>
                    x.CreatedAt
                )

                .Take(5)

                .ToListAsync();

        var recentActivities =
            recentAppointments

                .Select(x =>
                    new ActivityDto
                    {
                        Title =
                            $"{x.Patient.Name} booked appointment with Dr. {x.Doctor.Name}",

                        Time =
                            x.CreatedAt
                                .ToString(
                                    "dd MMM yyyy hh:mm tt"
                                )
                    })

                .ToList();

        // =================================================
        // FINAL RESPONSE
        // =================================================

        var dashboard =
            new
            {
                totalBranches,
                totalDoctors,
                totalPatients,
                totalAppointments,
                todayAppointments,
                completedAppointments,
                waitingAppointments,
                totalRevenue,
                overallRevenue = totalRevenue,
                opRevenue,
                labRevenue,
                pharmacyRevenue,
                pendingAmount,
                branchRevenue,
                growthChart,
                revenueTrend,
                clinicStatus,
                doctorReports,
                recentActivities
            };

        return Ok(dashboard);
    }
    // =====================================================
    // ADMIN REVENUE SUMMARY
    // Clinic total, billing-type breakdown and branch breakdown
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] int? branchId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "FromDate cannot be later than ToDate."
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your clinic."
                });
            }
        }

        IQueryable<AuthDemo.Models.Billing> query = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Paid");

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < until);
        }

        var overallRevenue = await query.SumAsync(x => x.TotalAmount);

        var opRevenue = await query
            .Where(x => x.BillingType == "OP")
            .SumAsync(x => x.TotalAmount);

        var labRevenue = await query
            .Where(x => x.BillingType == "Lab")
            .SumAsync(x => x.TotalAmount);

        var pharmacyRevenue = await query
            .Where(x => x.BillingType == "Pharmacy")
            .SumAsync(x => x.TotalAmount);

        IQueryable<AuthDemo.Models.Billing> pendingQuery = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Pending");

        if (branchId.HasValue)
        {
            pendingQuery = pendingQuery.Where(x =>
                x.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            pendingQuery = pendingQuery.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            pendingQuery = pendingQuery.Where(x => x.CreatedAt < until);
        }

        var pendingAmount = await pendingQuery
            .SumAsync(x => x.TotalAmount);

        var grouped = await query
            .GroupBy(x => x.BranchId)
            .Select(g => new
            {
                BranchId = g.Key,
                OverallRevenue = g.Sum(x => x.TotalAmount),
                OpRevenue = g
                    .Where(x => x.BillingType == "OP")
                    .Sum(x => x.TotalAmount),
                LabRevenue = g
                    .Where(x => x.BillingType == "Lab")
                    .Sum(x => x.TotalAmount),
                PharmacyRevenue = g
                    .Where(x => x.BillingType == "Pharmacy")
                    .Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var branchesQuery = _context.Branches
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId);

        if (branchId.HasValue)
        {
            branchesQuery = branchesQuery.Where(x => x.Id == branchId.Value);
        }

        var branches = await branchesQuery
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync();

        var branchRevenue = branches
            .Select(branch =>
            {
                var summary = grouped
                    .FirstOrDefault(x => x.BranchId == branch.Id);

                return new
                {
                    branchId = branch.Id,
                    branchName = branch.Name,
                    overallRevenue = summary?.OverallRevenue ?? 0m,
                    opRevenue = summary?.OpRevenue ?? 0m,
                    labRevenue = summary?.LabRevenue ?? 0m,
                    pharmacyRevenue = summary?.PharmacyRevenue ?? 0m
                };
            })
            .OrderByDescending(x => x.overallRevenue)
            .ToList();

        return Ok(new
        {
            hospitalId,
            branchId,
            fromDate = fromDate?.Date,
            toDate = toDate?.Date,
            overallRevenue,
            opRevenue,
            labRevenue,
            pharmacyRevenue,
            pendingAmount,
            branchRevenue
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("ClincData")]
    public async Task<IActionResult> Dashboard()
    {
        var hospitalId = GetHospitalId();

        var clinic = await _context.Hospitals
            .FirstOrDefaultAsync(x => x.Id == hospitalId);

        if (clinic == null)
        {
            return NotFound(new
            {
                message = "Clinic not found"
            });
        }

        return Ok(new
        {
            clinicName = clinic.Name,
            contactNumber = clinic.Phone,
            email = clinic.Email,
            status = clinic.IsActive ? "Active" : "Inactive",
            address = clinic.Address
        });
    }
    // =====================================================
    // BRANCH DASHBOARD
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("branch/{branchId}")]
    public async Task<IActionResult> BranchDashboard(int branchId)
    {
        var hospitalId = GetHospitalId();

        var branch = await _context.Branches
            .FirstOrDefaultAsync(x =>
                x.Id == branchId &&
                x.HospitalId == hospitalId);

        if (branch == null)
        {
            return NotFound(new
            {
                message = "Branch not found"
            });
        }

        var totalDoctors = await _context.Doctors
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId);

        var totalStaff = await _context.Staffs
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId);

        var totalReceptionists = await _context.Receptionists
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId);

        var today = DateTime.Today;

        var todayAppointments = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Date.Date == today);

        var waitingAppointments = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Status == "Waiting");

        var completedAppointments = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Status == "Completed");

        var cancelledAppointments = await _context.Appointments
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Status == "Cancelled");

        var branchPaidBills = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Status == "Paid");

        var overallRevenue = await branchPaidBills
            .SumAsync(x => x.TotalAmount);

        var opRevenue = await branchPaidBills
            .Where(x => x.BillingType == "OP")
            .SumAsync(x => x.TotalAmount);

        var labRevenue = await branchPaidBills
            .Where(x => x.BillingType == "Lab")
            .SumAsync(x => x.TotalAmount);

        var pharmacyRevenue = await branchPaidBills
            .Where(x => x.BillingType == "Pharmacy")
            .SumAsync(x => x.TotalAmount);

        var pendingAmount = await _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Status == "Pending")
            .SumAsync(x => x.TotalAmount);

        return Ok(new
        {
            branchId,
            branchName = branch.Name,
            totalDoctors,
            totalStaff,
            totalReceptionists,
            todayAppointments,
            waitingAppointments,
            completedAppointments,
            cancelledAppointments,
            overallRevenue,
            opRevenue,
            labRevenue,
            pharmacyRevenue,
            pendingAmount
        });
    }
    // =====================================================
    // BRANCH DASHBOARD FILTER
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("branch/{branchId}/filter")]
    public async Task<IActionResult> BranchDashboardFilter(
        int branchId,
        DateTime from,
        DateTime to)
    {
        var hospitalId = GetHospitalId();

        var appointments = await _context.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Patient)
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Date.Date >= from.Date &&
                x.Date.Date <= to.Date)
            .Select(x => new
            {
                appointmentId = x.Id,
                token = x.TokenNumber,
                patient = x.Patient.Name,
                doctor = x.Doctor.Name,
                date = x.Date,
                time = x.Date.Date
                .Add(x.StartTime)
                .ToString("hh:mm tt"),
                status = x.Status
            })
            .ToListAsync();

        var totalAppointments = appointments.Count;

        var completed = appointments.Count(x => x.status == "Completed");

        var cancelled = appointments.Count(x => x.status == "Cancelled");

        var waiting = appointments.Count(x => x.status == "Waiting");

        return Ok(new
        {
            totalAppointments,
            completed,
            cancelled,
            waiting,
            appointments
        });
    }
    // =====================================================
    // DOCTORS BY BRANCH
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("branch/{branchId}/doctors")]
    public async Task<IActionResult> DoctorsByBranch(int branchId)
    {
        var hospitalId = GetHospitalId();

        var doctors = await _context.Doctors
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId)
            .Select(x => new
            {
                doctorId = x.Id,
                name = x.Name,
                specialization = x.Specialization,
                qualification = x.Qualification,
                fees = x.Fees,
                phone = x.Phone,
                email = x.Email,
                status = x.IsActive ? "Active" : "Inactive"
            })
            .ToListAsync();

        return Ok(doctors);
    }
    // =====================================================
    // TODAY APPOINTMENTS BY BRANCH
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("branch/{branchId}/today")]
    public async Task<IActionResult> TodayAppointmentsByBranch(int branchId)
    {
        var hospitalId = GetHospitalId();

        var today = DateTime.Today;

        var appointments = await _context.Appointments
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId &&
                x.Date.Date == today)
            .OrderBy(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                token = x.TokenNumber,
                patient = x.Patient.Name,
                doctor = x.Doctor.Name,
                phone = x.Patient.Phone,
                time = x.StartTime,
                status = x.Status
            })
            .ToListAsync();

        return Ok(new
        {
            totalAppointments = appointments.Count,
            appointments
        });
    }

    // =====================================================
    // STAFF LOGIN DASHBOARD
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("staff-login-dashboard")]
    public async Task<IActionResult> StaffLoginDashboard()
    {
        var hospitalId = GetHospitalId();

        var totalStaff = await _context.Users
            .CountAsync(x =>
                x.HospitalId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist"));

        var onlineStaff = await _context.AuditLogs
            .CountAsync(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist") &&
                x.IsOnline);

        var todayLogins = await _context.AuditLogs
            .CountAsync(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist") &&
                x.LoginTime.Date == DateTime.UtcNow.Date);

        var lastLogin = await _context.AuditLogs
            .Where(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist"))
            .OrderByDescending(x => x.LoginTime)
            .FirstOrDefaultAsync();

        return Ok(new LoginDashboardDto
        {
            TotalUsers = totalStaff,
            OnlineUsers = onlineStaff,
            TodayLogins = todayLogins,
            LastLoginUser = lastLogin?.UserName,
            LastLoginTime = lastLogin?.LoginTime
        });
    }
    // =====================================================
    // STAFF LOGIN TREND
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("staff-login-trend")]
    public async Task<IActionResult> StaffLoginTrend()
    {
        var hospitalId = GetHospitalId();

        var trend = await _context.AuditLogs
            .Where(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist") &&
                x.LoginTime >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(x => x.LoginTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return Ok(trend);
    }
    // =====================================================
    // ONLINE STAFF WITH HOSPITAL AND BRANCH DETAILS
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("online-staff")]
    public async Task<IActionResult> OnlineStaff(
        [FromQuery] int? branchId = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token"
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your hospital"
                });
            }
        }

        var auditQuery = _context.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.ClinicId == hospitalId &&
                (x.Role == "Doctor" ||
                 x.Role == "Receptionist") &&
                x.IsOnline);

        var auditLogs = await auditQuery
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Role,
                x.ClinicId,
                x.BranchId,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.LoginTime,
                x.LogoutTime,
                x.IsOnline
            })
            .ToListAsync();

        var hospital = await _context.Hospitals
            .AsNoTracking()
            .Where(x => x.Id == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.City,
                x.District,
                x.State,
                x.Country,
                x.PostalCode
            })
            .FirstOrDefaultAsync();

        if (hospital == null)
        {
            return NotFound(new
            {
                message = "Hospital not found"
            });
        }

        var userIds = auditLogs
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var loginUsers = await _context.Users
            .AsNoTracking()
            .Where(x =>
                userIds.Contains(x.Id) &&
                x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.BranchId
            })
            .ToListAsync();

        var branchIds = auditLogs
            .Select(log =>
            {
                if (log.BranchId.HasValue)
                {
                    return log.BranchId;
                }

                var user = loginUsers
                    .FirstOrDefault(x => x.Id == log.UserId);

                return user?.BranchId;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(x =>
                x.Id.HasValue &&
                branchIds.Contains(x.Id.Value) &&
                x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.City,
                x.District,
                x.State,
                x.Country,
                x.PostalCode
            })
            .ToListAsync();

        var hospitalLocation = BuildLocation(
            hospital.Address,
            hospital.City,
            hospital.District,
            hospital.State,
            hospital.Country,
            hospital.PostalCode);

        var result = auditLogs
            .Select(log =>
            {
                var user = loginUsers
                    .FirstOrDefault(x => x.Id == log.UserId);

                var effectiveBranchId =
                    log.BranchId ?? user?.BranchId;

                var branch = effectiveBranchId.HasValue
                    ? branches.FirstOrDefault(x =>
                        x.Id == effectiveBranchId.Value)
                    : null;

                return new
                {
                    id = log.Id,
                    userId = log.UserId,
                    userName = log.UserName,
                    role = log.Role,

                    hospitalId = hospital.Id,
                    hospitalName = hospital.Name,
                    hospitalLocation,

                    branchId = effectiveBranchId,
                    branchName = branch?.Name ?? "Unassigned",
                    branchLocation = branch == null
                        ? null
                        : BuildLocation(
                            branch.Address,
                            branch.City,
                            branch.District,
                            branch.State,
                            branch.Country,
                            branch.PostalCode),

                    ipAddress = log.IpAddress,
                    browser = log.Browser,
                    device = log.Device,
                    loginTime = log.LoginTime,
                    logoutTime = log.LogoutTime,
                    isOnline = log.IsOnline
                };
            })
            .Where(x =>
                !branchId.HasValue ||
                x.branchId == branchId.Value)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // TODAY LOGIN HISTORY WITH HOSPITAL AND BRANCH DETAILS
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("today-logins")]
    public async Task<IActionResult> TodayLogins(
        [FromQuery] int? branchId = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token"
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your hospital"
                });
            }
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Fetch scalar AuditLog fields only. This avoids the EF Core
        // navigation-projection translation error that caused HTTP 500.
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.ClinicId == hospitalId &&
                x.LoginTime >= today &&
                x.LoginTime < tomorrow)
            .OrderByDescending(x => x.LoginTime)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.UserName,
                x.Role,
                x.ClinicId,
                x.BranchId,
                x.Action,
                x.SystemAction,
                x.IpAddress,
                x.Browser,
                x.Device,
                x.LoginTime,
                x.LogoutTime,
                x.IsOnline
            })
            .ToListAsync();

        var hospital = await _context.Hospitals
            .AsNoTracking()
            .Where(x => x.Id == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.City,
                x.District,
                x.State,
                x.Country,
                x.PostalCode
            })
            .FirstOrDefaultAsync();

        if (hospital == null)
        {
            return NotFound(new
            {
                message = "Hospital not found"
            });
        }

        var userIds = auditLogs
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        // User.BranchId is used as a fallback for older audit records
        // where AuditLog.BranchId was not stored during login.
        var loginUsers = await _context.Users
            .AsNoTracking()
            .Where(x =>
                userIds.Contains(x.Id) &&
                x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.BranchId
            })
            .ToListAsync();

        var branchIds = auditLogs
            .Select(log =>
            {
                if (log.BranchId.HasValue)
                {
                    return log.BranchId;
                }

                var user = loginUsers
                    .FirstOrDefault(x => x.Id == log.UserId);

                return user?.BranchId;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(x =>
                x.Id.HasValue &&
                branchIds.Contains(x.Id.Value) &&
                x.HospitalId == hospitalId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Address,
                x.City,
                x.District,
                x.State,
                x.Country,
                x.PostalCode
            })
            .ToListAsync();

        var hospitalLocation = BuildLocation(
            hospital.Address,
            hospital.City,
            hospital.District,
            hospital.State,
            hospital.Country,
            hospital.PostalCode);

        var result = auditLogs
            .Select(log =>
            {
                var user = loginUsers
                    .FirstOrDefault(x => x.Id == log.UserId);

                var effectiveBranchId =
                    log.BranchId ?? user?.BranchId;

                var branch = effectiveBranchId.HasValue
                    ? branches.FirstOrDefault(x =>
                        x.Id == effectiveBranchId.Value)
                    : null;

                return new
                {
                    id = log.Id,
                    userId = log.UserId,
                    userName = log.UserName,
                    role = log.Role,

                    hospitalId = hospital.Id,
                    hospitalName = hospital.Name,
                    hospitalLocation,

                    branchId = effectiveBranchId,
                    branchName = branch?.Name ?? "Unassigned",
                    branchLocation = branch == null
                        ? null
                        : BuildLocation(
                            branch.Address,
                            branch.City,
                            branch.District,
                            branch.State,
                            branch.Country,
                            branch.PostalCode),

                    action = log.Action,
                    systemAction = log.SystemAction,
                    ipAddress = log.IpAddress,
                    browser = log.Browser,
                    device = log.Device,
                    loginTime = log.LoginTime,
                    logoutTime = log.LogoutTime,
                    isOnline = log.IsOnline
                };
            })
            .Where(x =>
                !branchId.HasValue ||
                x.branchId == branchId.Value)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // DAILY APPOINTMENTS REPORT
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("reports/daily-appointments")]
    public async Task<IActionResult> DailyAppointmentsReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? doctorId = null,
        [FromQuery] int? branchId = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "FromDate cannot be later than ToDate."
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your clinic."
                });
            }
        }

        if (doctorId.HasValue)
        {
            var doctorExists = await _context.Doctors
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == doctorId.Value &&
                    x.HospitalId == hospitalId);

            if (!doctorExists)
            {
                return NotFound(new
                {
                    message = "Doctor not found in your clinic."
                });
            }
        }

        var query = _context.Appointments
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId);

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.Date >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.Date < until);
        }

        if (doctorId.HasValue)
        {
            query = query.Where(x => x.DoctorId == doctorId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        var appointmentRows = await query
            .Select(x => new
            {
                x.Date,
                x.Status
            })
            .ToListAsync();

        var grouped = appointmentRows
            .GroupBy(x => x.Date.DayOfWeek)
            .Select(g => new
            {
                dayNumber = (int)g.Key,
                appointments = g.Count(),
                completed = g.Count(x => x.Status == "Completed")
            })
            .ToList();

        var week = Enumerable.Range(0, 7)
            .Select(dayNumber =>
            {
                var row = grouped.FirstOrDefault(x =>
                    x.dayNumber == dayNumber);

                return new
                {
                    dayNumber,
                    day = ((DayOfWeek)dayNumber).ToString().Substring(0, 3),
                    appointments = row?.appointments ?? 0,
                    completed = row?.completed ?? 0
                };
            })
            .OrderBy(x => x.dayNumber == 0 ? 7 : x.dayNumber)
            .Select((x, index) => new
            {
                sNo = index + 1,
                x.day,
                x.appointments,
                x.completed
            })
            .ToList();

        return Ok(week);
    }

    // =====================================================
    // BRANCH-WISE REVENUE REPORT
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("reports/revenue")]
    public async Task<IActionResult> RevenueReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? branchId = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "FromDate cannot be later than ToDate."
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your clinic."
                });
            }
        }

        var query = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Paid");

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < until);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        var grouped = await query
            .Where(x => x.BranchId.HasValue)
            .GroupBy(x => new
            {
                x.CreatedAt.Year,
                x.CreatedAt.Month,
                BranchId = x.BranchId!.Value
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.BranchId,
                opRevenue = g
                    .Where(x => x.BillingType == "OP")
                    .Sum(x => x.TotalAmount),
                diagnosticRevenue = g
                    .Where(x => x.BillingType == "Lab")
                    .Sum(x => x.TotalAmount),
                pharmacyRevenue = g
                    .Where(x => x.BillingType == "Pharmacy")
                    .Sum(x => x.TotalAmount),
                totalGst = g.Sum(x => x.GstAmount),
                totalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.BranchId)
            .ToListAsync();

        var branchIds = grouped
            .Select(x => x.BranchId)
            .Distinct()
            .ToList();

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                branchIds.Contains(x.Id!.Value))
            .Select(x => new
            {
                Id = x.Id!.Value,
                x.Name
            })
            .ToListAsync();

        var result = grouped
            .Select((x, index) =>
            {
                var branch = branches.FirstOrDefault(b =>
                    b.Id == x.BranchId);

                var cgst = Math.Round(x.totalGst / 2m, 2);
                var sgst = x.totalGst - cgst;

                return new
                {
                    sNo = index + 1,
                    month = new DateTime(x.Year, x.Month, 1)
                        .ToString("MMM yyyy"),
                    branchId = x.BranchId,
                    branch = branch?.Name ?? "Unknown",
                    x.opRevenue,
                    x.diagnosticRevenue,
                    x.pharmacyRevenue,
                    cgst,
                    sgst,
                    x.totalGst,
                    x.totalRevenue
                };
            })
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // DOCTOR-WISE REPORT
    // =====================================================

    [Authorize(Roles = "Admin")]
    [HttpGet("reports/doctors")]
    public async Task<IActionResult> DoctorWiseReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? doctorId = null,
        [FromQuery] int? branchId = null)
    {
        var hospitalId = GetHospitalId();

        if (hospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return BadRequest(new
            {
                message = "FromDate cannot be later than ToDate."
            });
        }

        if (branchId.HasValue)
        {
            var branchExists = await _context.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == branchId.Value &&
                    x.HospitalId == hospitalId);

            if (!branchExists)
            {
                return NotFound(new
                {
                    message = "Branch not found in your clinic."
                });
            }
        }

        if (doctorId.HasValue)
        {
            var doctorExists = await _context.Doctors
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == doctorId.Value &&
                    x.HospitalId == hospitalId);

            if (!doctorExists)
            {
                return NotFound(new
                {
                    message = "Doctor not found in your clinic."
                });
            }
        }

        var query = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Paid" &&
                x.BillingType == "OP");

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var until = toDate.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < until);
        }

        if (doctorId.HasValue)
        {
            query = query.Where(x => x.DoctorId == doctorId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        var grouped = await query
            .GroupBy(x => new
            {
                x.DoctorId,
                x.CreatedAt.Year,
                x.CreatedAt.Month
            })
            .Select(g => new
            {
                g.Key.DoctorId,
                g.Key.Year,
                g.Key.Month,
                opBills = g.Count(),
                opRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.DoctorId)
            .ToListAsync();

        var doctorIds = grouped
            .Select(x => x.DoctorId)
            .Distinct()
            .ToList();

        var doctors = await _context.Doctors
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                doctorIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Specialization
            })
            .ToListAsync();

        var result = grouped
            .Select((x, index) =>
            {
                var doctor = doctors.FirstOrDefault(d =>
                    d.Id == x.DoctorId);

                return new
                {
                    sNo = index + 1,
                    doctorId = x.DoctorId,
                    doctor = doctor?.Name ?? "Unknown",
                    specialization = doctor?.Specialization ?? "",
                    month = new DateTime(x.Year, x.Month, 1)
                        .ToString("MMM yyyy"),
                    x.opBills,
                    x.opRevenue
                };
            })
            .ToList();

        return Ok(result);
    }

}