using AuthDemo.Data;
using AuthDemo.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/login-history")]
[Authorize(Roles = "Admin")]
public class LoginHistoryController : ControllerBase
{
    private readonly AppDbContext _context;
    public LoginHistoryController(AppDbContext context) => _context=context;
    private int HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Get(DateTime? fromDate=null, DateTime? toDate=null)
    {
        var q=_context.AuditLogs.AsNoTracking().Where(x=>x.ClinicId==HospitalId && x.IsLoginActivity && x.Role!="SuperAdmin");
        if(fromDate.HasValue)q=q.Where(x=>x.LoginTime>=fromDate.Value); if(toDate.HasValue)q=q.Where(x=>x.LoginTime<=toDate.Value);
        return Ok(await q.OrderByDescending(x=>x.LoginTime).Select(x=>new LoginHistoryDto { UserName=x.UserName, Role=x.Role, ClinicId=x.ClinicId, BranchId=x.BranchId, IpAddress=x.IpAddress, Browser=x.Browser, Device=x.Device, LoginTime=x.LoginTime, LogoutTime=x.LogoutTime, IsOnline=x.IsOnline }).ToListAsync());
    }
}
