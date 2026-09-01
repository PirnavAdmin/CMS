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
    // GET PUBLIC CLINIC LOGO
    // GET /api/public/clinics/{clinicId}/logo
    // Example:
    // GET /api/public/clinics/3/logo
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

        // Check whether clinic exists and is active
        var clinic = await _context.Hospitals
            .AsNoTracking()
            .Where(x => x.Id == clinicId && x.IsActive)
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

        // Get saved logo from InvoiceSettings
        var invoiceSetting = await _context.InvoiceSettings
            .AsNoTracking()
            .Where(x => x.HospitalId == clinicId)
            .Select(x => new
            {
                x.LogoUrl
            })
            .FirstOrDefaultAsync();

        // Clinic exists but no invoice settings/logo saved
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

        // Clinic logo exists
        return Ok(new
        {
            clinicId = clinic.Id,
            clinicName = clinic.Name,
            logoUrl = invoiceSetting.LogoUrl,
            hasLogo = true
        });
    }
}