using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SuperAdminReportsController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // SUMMARY
    // =====================================================

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var paidQuery = _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Paid");

        var pendingQuery = _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Pending");

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;

            paidQuery = paidQuery.Where(x =>
                x.CreatedAt >= from);

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);

            paidQuery = paidQuery.Where(x =>
                x.CreatedAt < toExclusive);

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt < toExclusive);
        }
        
         
        var totalRevenue = await paidQuery
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var opRevenue = await paidQuery
            .Where(x => x.BillingType == "OP")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var labRevenue = await paidQuery
            .Where(x => x.BillingType == "Lab")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var pharmacyRevenue = await paidQuery
            .Where(x => x.BillingType == "Pharmacy")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var pendingAmount = await pendingQuery
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var activeClinics = await _context.Hospitals
            .AsNoTracking()
            .CountAsync(x => x.IsActive);

        var totalClinics = await _context.Hospitals
            .AsNoTracking()
            .CountAsync();

        var activeAdmins = await _context.Users
            .AsNoTracking()
            .CountAsync(x =>
                x.Role == "Admin" &&
                x.IsActive);

        var totalAdmins = await _context.Users
            .AsNoTracking()
            .CountAsync(x =>
                x.Role == "Admin");

        var totalUsers = await _context.Users
            .AsNoTracking()
            .CountAsync();

        var totalPatients = await _context.Patients
            .AsNoTracking()
            .CountAsync();

        var totalAppointments = await _context.Appointments
            .AsNoTracking()
            .CountAsync();

        return Ok(new
        {
            totalRevenue,
            overallRevenue = totalRevenue,
            opRevenue,
            labRevenue,
            pharmacyRevenue,
            pendingAmount,

            totalClinics,
            activeClinics,
            totalAdmins,
            activeAdmins,
            totalUsers,
            totalPatients,
            totalAppointments,

            fromDate,
            toDate
        });
    }

    // =====================================================
    // ALL CLINICS REVENUE
    // =====================================================

    [HttpGet("clinic-revenue")]
    public async Task<IActionResult> ClinicRevenue(
        [FromQuery] int? hospitalId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Paid");

        if (hospitalId.HasValue)
        {
            var clinicExists = await _context.Hospitals
                .AsNoTracking()
                .AnyAsync(x => x.Id == hospitalId.Value);

            if (!clinicExists)
            {
                return NotFound(new
                {
                    message = "Clinic not found."
                });
            }

            query = query.Where(x =>
                x.HospitalId == hospitalId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;

            query = query.Where(x =>
                x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);

            query = query.Where(x =>
                x.CreatedAt < toExclusive);
        }

        var groupedRevenue = await query
            .GroupBy(x => x.HospitalId)
            .Select(g => new
            {
                hospitalId = g.Key,

                overallRevenue =
                    g.Sum(x => x.TotalAmount),

                opRevenue =
                    g.Where(x => x.BillingType == "OP")
                     .Sum(x => x.TotalAmount),

                labRevenue =
                    g.Where(x => x.BillingType == "Lab")
                     .Sum(x => x.TotalAmount),

                pharmacyRevenue =
                    g.Where(x => x.BillingType == "Pharmacy")
                     .Sum(x => x.TotalAmount),

                totalPaidBills = g.Count()
            })
            .ToListAsync();

        var pendingQuery = _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Pending");

        if (hospitalId.HasValue)
        {
            pendingQuery = pendingQuery.Where(x =>
                x.HospitalId == hospitalId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt < toExclusive);
        }

        var pendingByClinic = await pendingQuery
            .GroupBy(x => x.HospitalId)
            .Select(g => new
            {
                hospitalId = g.Key,
                pendingAmount =
                    g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var clinicsQuery = _context.Hospitals
            .AsNoTracking()
            .AsQueryable();

        if (hospitalId.HasValue)
        {
            clinicsQuery = clinicsQuery.Where(x =>
                x.Id == hospitalId.Value);
        }

        var clinics = await clinicsQuery
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsActive
            })
            .ToListAsync();

        var result = clinics.Select(clinic =>
        {
            var revenue = groupedRevenue.FirstOrDefault(x =>
                x.hospitalId == clinic.Id);

            var pending = pendingByClinic.FirstOrDefault(x =>
                x.hospitalId == clinic.Id);

            return new
            {
                hospitalId = clinic.Id,
                clinicName = clinic.Name,
                status = clinic.IsActive
                    ? "Active"
                    : "Inactive",

                overallRevenue =
                    revenue?.overallRevenue ?? 0,

                opRevenue =
                    revenue?.opRevenue ?? 0,

                labRevenue =
                    revenue?.labRevenue ?? 0,

                pharmacyRevenue =
                    revenue?.pharmacyRevenue ?? 0,

                pendingAmount =
                    pending?.pendingAmount ?? 0,

                totalPaidBills =
                    revenue?.totalPaidBills ?? 0
            };
        })
        .OrderByDescending(x => x.overallRevenue)
        .ToList();

        return Ok(new
        {
            allClinicsRevenue =
                result.Sum(x => x.overallRevenue),

            overallRevenue =
                result.Sum(x => x.overallRevenue),

            opRevenue =
                result.Sum(x => x.opRevenue),

            labRevenue =
                result.Sum(x => x.labRevenue),

            pharmacyRevenue =
                result.Sum(x => x.pharmacyRevenue),

            pendingAmount =
                result.Sum(x => x.pendingAmount),

            fromDate,
            toDate,
            clinics = result
        });
    }

    // =====================================================
    // ONE CLINIC REVENUE WITH BRANCH BREAKDOWN
    // =====================================================

    [HttpGet("clinic-revenue/{hospitalId:int}")]
    public async Task<IActionResult> ClinicRevenueById(
        int hospitalId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var clinic = await _context.Hospitals
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == hospitalId);

        if (clinic == null)
        {
            return NotFound(new
            {
                message = "Clinic not found."
            });
        }

        var paidQuery = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Paid");

        var pendingQuery = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.Status == "Pending");

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;

            paidQuery = paidQuery.Where(x =>
                x.CreatedAt >= from);

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);

            paidQuery = paidQuery.Where(x =>
                x.CreatedAt < toExclusive);

            pendingQuery = pendingQuery.Where(x =>
                x.CreatedAt < toExclusive);
        }

        var overallRevenue = await paidQuery
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var opRevenue = await paidQuery
            .Where(x => x.BillingType == "OP")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var labRevenue = await paidQuery
            .Where(x => x.BillingType == "Lab")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var pharmacyRevenue = await paidQuery
            .Where(x => x.BillingType == "Pharmacy")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var pendingAmount = await pendingQuery
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var branchRevenueData = await paidQuery
            .Where(x => x.BranchId.HasValue)
            .GroupBy(x => x.BranchId!.Value)
            .Select(g => new
            {
                branchId = g.Key,

                overallRevenue =
                    g.Sum(x => x.TotalAmount),

                opRevenue =
                    g.Where(x => x.BillingType == "OP")
                     .Sum(x => x.TotalAmount),

                labRevenue =
                    g.Where(x => x.BillingType == "Lab")
                     .Sum(x => x.TotalAmount),

                pharmacyRevenue =
                    g.Where(x => x.BillingType == "Pharmacy")
                     .Sum(x => x.TotalAmount),

                totalPaidBills = g.Count()
            })
            .ToListAsync();

        var branchPendingData = await pendingQuery
            .Where(x => x.BranchId.HasValue)
            .GroupBy(x => x.BranchId!.Value)
            .Select(g => new
            {
                branchId = g.Key,

                pendingAmount =
                    g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var branches = await _context.Branches
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsActive
            })
            .ToListAsync();

        var branchRevenue = branches.Select(branch =>
        {
            var revenue = branchRevenueData.FirstOrDefault(x =>
                x.branchId == branch.Id);

            var pending = branchPendingData.FirstOrDefault(x =>
                x.branchId == branch.Id);

            return new
            {
                branchId = branch.Id,
                branchName = branch.Name,

                status = branch.IsActive
                    ? "Active"
                    : "Inactive",

                overallRevenue =
                    revenue?.overallRevenue ?? 0,

                opRevenue =
                    revenue?.opRevenue ?? 0,

                labRevenue =
                    revenue?.labRevenue ?? 0,

                pharmacyRevenue =
                    revenue?.pharmacyRevenue ?? 0,

                pendingAmount =
                    pending?.pendingAmount ?? 0,

                totalPaidBills =
                    revenue?.totalPaidBills ?? 0
            };
        })
        .OrderByDescending(x => x.overallRevenue)
        .ToList();

        return Ok(new
        {
            hospitalId = clinic.Id,
            clinicName = clinic.Name,

            overallRevenue,
            opRevenue,
            labRevenue,
            pharmacyRevenue,
            pendingAmount,

            fromDate,
            toDate,

            branches = branchRevenue
        });
    }

    // =====================================================
    // REVENUE TREND
    // =====================================================

    [HttpGet("revenue-trend")]
    public async Task<IActionResult> RevenueTrend(
        [FromQuery] int? hospitalId = null,
        [FromQuery] int? branchId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Paid");

        if (hospitalId.HasValue)
        {
            query = query.Where(x =>
                x.HospitalId == hospitalId.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x =>
                x.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;

            query = query.Where(x =>
                x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = toDate.Value.Date.AddDays(1);

            query = query.Where(x =>
                x.CreatedAt < toExclusive);
        }

        var data = await query
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new
            {
                date = g.Key,

                revenue =
                    g.Sum(x => x.TotalAmount),

                opRevenue =
                    g.Where(x => x.BillingType == "OP")
                     .Sum(x => x.TotalAmount),

                labRevenue =
                    g.Where(x => x.BillingType == "Lab")
                     .Sum(x => x.TotalAmount),

                pharmacyRevenue =
                    g.Where(x => x.BillingType == "Pharmacy")
                     .Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.date)
            .ToListAsync();

        return Ok(data);
    }

    // =====================================================
    // TOP CLINICS
    // =====================================================

    [HttpGet("top-clinics")]
    public async Task<IActionResult> TopClinics(
        [FromQuery] int limit = 10)
    {
        if (limit <= 0)
        {
            limit = 10;
        }

        if (limit > 100)
        {
            limit = 100;
        }

        var revenueData = await _context.Billings
            .AsNoTracking()
            .Where(x => x.Status == "Paid")
            .GroupBy(x => x.HospitalId)
            .Select(g => new
            {
                hospitalId = g.Key,

                overallRevenue =
                    g.Sum(x => x.TotalAmount),

                opRevenue =
                    g.Where(x => x.BillingType == "OP")
                     .Sum(x => x.TotalAmount),

                labRevenue =
                    g.Where(x => x.BillingType == "Lab")
                     .Sum(x => x.TotalAmount),

                pharmacyRevenue =
                    g.Where(x => x.BillingType == "Pharmacy")
                     .Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var clinics = await _context.Hospitals
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IsActive,

                visits = _context.Appointments
                    .Count(a => a.HospitalId == x.Id)
            })
            .ToListAsync();

        var result = clinics
            .Select(clinic =>
            {
                var revenue = revenueData.FirstOrDefault(x =>
                    x.hospitalId == clinic.Id);

                return new
                {
                    hospitalId = clinic.Id,
                    clinic = clinic.Name,
                    status = clinic.IsActive
                        ? "Active"
                        : "Inactive",

                    clinic.visits,

                    revenue =
                        revenue?.overallRevenue ?? 0,

                    overallRevenue =
                        revenue?.overallRevenue ?? 0,

                    opRevenue =
                        revenue?.opRevenue ?? 0,

                    labRevenue =
                        revenue?.labRevenue ?? 0,

                    pharmacyRevenue =
                        revenue?.pharmacyRevenue ?? 0
                };
            })
            .OrderByDescending(x => x.overallRevenue)
            .Take(limit)
            .ToList();

        return Ok(result);
    }

    // =====================================================
    // USER ACTIVITY
    // =====================================================

    [HttpGet("user-activity")]
    public async Task<IActionResult> UserActivity()
    {
        var totalUsers = await _context.Users
            .AsNoTracking()
            .CountAsync();

        var activeUsers = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.IsActive);

        var admins = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Admin");

        var doctors = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Doctor");

        var receptionists = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Receptionist");

        var nurses = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Nurse");

        var labTechnicians = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "LabTechnician");

        var patients = await _context.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == "Patient");

        return Ok(new
        {
            users = totalUsers,
            totalUsers,
            activeUsers,
            inactiveUsers = totalUsers - activeUsers,
            admins,
            doctors,
            receptionists,
            nurses,
            labTechnicians,
            patients
        });
    }
}