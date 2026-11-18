
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacists")]
[Authorize(Roles = "Admin")]
public class PharmacistsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public PharmacistsController(AppDbContext db)
    {
        _db = db;
        _hasher = new PasswordHasher<User>();
    }

    private async Task<User?> GetAdminAsync()
    {
        var idValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(idValue, out var userId))
            return null;

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.Role == "Admin" &&
                x.IsActive);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePharmacistDto dto)
    {
        var admin = await GetAdminAsync();

        if (admin == null || !admin.HospitalId.HasValue)
            return Forbid();

        int hospitalId = admin.HospitalId.Value;

        bool branchExists = await _db.Branches
            .AnyAsync(x =>
                x.Id == dto.BranchId &&
                x.HospitalId == hospitalId);

        if (!branchExists)
            return BadRequest(
                "Branch does not belong to your clinic.");

        string email = dto.Email.Trim().ToLowerInvariant();

        bool emailExists = await _db.Users
            .AnyAsync(x => x.Email == email);

        if (emailExists)
            return Conflict("Email already registered.");

        var pharmacist = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            MobileNumber = dto.MobileNumber,
            Role = "Pharmacist",
            HospitalId = hospitalId,
            BranchId = dto.BranchId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        pharmacist.PasswordHash =
            _hasher.HashPassword(pharmacist, dto.Password);

        _db.Users.Add(pharmacist);
        await _db.SaveChangesAsync();

        return StatusCode(201, new
        {
            pharmacist.Id,
            pharmacist.Name,
            pharmacist.Email,
            pharmacist.MobileNumber,
            pharmacist.Role,
            pharmacist.HospitalId,
            pharmacist.BranchId,
            pharmacist.IsActive
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var admin = await GetAdminAsync();

        if (admin == null || !admin.HospitalId.HasValue)
            return Forbid();

        var pharmacists = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.HospitalId == admin.HospitalId.Value &&
                x.Role == "Pharmacist")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.BranchId,
                x.IsActive
            })
            .ToListAsync();

        return Ok(pharmacists);
    }
}
