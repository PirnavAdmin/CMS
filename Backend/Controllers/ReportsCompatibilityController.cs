using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class ReportsCompatibilityController : ControllerBase
{
    private readonly AppDbContext _context;
    public ReportsCompatibilityController(AppDbContext context) => _context = context;
    private int HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    [HttpGet("api/revenue")]
    [HttpGet("api/reports/revenue")]
    public async Task<IActionResult> Revenue()
    {
        var query = _context.Billings.AsNoTracking().Where(x => x.HospitalId == HospitalId);
        var totalRevenue = await query.Where(x=>x.Status=="Paid").SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;
        var monthly = await query.Where(x=>x.Status=="Paid").GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month }).Select(g => new { year=g.Key.Year, month=g.Key.Month, revenue=g.Sum(x=>x.TotalAmount) }).OrderBy(x=>x.year).ThenBy(x=>x.month).ToListAsync();
        return Ok(new { totalRevenue, revenue=totalRevenue, monthly, items=monthly });
    }

    [HttpGet("api/activity")]
    [HttpGet("api/reports/activity")]
    public async Task<IActionResult> Activity()
    {
        var logs = await _context.AuditLogs.AsNoTracking().Where(x=>x.ClinicId==HospitalId && x.Role!="SuperAdmin").OrderByDescending(x=>x.Timestamp).Take(20).Select(x=>new { id=x.Id,user=x.UserName,role=x.Role,action=x.Action,module=x.SystemAction,ip=x.IpAddress,time=x.Timestamp }).ToListAsync();
        return Ok(new { logs, items=logs });
    }
}
