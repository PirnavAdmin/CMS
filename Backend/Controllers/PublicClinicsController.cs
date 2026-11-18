
using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/public/clinics")]
[AllowAnonymous]
public class PublicClinicsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PublicClinicsController(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // 1. GET ALL ACTIVE CLINICS
    // GET /api/public/clinics
    // Used by: Patient Portal - Clinic Selection
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetClinics(
        [FromQuery] string? search = null,
        [FromQuery] string? city = null)
    {
        var query = _context.Hospitals
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                (x.City != null &&
                 x.City.Contains(keyword)) ||
                (x.District != null &&
                 x.District.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityFilter = city.Trim();

            query = query.Where(x =>
                x.City != null &&
                x.City.Contains(cityFilter));
        }

        var clinics = await query
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                id = x.Id,
                clinicId = x.Id,
                clinicName = x.Name,
                name = x.Name,
                address = x.Address,
                phone = x.Phone,
                email = x.Email,
                city = x.City,
                state = x.State,
                district = x.District,
                country = x.Country,
                postalCode = x.PostalCode,
                isActive = x.IsActive
            })
            .ToListAsync();

        return Ok(clinics);
    }

    // =====================================================
    // 2. GET CLINIC DETAILS BY ID
    // GET /api/public/clinics/{clinicId}
    // Used by: Patient Portal - Selected Clinic Details
    // =====================================================

    [HttpGet("{clinicId:int}")]
    public async Task<IActionResult> GetClinicById(int clinicId)
    {
        if (clinicId <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid clinic id."
            });
        }

        var clinic = await _context.Hospitals
            .AsNoTracking()
            .Where(x =>
                x.Id == clinicId &&
                x.IsActive)
            .Select(x => new
            {
                id = x.Id,
                clinicId = x.Id,
                clinicName = x.Name,
                name = x.Name,
                address = x.Address,
                phone = x.Phone,
                email = x.Email,
                city = x.City,
                state = x.State,
                district = x.District,
                country = x.Country,
                postalCode = x.PostalCode,
                isActive = x.IsActive
            })
            .FirstOrDefaultAsync();

        if (clinic == null)
        {
            return NotFound(new
            {
                message = "Clinic not found or inactive."
            });
        }

        return Ok(clinic);
    }

    // =====================================================
    // 3. GET PUBLIC CLINIC LOGO
    // GET /api/public/clinics/{clinicId}/logo
    // Used by: Patient Portal - Clinic Logo
    // =====================================================

    [HttpGet("{clinicId:int}/logo")]
    public async Task<IActionResult> GetClinicLogo(int clinicId)
    {
        if (clinicId <= 0)
        {
            return BadRequest(new
            {
                message = "Invalid clinic id."
            });
        }

        var clinic = await _context.Hospitals
            .AsNoTracking()
            .Where(x =>
                x.Id == clinicId &&
                x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .FirstOrDefaultAsync();

        if (clinic == null)
        {
            return NotFound(new
            {
                message = "Clinic not found."
            });
        }

        var invoiceSetting = await _context.InvoiceSettings
            .AsNoTracking()
            .Where(x => x.HospitalId == clinicId)
            .Select(x => new
            {
                x.LogoUrl
            })
            .FirstOrDefaultAsync();

        if (invoiceSetting == null ||
            string.IsNullOrWhiteSpace(invoiceSetting.LogoUrl))
        {
            return Ok(new
            {
                clinicId = clinic.Id,
                clinicName = clinic.Name,
                logoUrl = (string?)null,
                hasLogo = false
            });
        }

        return Ok(new
        {
            clinicId = clinic.Id,
            clinicName = clinic.Name,
            logoUrl = invoiceSetting.LogoUrl,
            hasLogo = true
        });
    }
}
