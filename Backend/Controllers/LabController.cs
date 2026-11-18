using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/Lab")]
[Authorize]
public class LabController : ControllerBase, IAsyncActionFilter
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public LabController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private int HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;
    private int? BranchId => int.TryParse(User.FindFirstValue("BranchId"), out var id) ? id : null;
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Defense in depth: do not rely solely on JWT clinic/branch claims or UI visibility.
    [NonAction]
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Patient report endpoints enforce ownership in their own queries.
        if (User.IsInRole("Patient"))
        {
            await next();
            return;
        }

        if (HospitalId <= 0 || UserId <= 0)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Valid clinic assignment is required. Please log in again."
            });
            return;
        }

        var account = await _context.Users.AsNoTracking()
            .Where(x => x.Id == UserId && x.IsActive)
            .Select(x => new { x.HospitalId, x.BranchId, x.Role })
            .FirstOrDefaultAsync();

        if (account == null || account.HospitalId != HospitalId ||
            !string.Equals(account.Role, User.FindFirstValue(ClaimTypes.Role), StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ForbidResult();
            return;
        }

        if ((User.IsInRole("LabTechnician") || User.IsInRole("Receptionist")) &&
            (!BranchId.HasValue || account.BranchId != BranchId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var now = DateTime.UtcNow;
        var labEnabled = await _context.ClinicSubscriptions.AsNoTracking()
            .AnyAsync(x => x.HospitalId == HospitalId &&
                x.Status == "Active" && x.IncludesLab &&
                x.StartDate <= now && x.EndDate > now);

        if (!labEnabled)
        {
            context.Result = new ObjectResult(new
            {
                message = "An active subscription with Lab access is required."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }


    private IQueryable<LabOrder> ScopedOrders()
    {
        IQueryable<LabOrder> query = _context.LabOrders
            .Where(x => x.HospitalId == HospitalId);

        if (User.IsInRole("LabTechnician") && BranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == BranchId.Value);
        }

        return query;
    }

    // Read-only lab payment verification. Does not grant access to BillingController.
    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpGet("billing-status/{appointmentId:int}")]
    public async Task<IActionResult> GetLabBillingStatus(int appointmentId)
    {
        if (appointmentId <= 0)
            return BadRequest(new { message = "Valid appointment ID is required." });

        // Lab Technician must be assigned to a branch.
        if (User.IsInRole("LabTechnician") && !BranchId.HasValue)
            return StatusCode(403, new { message = "Branch assignment is required." });

        var appointment = await _context.Appointments.AsNoTracking()
            .Where(x => x.Id == appointmentId && x.HospitalId == HospitalId)
            .Select(x => new { x.Id, x.PatientId, x.HospitalId, x.BranchId })
            .FirstOrDefaultAsync();

        if (appointment == null ||
            (User.IsInRole("LabTechnician") && appointment.BranchId != BranchId))
            return NotFound(new { message = "Appointment not found in your authorized clinic/branch." });

        var hasLabOrders = await _context.LabOrders.AsNoTracking()
            .AnyAsync(x => x.AppointmentId == appointmentId &&
                           x.HospitalId == HospitalId &&
                           x.BranchId == appointment.BranchId &&
                           x.Status != "Cancelled");

        var bill = await _context.Billings.AsNoTracking()
            .Where(x => x.AppointmentId == appointmentId &&
                        x.PatientId == appointment.PatientId &&
                        x.HospitalId == HospitalId &&
                        x.BranchId == appointment.BranchId &&
                        x.BillingType == "Lab")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Status, x.TotalAmount })
            .FirstOrDefaultAsync();

        var paid = bill != null &&
            string.Equals(bill.Status, "Paid", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            appointmentId,
            patientId = appointment.PatientId,
            hospitalId = appointment.HospitalId,
            branchId = appointment.BranchId,
            hasLabOrders,
            billId = bill?.Id,
            billNumber = bill == null ? null : "BILL-" + bill.Id,
            billingType = "Lab",
            paymentStatus = bill?.Status ?? "NotBilled",
            totalAmount = bill?.TotalAmount,
            isBillingCompleted = paid,
            canCollectSample = paid && hasLabOrders,
            message = !hasLabOrders ? "No active lab orders found." :
                bill == null ? "Lab bill has not been created." :
                paid ? "Lab billing completed." : "Lab payment is pending."
        });
    }

    // =====================================================
    // LAB TECHNICIAN DASHBOARD
    // =====================================================

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if (HospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        IQueryable<LabOrder> ordersQuery = ScopedOrders()
            .AsNoTracking();

        var todayOrders = await ordersQuery.CountAsync(x =>
            x.OrderedAt >= today && x.OrderedAt < tomorrow);

        var pendingOrders = await ordersQuery.CountAsync(x =>
            x.Status == "Ordered");

        var sampleCollected = await ordersQuery.CountAsync(x =>
            x.Status == "SampleCollected");

        var inProgress = await ordersQuery.CountAsync(x =>
            x.Status == "InProgress");

        var completedToday = await ordersQuery.CountAsync(x =>
            x.Status == "Completed" &&
            x.CompletedAt.HasValue &&
            x.CompletedAt.Value >= today &&
            x.CompletedAt.Value < tomorrow);

        var cancelled = await ordersQuery.CountAsync(x =>
            x.Status == "Cancelled");

        var pendingReports = await ordersQuery.CountAsync(x =>
            x.Status != "Completed" &&
            x.Status != "Cancelled" &&
            string.IsNullOrEmpty(x.ReportFilePath));

        IQueryable<Billing> billingQuery = _context.Billings
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == HospitalId &&
                x.BillingType == "Lab" &&
                x.CreatedAt >= today &&
                x.CreatedAt < tomorrow);

        if (User.IsInRole("LabTechnician") && BranchId.HasValue)
        {
            billingQuery = billingQuery.Where(x =>
                x.BranchId == BranchId.Value);
        }

        var todayRevenue = await billingQuery
            .Where(x => x.Status == "Paid")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var pendingBillingAmount = await billingQuery
            .Where(x => x.Status == "Pending")
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var recentOrders = await ordersQuery
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .OrderByDescending(x => x.OrderedAt)
            .Take(10)
            .Select(x => new
            {
                labOrderId = x.Id,
                appointmentId = x.AppointmentId,
                appointmentNumber = x.Appointment != null
                    ? x.Appointment.TokenNumber
                    : null,
                patientId = x.PatientId,
                patientName = x.Patient != null
                    ? x.Patient.Name
                    : null,
                doctorId = x.DoctorId,
                doctorName = x.Doctor != null
                    ? x.Doctor.Name
                    : null,
                testCode = x.TestCode,
                testName = x.TestName,
                priority = x.Priority,
                status = x.Status,
                branchId = x.BranchId,
                price = x.Price,
                orderedAt = x.OrderedAt,
                sampleCollectedAt = x.SampleCollectedAt,
                completedAt = x.CompletedAt,
                hasResult = !string.IsNullOrEmpty(x.ResultValue),
                hasReport = !string.IsNullOrEmpty(x.ReportFilePath)
            })
            .ToListAsync();

        return Ok(new
        {
            hospitalId = HospitalId,
            branchId = User.IsInRole("LabTechnician")
                ? BranchId
                : null,
            generatedAt = DateTime.UtcNow,
            cards = new
            {
                todayOrders,
                pendingOrders,
                sampleCollected,
                inProgress,
                completedToday,
                cancelled,
                pendingReports,
                todayRevenue,
                pendingBillingAmount
            },
            recentOrders
        });
    }

    // Returns lab test catalogue records saved in the database.
    [Authorize(Roles = "Admin,Doctor,LabTechnician")]
    [HttpGet("master")]
    public async Task<IActionResult> GetMaster(int? branchId = null, string? category = null, string? search = null, bool? isActive = null)
    {
        var q = _context.LabTestMasters.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId);

        if (User.IsInRole("LabTechnician"))
        {
            if (!BranchId.HasValue)
                return StatusCode(403, new { message = "Branch assignment is required." });
            q = q.Where(x => x.BranchId == null || x.BranchId == BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            if (!await _context.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId.Value && x.HospitalId == HospitalId))
                return BadRequest(new { message = "Invalid branch." });
            q = q.Where(x => x.BranchId == null || x.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(x => x.Category == category.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.TestCode.Contains(term) || x.TestName.Contains(term));
        }
        if (isActive.HasValue)
            q = q.Where(x => x.IsActive == isActive.Value);

        return Ok(await q.OrderBy(x => x.Category).ThenBy(x => x.TestName).ToListAsync());
    }

    // Lightweight dropdown endpoint. Test names come from the persisted master catalogue.
    [Authorize(Roles = "Admin,Doctor,LabTechnician")]
    [HttpGet("master/names")]
    public async Task<IActionResult> GetMasterNames(int? branchId = null, string? search = null)
    {
        var q = _context.LabTestMasters.AsNoTracking()
            .Where(x => x.HospitalId == HospitalId && x.IsActive);

        if (User.IsInRole("LabTechnician"))
        {
            if (!BranchId.HasValue)
                return StatusCode(403, new { message = "Branch assignment is required." });
            q = q.Where(x => x.BranchId == null || x.BranchId == BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            if (!await _context.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId.Value && x.HospitalId == HospitalId))
                return BadRequest(new { message = "Invalid branch." });
            q = q.Where(x => x.BranchId == null || x.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x => x.TestName.Contains(term) || x.TestCode.Contains(term));
        }

        return Ok(await q.OrderBy(x => x.TestName)
            .Select(x => new {
                id = x.Id,
                testCode = x.TestCode,
                testName = x.TestName,
                category = x.Category,
                sampleType = x.SampleType,
                price = x.Price,
                branchId = x.BranchId
            })
            .ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("master")]
    public async Task<IActionResult> CreateMaster(UpsertLabTestMasterDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.TestCode) || string.IsNullOrWhiteSpace(dto.TestName))
            return BadRequest(new { message = "TestCode and TestName are required." });
        if (dto.Price < 0)
            return BadRequest(new { message = "Price cannot be negative." });
        if (dto.TurnaroundHours < 0)
            return BadRequest(new { message = "TurnaroundHours cannot be negative." });

        var code = dto.TestCode.Trim().ToUpperInvariant();
        if (await _context.LabTestMasters.AnyAsync(x => x.HospitalId == HospitalId && x.TestCode == code))
            return BadRequest(new { message = "TestCode already exists." });
        if (dto.BranchId.HasValue && !await _context.Branches.AnyAsync(x => x.Id == dto.BranchId.Value && x.HospitalId == HospitalId))
            return BadRequest(new { message = "Invalid branch." });

        var entity = MapMaster(new LabTestMaster { HospitalId = HospitalId, CreatedAt = DateTime.UtcNow }, dto, code);
        _context.LabTestMasters.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Lab test created successfully.", id = entity.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("master/{id:int}")]
    public async Task<IActionResult> UpdateMaster(
        int id,
        UpsertLabTestMasterDto dto)
    {
        var e = await _context.LabTestMasters
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == HospitalId);

        if (e == null)
        {
            return NotFound(new
            {
                message = "Lab test not found."
            });
        }

        var code = dto.TestCode
            .Trim()
            .ToUpperInvariant();

        var duplicate = await _context.LabTestMasters
            .AnyAsync(x =>
                x.Id != id &&
                x.HospitalId == HospitalId &&
                x.TestCode == code);

        if (duplicate)
        {
            return BadRequest(new
            {
                message = "TestCode already exists."
            });
        }

        // BranchId may be NULL for a hospital-wide/global test.
        // If a branch is supplied, it must belong to the logged-in hospital.
        if (dto.BranchId.HasValue)
        {
            var validBranch = await _context.Branches
                .AnyAsync(x =>
                    x.Id == dto.BranchId.Value &&
                    x.HospitalId == HospitalId);

            if (!validBranch)
            {
                return BadRequest(new
                {
                    message = "Invalid branch."
                });
            }
        }

        e.TestCode = code;
        e.TestName = dto.TestName.Trim();
        e.Category = dto.Category?.Trim();
        e.SampleType = dto.SampleType?.Trim();
        e.Unit = dto.Unit?.Trim();
        e.ReferenceRange = dto.ReferenceRange?.Trim();
        e.Price = dto.Price;
        e.TurnaroundHours = dto.TurnaroundHours;
        e.Instructions = dto.Instructions?.Trim();
        e.BranchId = dto.BranchId;
        e.IsActive = dto.IsActive;
        e.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Lab test updated successfully.",
            id = e.Id
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("master/{id:int}")]
    public async Task<IActionResult> DeactivateMaster(int id)
    {
        var e = await _context.LabTestMasters.FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == HospitalId);
        if (e == null) return NotFound(new { message = "Lab test not found." });
        e.IsActive = false; e.UpdatedAt = DateTime.UtcNow; await _context.SaveChangesAsync();
        return Ok(new { message = "Lab test deactivated successfully." });
    }

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpGet("master/export")]
    public async Task<IActionResult> ExportMaster(int? branchId = null)
    {
        var q = _context.LabTestMasters.AsNoTracking().Where(x => x.HospitalId == HospitalId);
        if (User.IsInRole("LabTechnician") && BranchId.HasValue) q = q.Where(x => x.BranchId == null || x.BranchId == BranchId.Value);
        else if (branchId.HasValue) q = q.Where(x => x.BranchId == null || x.BranchId == branchId.Value);
        var rows = await q.OrderBy(x => x.TestCode).ToListAsync();
        using var wb = new XLWorkbook(); var ws = wb.Worksheets.Add("Lab Test Master");
        string[] headers = { "TestCode", "TestName", "Category", "SampleType", "Unit", "ReferenceRange", "Price", "TurnaroundHours", "Instructions", "BranchId", "IsActive" };
        for (int c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        int r = 2; foreach (var x in rows) { ws.Cell(r, 1).Value = x.TestCode; ws.Cell(r, 2).Value = x.TestName; ws.Cell(r, 3).Value = x.Category; ws.Cell(r, 4).Value = x.SampleType; ws.Cell(r, 5).Value = x.Unit; ws.Cell(r, 6).Value = x.ReferenceRange; ws.Cell(r, 7).Value = x.Price; ws.Cell(r, 8).Value = x.TurnaroundHours; ws.Cell(r, 9).Value = x.Instructions; ws.Cell(r, 10).Value = x.BranchId; ws.Cell(r, 11).Value = x.IsActive; r++; }
        ws.Row(1).Style.Font.Bold = true; ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Lab_Test_Master_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("master/import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportMaster(IFormFile file)
    {
        if (file == null || file.Length == 0 || Path.GetExtension(file.FileName).ToLowerInvariant() != ".xlsx")
            return BadRequest(new { message = "A valid .xlsx file is required." });
        int inserted = 0, updated = 0, skipped = 0; var errors = new List<object>();
        using var stream = file.OpenReadStream(); using var wb = new XLWorkbook(stream); var ws = wb.Worksheets.First();
        var header = Enumerable.Range(1, ws.LastColumnUsed()?.ColumnNumber() ?? 0).ToDictionary(c => ws.Cell(1, c).GetString().Trim(), c => c, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "TestCode", "TestName", "Price" }) if (!header.ContainsKey(required)) return BadRequest(new { message = $"Missing column: {required}" });
        int last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int row = 2; row <= last; row++)
        {
            try
            {
                string Get(string n) => header.TryGetValue(n, out var c) ? ws.Cell(row, c).GetString().Trim() : "";
                var code = Get("TestCode").ToUpperInvariant(); var name = Get("TestName");
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) { skipped++; continue; }
                if (!decimal.TryParse(Get("Price"), out var price) || price < 0) throw new Exception("Invalid Price");
                int.TryParse(Get("TurnaroundHours"), out var hours); int? branch = null; if (int.TryParse(Get("BranchId"), out var bid)) branch = bid;
                if (branch.HasValue && !await _context.Branches.AnyAsync(x => x.Id == branch && x.HospitalId == HospitalId)) throw new Exception("Invalid BranchId");
                bool active = !bool.TryParse(Get("IsActive"), out var a) || a;
                var e = await _context.LabTestMasters.FirstOrDefaultAsync(x => x.HospitalId == HospitalId && x.TestCode == code);
                if (e == null) { e = new LabTestMaster { HospitalId = HospitalId, TestCode = code, CreatedAt = DateTime.UtcNow }; _context.LabTestMasters.Add(e); inserted++; } else { updated++; e.UpdatedAt = DateTime.UtcNow; }
                e.TestName = name; e.Category = Get("Category"); e.SampleType = Get("SampleType"); e.Unit = Get("Unit"); e.ReferenceRange = Get("ReferenceRange"); e.Price = price; e.TurnaroundHours = hours; e.Instructions = Get("Instructions"); e.BranchId = branch; e.IsActive = active;
            }
            catch (Exception ex) { errors.Add(new { row, message = ex.Message }); }
        }
        await _context.SaveChangesAsync();
        return Ok(new { message = "Lab test master imported successfully.", inserted, updated, skipped, failed = errors.Count, errors });
    }

    [Authorize(Roles = "Admin,Receptionist,LabTechnician")]
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        int? appointmentId = null,
        string? status = null,
        DateTime? date = null,
        string? search = null)
    {
        if (HospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        IQueryable<LabOrder> q = _context.LabOrders
            .AsNoTracking()
            .Where(x => x.HospitalId == HospitalId)
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment);

        // Receptionist and Lab Technician can only read orders from
        // the branch contained in their authentication token.
        if ((User.IsInRole("Receptionist") ||
             User.IsInRole("LabTechnician")) &&
            BranchId.HasValue)
        {
            q = q.Where(x => x.BranchId == BranchId.Value);
        }

        // Used by Reception Billing to fetch the tests prescribed
        // for the currently selected appointment.
        if (appointmentId.HasValue)
        {
            q = q.Where(x => x.AppointmentId == appointmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();

            q = q.Where(x =>
                x.Status == normalizedStatus);
        }

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;

            q = q.Where(x =>
                x.OrderedAt.Date == selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            q = q.Where(x =>
                x.TestName.Contains(term) ||
                (x.Patient != null &&
                 (
                     x.Patient.Name.Contains(term) ||
                     x.Patient.PatientCode.Contains(term)
                 )));
        }

        var orders = await q
            .OrderByDescending(x => x.OrderedAt)
            .Select(x => new
            {
                labOrderId = x.Id,

                appointmentId = x.AppointmentId,

                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                patientId = x.PatientId,

                patientCode =
                    x.Patient != null
                        ? x.Patient.PatientCode
                        : null,

                patientName =
                    x.Patient != null
                        ? x.Patient.Name
                        : null,

                patientPhone =
                    x.Patient != null
                        ? x.Patient.Phone
                        : null,

                doctorId = x.DoctorId,

                doctorName =
                    x.Doctor != null
                        ? x.Doctor.Name
                        : null,

                testCode = x.TestCode,
                testName = x.TestName,
                category = x.Category,
                sampleType = x.SampleType,
                price = x.Price,

                paymentStatus = _context.Billings
                    .Where(b =>
                        b.AppointmentId == x.AppointmentId &&
                        b.HospitalId == x.HospitalId &&
                        b.BillingType == "Lab")
                    .Select(b => b.Status)
                    .FirstOrDefault() ?? "NotBilled",

                canCollectSample = _context.Billings.Any(b =>
                    b.AppointmentId == x.AppointmentId &&
                    b.HospitalId == x.HospitalId &&
                    b.BillingType == "Lab" &&
                    b.Status == "Paid"),

                branchId = x.BranchId,
                priority = x.Priority,
                status = x.Status,
                orderedAt = x.OrderedAt,
                sampleCollectedAt = x.SampleCollectedAt,
                completedAt = x.CompletedAt,

                hasReport =
                    !string.IsNullOrWhiteSpace(
                        x.ReportFilePath)
            })
            .ToListAsync();

        return Ok(orders);
    }

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var x = await ScopedOrders().AsNoTracking().Include(x => x.Patient).Include(x => x.Doctor).Include(x => x.Appointment).FirstOrDefaultAsync(x => x.Id == id);
        if (x == null) return NotFound(new { message = "Lab order not found." }); return Ok(x);
    }

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpPatch("orders/{id:int}/sample-collected")]
    public Task<IActionResult> SampleCollected(int id) => SetStatus(id, "SampleCollected");
    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpPatch("orders/{id:int}/start")]
    public Task<IActionResult> Start(int id) => SetStatus(id, "InProgress");

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpPut("orders/{id:int}/result")]
    public async Task<IActionResult> Result(int id, UpdateLabResultDto dto)
    {
        var x = await ScopedOrders()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (x == null)
        {
            return NotFound(new { message = "Lab order not found." });
        }

        var paymentError = await ValidatePaidLabBill(x);
        if (paymentError != null)
        {
            return paymentError;
        }

        x.ResultValue = dto.ResultValue?.Trim();
        x.ResultUnit = dto.ResultUnit?.Trim();
        x.ReferenceRange = dto.ReferenceRange?.Trim() ?? x.ReferenceRange;
        x.Remarks = dto.Remarks?.Trim();
        x.TechnicianUserId = UserId;

        if (x.Status == "Ordered" || x.Status == "SampleCollected")
        {
            x.Status = "InProgress";
            x.StartedAt ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Result saved successfully.",
            x.Id,
            x.Status
        });
    }

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpPost("orders/{id:int}/report")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadReport(int id, IFormFile file)
    {
        var x = await ScopedOrders()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (x == null)
        {
            return NotFound(new { message = "Lab order not found." });
        }

        var paymentError = await ValidatePaidLabBill(x);
        if (paymentError != null)
        {
            return paymentError;
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Report file is required." });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(ext))
        {
            return BadRequest(new
            {
                message = "Only PDF, JPG, JPEG and PNG reports are allowed."
            });
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            return BadRequest(new { message = "Report must not exceed 20 MB." });
        }

        // Store sensitive reports outside wwwroot so static-file middleware
        // cannot serve them without authorization.
        var privateFolder = Path.Combine(
            _environment.ContentRootPath, "private-data", "lab-reports");
        Directory.CreateDirectory(privateFolder);

        var stored = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(privateFolder, stored);

        await using (var fs = new FileStream(
            fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(fs);
        }

        x.ReportFilePath = $"private/lab-reports/{stored}";
        x.ReportFileName = Path.GetFileName(file.FileName);
        x.ReportContentType = file.ContentType;
        x.TechnicianUserId = UserId;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Avoid leaving an orphaned sensitive report if DB save fails.
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            throw;
        }

        return Ok(new
        {
            message = "Report uploaded successfully.",
            x.ReportFilePath
        });
    }

    [Authorize(Roles = "Admin,LabTechnician")]
    [HttpPatch("orders/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var x = await ScopedOrders()
            .Include(x => x.Patient)
            .Include(x => x.PrescriptionLabTest)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (x == null)
        {
            return NotFound(new { message = "Lab order not found." });
        }

        var paymentError = await ValidatePaidLabBill(x);
        if (paymentError != null)
        {
            return paymentError;
        }

        if (string.IsNullOrWhiteSpace(x.ResultValue) &&
            string.IsNullOrWhiteSpace(x.ReportFilePath))
        {
            return BadRequest(new
            {
                message = "Enter a result or upload a report before completion."
            });
        }

        x.Status = "Completed";
        x.CompletedAt = DateTime.UtcNow;
        x.TechnicianUserId = UserId;

        if (x.PrescriptionLabTest != null)
        {
            x.PrescriptionLabTest.Status = "Completed";
        }

        _context.Notifications.Add(new Notification
        {
            PatientId = x.PatientId,
            Title = "Lab Report Ready",
            Message = $"Your {x.TestName} report is completed and ready to view.",
            IsSent = true,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Lab report completed successfully.",
            x.Id,
            x.Status
        });
    }

    [Authorize(Roles = "Doctor")]
    [HttpGet("doctor/reports")]
    public async Task<IActionResult> DoctorReports(int? branchId = null)
    {
        int doctorId = int.TryParse(User.FindFirstValue("DoctorId"), out var d) ? d : 0; var q = _context.LabOrders.AsNoTracking().Where(x => x.DoctorId == doctorId && x.HospitalId == HospitalId && x.Status == "Completed"); if (branchId.HasValue) q = q.Where(x => x.BranchId == branchId); return Ok(await q.Include(x => x.Patient).OrderByDescending(x => x.CompletedAt).ToListAsync());
    }

    [Authorize(Roles = "Patient")]
    [HttpGet("patient/reports")]
    public async Task<IActionResult> PatientReports()
    {
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        var patient = await _context.Patients.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email);
        if (patient == null) return NotFound(new { message = "Patient not found." });

        var now = DateTime.UtcNow;
        var reports = await _context.LabOrders.AsNoTracking()
            .Where(x => x.PatientId == patient.Id && x.Status == "Completed")
            .Where(x => _context.ClinicSubscriptions.Any(s =>
                s.HospitalId == x.HospitalId && s.Status == "Active" &&
                s.IncludesLab && s.StartDate <= now && s.EndDate > now))
            .OrderByDescending(x => x.CompletedAt)
            .ToListAsync();
        return Ok(reports);
    }

    [Authorize(Roles = "Admin,LabTechnician,Doctor,Patient")]
    [HttpGet("orders/{id:int}/report/download")]
    public async Task<IActionResult> DownloadReport(int id)
    {
        var x = await _context.LabOrders.AsNoTracking()
            .Include(o => o.Patient)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (x == null || string.IsNullOrWhiteSpace(x.ReportFilePath))
            return NotFound(new { message = "Report not found." });

        if (User.IsInRole("Patient"))
        {
            var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email) ||
                !await _context.Patients.AsNoTracking().AnyAsync(p =>
                    p.Id == x.PatientId && p.Email == email))
                return Forbid();

            var now = DateTime.UtcNow;
            var allowed = await _context.ClinicSubscriptions.AsNoTracking()
                .AnyAsync(s => s.HospitalId == x.HospitalId &&
                    s.Status == "Active" && s.IncludesLab &&
                    s.StartDate <= now && s.EndDate > now);
            if (!allowed) return StatusCode(403, new { message = "Lab access is not active." });
        }
        else
        {
            if (x.HospitalId != HospitalId) return NotFound();
            if (User.IsInRole("LabTechnician") && x.BranchId != BranchId) return Forbid();
            if (User.IsInRole("Doctor") &&
                x.DoctorId != (int.TryParse(User.FindFirstValue("DoctorId"), out var doctorId) ? doctorId : 0))
                return Forbid();
        }

        // Only accept filenames generated by our server. Never use a
        // database path directly as an arbitrary filesystem path.
        var storedPath = x.ReportFilePath.Replace('\\', '/').TrimStart('/');
        var isNew = storedPath.StartsWith("private/lab-reports/", StringComparison.OrdinalIgnoreCase);
        var isLegacy = storedPath.StartsWith("uploads/lab-reports/", StringComparison.OrdinalIgnoreCase);
        if (!isNew && !isLegacy)
            return NotFound(new { message = "Invalid report path." });

        var filename = Path.GetFileName(storedPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                filename, @"\A[a-fA-F0-9]{32}\.(pdf|jpg|jpeg|png)\z",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return NotFound(new { message = "Invalid report filename." });

        var privateFolder = Path.Combine(
            _environment.ContentRootPath, "private-data", "lab-reports");
        var full = Path.Combine(privateFolder, filename);
        if (!System.IO.File.Exists(full))
            return NotFound(new { message = "Report file is missing from protected storage." });

        // Legacy DB paths are accepted only after their files have been
        // moved OUT of wwwroot into the protected folder.
        return PhysicalFile(full, x.ReportContentType ?? "application/octet-stream",
            x.ReportFileName ?? filename);
    }

    private LabTestMaster MapMaster(
        LabTestMaster entity,
        UpsertLabTestMasterDto dto,
        string code)
    {
        entity.TestCode = code;
        entity.TestName = dto.TestName.Trim();
        entity.Category = dto.Category?.Trim();
        entity.SampleType = dto.SampleType?.Trim();
        entity.Unit = dto.Unit?.Trim();
        entity.ReferenceRange = dto.ReferenceRange?.Trim();
        entity.Price = dto.Price;
        entity.TurnaroundHours = dto.TurnaroundHours;
        entity.Instructions = dto.Instructions?.Trim();
        entity.BranchId = dto.BranchId;
        entity.IsActive = dto.IsActive;
        return entity;
    }

    private async Task<IActionResult?> ValidatePaidLabBill(LabOrder order)
    {
        var labBill = await _context.Billings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppointmentId == order.AppointmentId &&
                x.HospitalId == order.HospitalId &&
                x.BillingType == "Lab");

        if (labBill == null)
        {
            return BadRequest(new
            {
                message = "Lab bill has not been created. Send the patient to Billing first."
            });
        }

        if (!string.Equals(
                labBill.Status,
                "Paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Lab bill payment is pending. Complete payment before processing the test.",
                billId = labBill.Id,
                paymentStatus = labBill.Status
            });
        }

        return null;
    }

    private async Task<IActionResult> SetStatus(int id, string status)
    {
        var x = await ScopedOrders()
            .Include(x => x.PrescriptionLabTest)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (x == null)
        {
            return NotFound(new { message = "Lab order not found." });
        }

        if (status == "SampleCollected" || status == "InProgress")
        {
            var paymentError = await ValidatePaidLabBill(x);
            if (paymentError != null)
            {
                return paymentError;
            }
        }

        x.Status = status;
        x.TechnicianUserId = UserId;

        if (status == "SampleCollected")
        {
            x.SampleCollectedAt = DateTime.UtcNow;
        }

        if (status == "InProgress")
        {
            x.StartedAt ??= DateTime.UtcNow;
        }

        if (x.PrescriptionLabTest != null)
        {
            x.PrescriptionLabTest.Status = status;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Status updated to {status}.",
            x.Id,
            x.Status
        });
    }
}

