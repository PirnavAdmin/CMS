using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ReceptionistController
    : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EmailHelper _emailHelper;
    public ReceptionistController(
        AppDbContext context,
        EmailHelper emailHelper)
    {
        _context = context;
        _emailHelper = emailHelper;
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
    /// condition//
    
    

    // =====================================================
    // CREATE RECEPTIONIST
    // =====================================================
    [HttpPost]
    public async Task<IActionResult>
    Create(
        RegisterReceptionistDto dto)
    {
        var temporaryPassword =
            "Receptionist@" +
            Guid.NewGuid()
                .ToString("N")[..6];

        if (!dto.Email.EndsWith("@gmail.com"))
        {
            return BadRequest(new
            {
                message = "Only Gmail addresses are allowed."
            });
        }

        if (dto.Phone.Length != 10 ||
            !dto.Phone.All(char.IsDigit) ||
            !"6789".Contains(dto.Phone[0]))
        {
            return BadRequest(new
            {
                message = "Enter a valid mobile number."
            });
        }

        var hospitalId = GetHospitalId();
        if (dto.BranchId == null)
        {
            return BadRequest(new
            {
                message = "Please select a branch."
            });
        }

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == dto.BranchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchExists)
        {
            return BadRequest(new
            {
                message = "Invalid branch."
            });
        }
        // =====================================================
        // CHECK RECEPTIONIST ALREADY EXISTS FOR THIS BRANCH
        // =====================================================

        var receptionistExists = await _context.Receptionists
            .AnyAsync(x =>
                x.BranchId == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.IsActive);

        if (receptionistExists)
        {
            return BadRequest(new
            {
                message = "A receptionist already exists for this branch."
            });
        }



        var exists =
            await _context.Users
                .AnyAsync(x => x.Email == dto.Email);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Email already exists"
            });
        }

        var passwordHash =
            BCrypt.Net.BCrypt
                .HashPassword(temporaryPassword);

        var receptionist =
            new Receptionist
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = passwordHash,
                HospitalId = hospitalId,
                BranchId=dto.BranchId
                
            };

        _context.Receptionists.Add(receptionist);

        var user =
            new User
            {
                Name = dto.Name,
                Email = dto.Email,
                MobileNumber = dto.Phone,
                PasswordHash = passwordHash,
                Role = "Receptionist",
                HospitalId = hospitalId,
                BranchId = dto.BranchId,
            };

        _context.Users.Add(user);

        await _context.SaveChangesAsync();

        await _emailHelper.SendAdminCredentials(
    dto.Email,
    temporaryPassword);

        return Ok(new
        {
            message = "Receptionist created successfully",
            temporaryPassword = temporaryPassword,
            receptionistId = receptionist.Id
        });
    }

    // =====================================================
    // GET ALL RECEPTIONISTS
    // =====================================================

    
    [HttpGet]
    public async Task<IActionResult>
        GetAll()
    {
        var hospitalId =
            GetHospitalId();

        var data =
            await _context.Receptionists

                .Where(x =>
                    x.HospitalId ==
                    hospitalId
                )

                .OrderByDescending(x =>
                    x.CreatedAt
                )
                .Include(x => x.Branch)

                .Select(x =>
                    new
                    {
                        x.Id,
                        x.Name,
                        x.Email,
                        x.Phone,
                        x.IsActive,
                        x.HospitalId,
                        branchId = x.BranchId,
                        branchName = x.Branch != null ? x.Branch.Name : null,
                        x.CreatedAt
                    })

                .ToListAsync();

        return Ok(data);
    }

    // =====================================================
    // GET RECEPTIONIST BY ID
    // =====================================================
    
    
    [HttpGet("{id}")]
    public async Task<IActionResult>
        GetById(
            int id)
    {
        var hospitalId =
            GetHospitalId();

        var receptionist =
            await _context.Receptionists

                .Where(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                )
                .Include(x => x.Branch)

                .Select(x =>
                    new
                    {
                        x.Id,
                        x.Name,
                        x.Email,
                        x.Phone,
                        x.IsActive,
                        x.HospitalId,
                        branchId = x.BranchId,
                        branchName = x.Branch != null ? x.Branch.Name : null,
                        x.CreatedAt
                    })

                .FirstOrDefaultAsync();

        if (receptionist == null)
        {
            return NotFound(new
            {
                message =
                    "Receptionist not found"
            });
        }

        return Ok(receptionist);
    }

    // =====================================================
    // UPDATE RECEPTIONIST
    // =====================================================
    
   
    [HttpPut("{id}")]
    public async Task<IActionResult>
        Update(
            int id,

            RegisterReceptionistDto dto)
    {
        var hospitalId =
            GetHospitalId();

        var receptionist =
            await _context.Receptionists

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (receptionist == null)
        {
            return NotFound(new
            {
                message =
                    "Receptionist not found"
            });
        }

        receptionist.Name =
            dto.Name;

        receptionist.Email =
            dto.Email;

        receptionist.Phone =
            dto.Phone;


        receptionist.BranchId = dto.BranchId;
        // =====================================================
        // CHECK BRANCH ALREADY HAS RECEPTIONIST
        // =====================================================

        var receptionistExists = await _context.Receptionists
            .AnyAsync(x =>
                x.BranchId == dto.BranchId &&
                x.HospitalId == hospitalId &&
                x.Id != id &&
                x.IsActive);

        if (receptionistExists)
        {
            return BadRequest(new
            {
                message = "This branch already has a receptionist."
            });
        }

        // =================================================
        // UPDATE LOGIN USER
        // =================================================

        var user =
            await _context.Users

                .FirstOrDefaultAsync(x =>
                    x.Email ==
                    receptionist.Email
                );

        if (user != null)
        {
            user.Name =
                dto.Name;

            user.Email =
                dto.Email;

            user.MobileNumber =
                dto.Phone;
            user.BranchId = dto.BranchId;
        }

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Receptionist updated successfully"
        });
    }

    // =====================================================
    // DELETE RECEPTIONIST
    // =====================================================
    
   
    [HttpDelete("{id}")]
    public async Task<IActionResult>
        Delete(
            int id)
    {
        var hospitalId =
            GetHospitalId();

        var receptionist =
            await _context.Receptionists

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (receptionist == null)
        {
            return NotFound(new
            {
                message =
                    "Receptionist not found"
            });
        }

        // =================================================
        // DELETE LOGIN USER
        // =================================================

        var user =
            await _context.Users

                .FirstOrDefaultAsync(x =>
                    x.Email ==
                    receptionist.Email
                );

        if (user != null)
        {
            _context.Users
                .Remove(user);
        }

        _context.Receptionists
            .Remove(receptionist);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Receptionist deleted successfully"
        });
    }
    // =====================================================
    // GET RECEPTIONISTS BY BRANCH
    // =====================================================

    [HttpGet("branch/{branchId}")]
    public async Task<IActionResult> GetByBranch(int branchId)
    {
        var hospitalId = GetHospitalId();

        var receptionists = await _context.Receptionists
            .Include(x => x.Branch)
            .Where(x =>
                x.HospitalId == hospitalId &&
                x.BranchId == branchId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.IsActive,
                x.HospitalId,
                branchId = x.BranchId,
                branchName = x.Branch != null ? x.Branch.Name : null,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(receptionists);
    }
}
