
using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/InvoiceSettings")]
[Authorize(Roles = "Admin")]
public class InvoiceSettingsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public InvoiceSettingsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private int GetHospitalId()
    {
        var value = User.FindFirstValue("HospitalId");
        return int.TryParse(value, out var hospitalId) ? hospitalId : 0;
    }

    private IActionResult? ValidateHospital()
    {
        if (GetHospitalId() <= 0)
            return Unauthorized(new { message = "Hospital information is missing from token." });
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var error = ValidateHospital();
        if (error != null) return error;

        var hospitalId = GetHospitalId();
        var hospital = await _context.Hospitals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == hospitalId);

        if (hospital == null)
            return NotFound(new { message = "Clinic not found." });

        var setting = await _context.InvoiceSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.HospitalId == hospitalId);

        if (setting == null)
        {
            return Ok(new
            {
                exists = false,
                hospitalId,
                invoiceTemplate = "Professional",
                headerTitle = hospital.Name,
                headerSubtitle = "",
                clinicAddress = "",
                clinicPhone = "",
                clinicEmail = "",
                gstNumber = "",
                registrationNumber = "",
                footerNote = "Thank you for choosing our clinic. Please retain this invoice for your records.",
                accentColor = "#0f9d9d",
                logoUrl = (string?)null
            });
        }

        return Ok(setting);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceSettingDto dto)
    {
        var error = ValidateHospital();
        if (error != null) return error;

        var hospitalId = GetHospitalId();
        var hospitalExists = await _context.Hospitals.AnyAsync(x => x.Id == hospitalId && x.IsActive);
        if (!hospitalExists)
            return BadRequest(new { message = "Clinic not found or inactive." });

        if (await _context.InvoiceSettings.AnyAsync(x => x.HospitalId == hospitalId))
            return BadRequest(new { message = "Invoice settings already exist for this clinic. Use PUT to update them." });

        if (!IsValidHexColor(dto.AccentColor))
            return BadRequest(new { message = "AccentColor must be a valid hex color such as #0f9d9d." });

        var setting = new InvoiceSetting
        {
            HospitalId = hospitalId,
            InvoiceTemplate = NormalizeTemplate(dto.InvoiceTemplate),
            HeaderTitle = Clean(dto.HeaderTitle),
            HeaderSubtitle = Clean(dto.HeaderSubtitle),
            ClinicAddress = Clean(dto.ClinicAddress),
            ClinicPhone = Clean(dto.ClinicPhone),
            ClinicEmail = Clean(dto.ClinicEmail),
            GstNumber = Clean(dto.GstNumber),
            RegistrationNumber = Clean(dto.RegistrationNumber),
            FooterNote = Clean(dto.FooterNote),
            AccentColor = dto.AccentColor.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.InvoiceSettings.Add(setting);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Invoice settings created successfully.", setting.Id, setting.HospitalId });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateInvoiceSettingDto dto)
    {
        var error = ValidateHospital();
        if (error != null) return error;

        var hospitalId = GetHospitalId();
        var setting = await _context.InvoiceSettings.FirstOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (setting == null)
            return NotFound(new { message = "Invoice settings not found. Create them first." });

        if (!IsValidHexColor(dto.AccentColor))
            return BadRequest(new { message = "AccentColor must be a valid hex color such as #0f9d9d." });

        setting.InvoiceTemplate = NormalizeTemplate(dto.InvoiceTemplate);
        setting.HeaderTitle = Clean(dto.HeaderTitle);
        setting.HeaderSubtitle = Clean(dto.HeaderSubtitle);
        setting.ClinicAddress = Clean(dto.ClinicAddress);
        setting.ClinicPhone = Clean(dto.ClinicPhone);
        setting.ClinicEmail = Clean(dto.ClinicEmail);
        setting.GstNumber = Clean(dto.GstNumber);
        setting.RegistrationNumber = Clean(dto.RegistrationNumber);
        setting.FooterNote = Clean(dto.FooterNote);
        setting.AccentColor = dto.AccentColor.Trim();
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Invoice settings updated successfully." });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var error = ValidateHospital();
        if (error != null) return error;

        var hospitalId = GetHospitalId();
        var setting = await _context.InvoiceSettings.FirstOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (setting == null)
            return NotFound(new { message = "Invoice settings not found." });

        DeleteLogoFile(setting.LogoUrl);
        _context.InvoiceSettings.Remove(setting);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Invoice settings deleted successfully." });
    }

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadLogo(IFormFile logo)
    {
        var error = ValidateHospital();
        if (error != null) return error;

        if (logo == null || logo.Length == 0)
            return BadRequest(new { message = "Logo file is required." });
        if (logo.Length > 2 * 1024 * 1024)
            return BadRequest(new { message = "Logo size must be 2 MB or less." });

        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Only PNG, JPG, JPEG, and WEBP images are allowed." });

        var hospitalId = GetHospitalId();
        var setting = await _context.InvoiceSettings.FirstOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (setting == null)
            return BadRequest(new { message = "Create invoice settings before uploading a logo." });

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _environment.WebRootPath;

        var folder = Path.Combine(webRoot, "uploads", "clinic-logos");
        Directory.CreateDirectory(folder);
        DeleteLogoFile(setting.LogoUrl);

        var fileName = $"clinic-{hospitalId}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await logo.CopyToAsync(stream);

        setting.LogoUrl = $"/uploads/clinic-logos/{fileName}";
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Clinic logo uploaded successfully.", logoUrl = setting.LogoUrl });
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> DeleteLogo()
    {
        var error = ValidateHospital();
        if (error != null) return error;

        var hospitalId = GetHospitalId();
        var setting = await _context.InvoiceSettings.FirstOrDefaultAsync(x => x.HospitalId == hospitalId);
        if (setting == null)
            return NotFound(new { message = "Invoice settings not found." });
        if (string.IsNullOrWhiteSpace(setting.LogoUrl))
            return BadRequest(new { message = "No clinic logo is currently saved." });

        DeleteLogoFile(setting.LogoUrl);
        setting.LogoUrl = null;
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Clinic logo deleted successfully." });
    }

    private void DeleteLogoFile(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl)) return;
        var relativePath = logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _environment.WebRootPath;
        var fullPath = Path.Combine(webRoot, relativePath);
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    private static string NormalizeTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "Professional";
        var allowed = new[] { "Professional", "Modern", "Simple" };
        return allowed.FirstOrDefault(x => x.Equals(template.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Professional";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var color = value.Trim();
        return color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);
    }
}

