using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class BranchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BranchController(AppDbContext context)
        {
            _context = context;
        }

        //==================================================
        // CREATE BRANCH
        //==================================================

        [HttpPost]
        public async Task<IActionResult> CreateBranch(BranchDto dto)
        {
            var hospital = await _context.Hospitals.FindAsync(dto.HospitalId);

            if (hospital == null)
                return BadRequest("Hospital not found.");

            Branch branch = new Branch
            {
                HospitalId = dto.HospitalId,
                Name = dto.Name,
                Phone = dto.Phone,
                Email = dto.Email,
                Address = dto.Address,
                City = dto.City,
                District = dto.District,
                State = dto.State,
                Country = dto.Country,
                PostalCode = dto.PostalCode,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Branches.Add(branch);

            await _context.SaveChangesAsync();

            return Ok(branch);
        }

        //==================================================
        // GET ALL BRANCHES
        //==================================================

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await _context.Branches
                .Include(x => x.Hospital)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(branches);
        }

        //==================================================
        // GET BRANCH BY ID
        //==================================================

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBranch(int id)
        {
            var branch = await _context.Branches
                .Include(x => x.Hospital)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (branch == null)
                return NotFound();

            return Ok(branch);
        }

        //==================================================
        // UPDATE BRANCH
        //==================================================

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(int id, BranchDto dto)
        {
            var branch = await _context.Branches.FindAsync(id);

            if (branch == null)
                return NotFound();

            branch.Name = dto.Name;
            branch.Phone = dto.Phone;
            branch.Email = dto.Email;
            branch.Address = dto.Address;
            branch.City = dto.City;
            branch.District = dto.District;
            branch.State = dto.State;
            branch.Country = dto.Country;
            branch.PostalCode = dto.PostalCode;

            await _context.SaveChangesAsync();

            return Ok(branch);
        }

        //==================================================
        // ACTIVATE / DEACTIVATE
        //==================================================

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id)
        {
            var branch = await _context.Branches.FindAsync(id);

            if (branch == null)
                return NotFound();

            branch.IsActive = !branch.IsActive;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = branch.IsActive
                    ? "Branch Activated"
                    : "Branch Deactivated"
            });
        }

        [HttpGet("hospital/{hospitalId}")]
        public async Task<IActionResult> GetBranchesByHospital(int hospitalId)
        {
            var hospital = await _context.Hospitals.FindAsync(hospitalId);

            if (hospital == null)
                return NotFound(new { Message = "Hospital not found." });

            var branches = await _context.Branches
                .Where(x => x.HospitalId == hospitalId)
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Ok(branches);
        }
    }
}