using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class StaffService
    : IStaffService
{
    private readonly AppDbContext
        _context;

    public StaffService(
        AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // GET ALL STAFF
    // =====================================================

    public async Task<List<StaffResponseDto>>
        GetAllAsync(
            int hospitalId)
    {
        return await _context.Staffs
     .Include(x => x.User)
     .Include(x => x.Branch)
             .Where(x =>
                x.HospitalId ==
                hospitalId
            )

            .Include(x =>
                x.User
            )

            .OrderByDescending(x =>
                x.CreatedAt
            )

            .Select(x =>
                new StaffResponseDto
                {
                    Id =
                        x.Id,

                    Name =
                        x.User.Name,

                    Email =
                        x.User.Email,

                    Phone =
                        x.User.MobileNumber,

                    Role =
                        x.Role,
                    BranchId = x.BranchId,
                    BranchName = x.Branch != null ? x.Branch.Name : null,
                    HospitalId = x.HospitalId,

                    IsActive =
                        x.IsActive,

                    
                })

            .ToListAsync();
    }

    // =====================================================
    // CREATE STAFF
    // =====================================================

    public async Task<StaffResponseDto>
        CreateAsync(
            CreateStaffDto dto,
            string rootPath,
            int hospitalId)
    {
        // =================================================
        // EMAIL EXISTS
        // =================================================

        var exists =
            await _context.Users
                .AnyAsync(x =>
                    x.Email ==
                    dto.Email
                );

        if (exists)
        {
            throw new Exception(
                "Email already exists"
            );
        }
        var allowedRoles = new[] { "Receptionist", "Nurse", "LabTech", "Accountant" };
        if (string.IsNullOrWhiteSpace(dto.Role) ||
            !allowedRoles.Contains(dto.Role, StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception("Role must be Receptionist, Nurse, LabTech or Accountant.");
        }

        dto.Role = allowedRoles.First(x => x.Equals(dto.Role, StringComparison.OrdinalIgnoreCase));

        if (dto.BranchId == null)
        {
            throw new Exception("Please select a branch.");
        }

        var branchExists = await _context.Branches.AnyAsync(x =>
            x.Id == dto.BranchId &&
            x.HospitalId == hospitalId &&
            x.IsActive);

        if (!branchExists)
        {
            throw new Exception("Invalid branch.");
        }

        // =================================================
        // CREATE USER
        // =================================================

        var user =
            new User
            {
                Name =
                    dto.Name,

                Email =
                    dto.Email,

                MobileNumber =
                    dto.Phone,

                Role =
                    dto.Role,
                

                PasswordHash =
                    BCrypt.Net.BCrypt
                        .HashPassword(
                            dto.Password
                        ),

                HospitalId = hospitalId,
                BranchId = dto.BranchId
            };

        _context.Users
            .Add(user);

        await _context
            .SaveChangesAsync();

        // =================================================
        // SAVE IMAGE
        // =================================================

        

        // =================================================
        // CREATE STAFF
        // =================================================

        var staff =
            new Staff
            {
                UserId =
                    user.Id,

                Role =
                    dto.Role,


                IsActive =
                    dto.IsActive,

                HospitalId = hospitalId,
                BranchId = dto.BranchId
            };

        _context.Staffs
            .Add(staff);

        await _context
            .SaveChangesAsync();

        // =================================================
        // RESPONSE
        // =================================================

        return new StaffResponseDto
        {
            Id =
                staff.Id,

            Name =
                user.Name,

            Email =
                user.Email,

            Phone =
                user.MobileNumber,

            Role =
                staff.Role,
            BranchId = staff.BranchId,
            BranchName = (await _context.Branches.FindAsync(staff.BranchId))?.Name,
            HospitalId = staff.HospitalId,

            IsActive =
                staff.IsActive,

        };
    }

    // =====================================================
    // UPDATE STAFF
    // =====================================================

    public async Task<bool>
        UpdateAsync(
            int id,
            CreateStaffDto dto,
            string rootPath,
            int hospitalId)
    {
        var staff =
            await _context.Staffs

                .Include(x =>
                    x.User
                )

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (staff == null)
        {
            return false;
        }

        // =================================================
        // UPDATE USER
        // =================================================

        staff.User.Name =
            dto.Name;

        staff.User.Email =
            dto.Email;

        staff.User.MobileNumber =
            dto.Phone;
        staff.User.BranchId = dto.BranchId;
        // =================================================
        // UPDATE STAFF
        // =================================================

        staff.Role =
            dto.Role;
        staff.BranchId = dto.BranchId;

        staff.IsActive =
            dto.IsActive;

        // =================================================
        // UPDATE PASSWORD
        // =================================================

        if (!string.IsNullOrWhiteSpace(
                dto.Password))
        {
            staff.User.PasswordHash =
                BCrypt.Net.BCrypt
                    .HashPassword(
                        dto.Password
                    );
        }

        // =================================================
        // UPDATE IMAGE
        // =================================================



        await _context
            .SaveChangesAsync();

        return true;
    }



    // =====================================================
    // TOGGLE STAFF STATUS
    // =====================================================

    public async Task<bool>
        ToggleStatusAsync(
            int id,
            int hospitalId)
    {
          var staff = await _context.Staffs
      .Include(x => x.User)
      .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (staff == null)
        {
            return false;
        }

        staff.IsActive =
            !staff.IsActive;
        staff.User.IsActive = staff.IsActive;

        await _context
            .SaveChangesAsync();

        return true;
    }

    // =====================================================
    // DELETE STAFF
    // =====================================================

    public async Task<bool>
        DeleteAsync(
            int id,
            int hospitalId)
    {
        var staff =
            await _context.Staffs

                .Include(x =>
                    x.User
                )

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (staff == null)
        {
            return false;
        }

        // =================================================
        // DELETE USER
        // =================================================

        _context.Users
            .Remove(staff.User);

        // =================================================
        // DELETE STAFF
        // =================================================

        _context.Staffs
            .Remove(staff);

        await _context
            .SaveChangesAsync();

        return true;
    }
}
