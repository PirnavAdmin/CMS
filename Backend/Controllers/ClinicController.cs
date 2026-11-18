using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/clinics")]
[Authorize(Roles = "Admin")]
public class ClinicController : ControllerBase
{
    private readonly AppDbContext _context;
    private static readonly HttpClient PostalClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };

    public ClinicController(AppDbContext context) => _context = context;

    private int UserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static string BuildAddress(CreateClinicDto dto, PostalLocation? postal = null) =>
        !string.IsNullOrWhiteSpace(dto.Address) ? dto.Address.Trim() :
        !string.IsNullOrWhiteSpace(dto.Location) ? dto.Location.Trim() :
        string.Join(", ", new[] { postal?.City ?? dto.City, postal?.State ?? dto.State, postal?.Country ?? dto.Country, dto.PostalCode }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

    // Resolve postal details server-side as well; clients must not be trusted to
    // supply the correct state/district for a given PIN.
    private sealed record PostalLocation(string City, string District, string State, string Country);

    private static async Task<(PostalLocation? Location, string? Error, int Status)> LookupPostalAsync(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || !Regex.IsMatch(pin.Trim(), @"^[1-9]\d{5}$"))
            return (null, "Enter a valid 6-digit Indian PIN code.", 400);

        try
        {
            using var response = await PostalClient.GetAsync(
                $"https://api.postalpincode.in/pincode/{pin.Trim()}");
            if (!response.IsSuccessStatusCode)
                return (null, "Postal lookup service is temporarily unavailable.", 503);

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            if (json.RootElement.ValueKind != JsonValueKind.Array ||
                json.RootElement.GetArrayLength() == 0)
                return (null, "Unexpected postal lookup response.", 503);

            var result = json.RootElement[0];
            if (!result.TryGetProperty("Status", out var status) ||
                !string.Equals(status.GetString(), "Success", StringComparison.OrdinalIgnoreCase) ||
                !result.TryGetProperty("PostOffice", out var offices) ||
                offices.ValueKind != JsonValueKind.Array || offices.GetArrayLength() == 0)
                return (null, "No location found for this PIN code.", 400);

            var office = offices[0];
            static string Read(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim() ?? string.Empty : string.Empty;

            var district = Read(office, "District");
            var state = Read(office, "State");
            var country = Read(office, "Country");
            var block = Read(office, "Block");
            var city = !string.IsNullOrWhiteSpace(block) &&
                       !string.Equals(block, "NA", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(block, "Not Available", StringComparison.OrdinalIgnoreCase)
                ? block : district;

            if (string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(state))
                return (null, "Postal lookup did not provide sufficient location details.", 503);

            return (new PostalLocation(city, district, state,
                string.IsNullOrWhiteSpace(country) ? "India" : country), null, 200);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
        {
            return (null, "Postal lookup service is temporarily unavailable. Try again.", 503);
        }
    }

    private Task<User?> CurrentAdminAsync() => _context.Users
        .FirstOrDefaultAsync(x => x.Id == UserId && x.Role == "Admin" && x.IsActive);

    private static bool IsValidDuration(int months) =>
        months == 1 || months == 3 || months == 6 || months == 12;

    private static string? ValidateClinic(CreateClinicDto dto)
    {
        var name = dto.ClinicName?.Trim() ?? string.Empty;
        var email = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var phone = dto.PhoneNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
            !new EmailAddressAttribute().IsValid(email))
            return "Valid clinic name and email are required.";

        if (name.Length > 200 || email.Length > 200)
            return "Clinic name and email must be at most 200 characters.";

        if (!Regex.IsMatch(phone, @"^[6-9]\d{9}$"))
            return "Enter a valid 10-digit Indian clinic phone number.";

        return null;
    }

    // POST /api/clinics
    // The paid purchase becomes active only when the Admin creates the first clinic.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClinicDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Clinic details are required." });

        var validationError = ValidateClinic(dto);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var (postal, postalError, postalStatus) = await LookupPostalAsync(dto.PostalCode);
        if (postalError != null)
            return StatusCode(postalStatus, new { message = postalError });

        if (!dto.IsActive)
            return BadRequest(new { message = "A new clinic must be active for subscription onboarding." });

        if (UserId <= 0) return Unauthorized();

        var name = dto.ClinicName!.Trim();
        var email = dto.Email!.Trim().ToLowerInvariant();

        // One transaction ensures clinic, Admin assignment and subscription
        // are committed together or all rolled back on failure.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var admin = await CurrentAdminAsync();
        if (admin == null) return Unauthorized();

        if (admin.MustChangePassword)
            return Conflict(new
            {
                message = "Change your temporary password before creating your clinic."
            });

        if (admin.HospitalId.HasValue)
            return Conflict(new { message = "This Admin already has a clinic." });

        // Only initial purchases can activate the first clinic.
        // Renewal purchases are never eligible for initial onboarding.
        var purchases = await _context.SubscriptionPurchases
            .Where(x => x.AdminUserId == admin.Id &&
                        x.PaymentStatus == "Paid" && x.PaidAt.HasValue &&
                        x.IsGatewayPaymentVerified && x.GatewayVerifiedAt.HasValue &&
                        (x.GatewayMode == "Test" || x.GatewayMode == "Live") &&
                        x.GatewayOrderId != null && x.GatewayOrderId != "" &&
                        x.GatewayPaymentId != null && x.GatewayPaymentId != "" &&
                        x.AdminCreationStatus == "Created" &&
                        x.PurchaseType == "Initial" &&
                        x.HospitalId == null && x.ActivatedAt == null)
            .OrderBy(x => x.PaidAt)
            .ThenBy(x => x.Id)
            .Take(2)
            .ToListAsync();

        if (purchases.Count == 0)
            return Conflict(new
            {
                message = "No confirmed, unactivated purchase is linked to this Admin."
            });

        if (purchases.Count > 1)
            return Conflict(new
            {
                message = "Multiple unactivated purchases are linked to this Admin. Contact support."
            });

        var purchase = purchases[0];

        if (purchase.Amount <= 0 || !IsValidDuration(purchase.PurchasedDurationMonths))
            return Conflict(new { message = "The purchased subscription details are invalid." });

        if (!purchase.PurchasedIncludesCms)
            return Conflict(new
            {
                message = "The purchased subscription does not include mandatory CMS access."
            });

        var planExists = await _context.SubscriptionPlans.AsNoTracking()
            .AnyAsync(x => x.Id == purchase.SubscriptionPlanId);

        if (!planExists)
            return Conflict(new { message = "The purchased subscription plan no longer exists." });

        // Never use current plan price or module flags here. They may have
        // changed after checkout; the purchase snapshot is authoritative.
        var duplicate = await _context.Hospitals.AnyAsync(x =>
            x.Name.ToLower() == name.ToLower() ||
            (x.Email != null && x.Email.ToLower() == email));

        if (duplicate)
            return Conflict(new { message = "Clinic name or email already exists." });

        var now = DateTime.UtcNow;
        var hospital = new Hospital
        {
            Name = name,
            Email = email,
            Phone = dto.PhoneNumber?.Trim(),
            Address = BuildAddress(dto, postal),
            City = postal!.City,
            State = postal!.State,
            District = postal!.District,
            Country = postal!.Country,
            PostalCode = dto.PostalCode?.Trim(),
            IsActive = true
        };

        _context.Hospitals.Add(hospital);
        await _context.SaveChangesAsync();

        // The first branch is created with the clinic in the same transaction.
        // It counts against the purchased branch limit (minimum plan limit is one).
        _context.Branches.Add(new Branch
        {
            HospitalId = hospital.Id, Name = "Main Branch", Phone = hospital.Phone,
            Email = hospital.Email, Address = hospital.Address, City = hospital.City,
            District = hospital.District, State = hospital.State, Country = hospital.Country,
            PostalCode = hospital.PostalCode, IsActive = true, CreatedAt = now
        });

        admin.HospitalId = hospital.Id;
        purchase.HospitalId = hospital.Id;
        purchase.ActivatedAt = now;

        var subscription = new ClinicSubscription
        {
            HospitalId = hospital.Id,
            SubscriptionPlanId = purchase.SubscriptionPlanId,
            StartDate = now,
            EndDate = now.AddMonths(purchase.PurchasedDurationMonths),
            Amount = purchase.Amount,
            IncludesCms = purchase.PurchasedIncludesCms,
            IncludesLab = purchase.PurchasedIncludesLab,
            IncludesPharmacy = purchase.PurchasedIncludesPharmacy,
            Status = "Active",
            CreatedByUserId = admin.Id,
            CreatedAt = now
        };

        _context.ClinicSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Clinic and Main Branch created; verified paid subscription activated successfully.",
            mainBranchCreated = true,
            id = hospital.Id,
            clinicId = hospital.Id,
            adminUserId = admin.Id,
            purchaseId = purchase.Id,
            subscriptionId = subscription.Id,
            planId = subscription.SubscriptionPlanId,
            amount = subscription.Amount,
            currency = "INR",
            durationMonths = purchase.PurchasedDurationMonths,
            subscriptionStartDate = subscription.StartDate,
            subscriptionEndDate = subscription.EndDate,
            includesCms = subscription.IncludesCms,
            includesLab = subscription.IncludesLab,
            includesPharmacy = subscription.IncludesPharmacy,
            requiresRelogin = false,
            refreshTokenRequired = true
        });
    }

    // GET /api/clinics
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var admin = await CurrentAdminAsync();
        if (admin == null) return Unauthorized();
        if (admin.MustChangePassword)
            return StatusCode(403, new { message = "Change your password before accessing clinic details.", mustChangePassword = true });
        if (!admin.HospitalId.HasValue) return Ok(Array.Empty<object>());

        var data = await _context.Hospitals.AsNoTracking()
            .Where(x => x.Id == admin.HospitalId.Value)
            .Select(x => new
            {
                id = x.Id,
                clinicId = x.Id,
                clinicName = x.Name,
                name = x.Name,
                x.Email,
                phoneNumber = x.Phone,
                x.Address,
                x.City,
                x.State,
                x.District,
                x.Country,
                x.PostalCode,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/clinics/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var admin = await CurrentAdminAsync();
        if (admin == null) return Unauthorized();
        if (admin.MustChangePassword)
            return StatusCode(403, new { message = "Change your password before accessing clinic details.", mustChangePassword = true });
        if (admin.HospitalId != id) return Forbid();

        var clinic = await _context.Hospitals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return clinic == null ? NotFound() : Ok(clinic);
    }

    // PUT /api/clinics/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateClinicDto dto)
    {
        if (dto == null)
            return BadRequest(new { message = "Clinic details are required." });

        var validationError = ValidateClinic(dto);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var (postal, postalError, postalStatus) = await LookupPostalAsync(dto.PostalCode);
        if (postalError != null)
            return StatusCode(postalStatus, new { message = postalError });

        var admin = await CurrentAdminAsync();
        if (admin == null) return Unauthorized();
        if (admin.MustChangePassword)
            return StatusCode(403, new { message = "Change your password before accessing clinic details.", mustChangePassword = true });
        if (admin.HospitalId != id) return Forbid();

        var clinic = await _context.Hospitals.FirstOrDefaultAsync(x => x.Id == id);
        if (clinic == null) return NotFound();

        var name = dto.ClinicName!.Trim();
        var email = dto.Email!.Trim().ToLowerInvariant();

        var duplicate = await _context.Hospitals.AnyAsync(x => x.Id != id &&
            (x.Name.ToLower() == name.ToLower() ||
             (x.Email != null && x.Email.ToLower() == email)));
        if (duplicate)
            return Conflict(new { message = "Clinic name or email already exists." });

        clinic.Name = name;
        clinic.Email = email;
        clinic.Phone = dto.PhoneNumber?.Trim();
        clinic.Address = BuildAddress(dto, postal);
        clinic.City = postal!.City;
        clinic.State = postal!.State;
        clinic.District = postal!.District;
        clinic.Country = postal!.Country;
        clinic.PostalCode = dto.PostalCode?.Trim();
        clinic.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Clinic updated successfully.", clinicId = clinic.Id });
    }
}


