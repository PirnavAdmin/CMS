using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AuthDemo.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class BranchController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public BranchController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    private int HospitalId =>
        int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private sealed record PostalLocation(string City, string District, string State, string Country);

    // Resolve the tenant from the current active Admin record rather than trusting
    // an unverified or outdated HospitalId claim alone.
    private async Task<int> GetAuthorizedHospitalIdAsync()
    {
        if (CurrentUserId <= 0) return 0;
        var hospitalId = await _context.Users.AsNoTracking()
            .Where(x => x.Id == CurrentUserId && x.IsActive && x.Role == "Admin")
            .Select(x => x.HospitalId)
            .FirstOrDefaultAsync();
        if (!hospitalId.HasValue || hospitalId.Value <= 0) return 0;
        if (HospitalId > 0 && HospitalId != hospitalId.Value) return 0;
        return hospitalId.Value;
    }

    private async Task<(PostalLocation? Location, string? Error)> LookupPostalCodeAsync(string? postalCode)
    {
        var pin = postalCode?.Trim() ?? "";
        if (!Regex.IsMatch(pin, @"^[1-9]\d{5}$"))
            return (null, "Enter a valid 6-digit Indian PIN code.");

        try
        {
            using var response = await _httpClientFactory.CreateClient()
                .GetAsync($"https://api.postalpincode.in/pincode/{pin}", HttpContext.RequestAborted);

            if (!response.IsSuccessStatusCode)
                return (null, "Postal lookup service is temporarily unavailable. Please try again.");

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: HttpContext.RequestAborted);

            if (json.RootElement.ValueKind != JsonValueKind.Array ||
                json.RootElement.GetArrayLength() == 0)
                return (null, "No postal information was returned for this PIN code.");

            var result = json.RootElement[0];
            if (!result.TryGetProperty("Status", out var status) ||
                !string.Equals(status.GetString(), "Success", StringComparison.OrdinalIgnoreCase) ||
                !result.TryGetProperty("PostOffice", out var offices) ||
                offices.ValueKind != JsonValueKind.Array || offices.GetArrayLength() == 0)
                return (null, "PIN code not found.");

            var office = offices[0];
            static string Get(JsonElement obj, string property) =>
                obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim() ?? "" : "";

            var district = Get(office, "District");
            var block = Get(office, "Block");
            var city = !string.IsNullOrWhiteSpace(block) &&
                       !block.Equals("NA", StringComparison.OrdinalIgnoreCase) &&
                       !block.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                ? block : district;

            return (new PostalLocation(city, district, Get(office, "State"), Get(office, "Country")), null);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return (null, "Postal lookup timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            return (null, "Unable to connect to the postal lookup service. Please try again.");
        }
        catch (JsonException)
        {
            return (null, "Postal lookup returned an invalid response. Please try again.");
        }
    }

    // Call only inside a Serializable transaction after locking the Hospital row.
    // The same lock must be used by ALL branch creation/reactivation paths.
    private async Task<(ClinicSubscription? Subscription, int ActiveCount)> GetBranchCapacityAsync(int hospitalId)
    {
        var now = DateTime.UtcNow;
        var subscription = await _context.ClinicSubscriptions.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId && x.Status == "Active" &&
                        x.StartDate <= now && x.EndDate > now && x.IncludesCms &&
                        x.SupersededAt == null)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        var count = await _context.Branches.CountAsync(x => x.HospitalId == hospitalId && x.IsActive);
        return (subscription, count);
    }

    private async Task LockHospitalAsync(int hospitalId)
    {
        // SQL Server transaction-scoped lock serializes writes across all branches
        // of this hospital. This controller already targets SQL Server.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM [Hospitals] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {hospitalId}");
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch([FromBody] BranchDto dto)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return BadRequest(new { message = "Create your clinic first or refresh your login." });
        if (dto == null) return BadRequest(new { message = "Branch details are required." });

        var (location, error) = await LookupPostalCodeAsync(dto.PostalCode);
        if (error != null) return BadRequest(new { message = error });

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockHospitalAsync(hospitalId);
        var (subscription, activeCount) = await GetBranchCapacityAsync(hospitalId);
        if (subscription == null)
            return Conflict(new { message = "An active clinic subscription is required to create branches." });
        if (activeCount >= subscription.PurchasedMaxBranches)
            return Conflict(new
            {
                message = "Active branch limit reached. Upgrade your subscription or deactivate an eligible branch.",
                activeBranches = activeCount,
                maxBranches = subscription.PurchasedMaxBranches
            });

        var branch = new Branch
        {
            HospitalId = hospitalId,
            Name = dto.Name,
            Phone = dto.Phone,
            Email = dto.Email,
            Address = dto.Address,
            City = location!.City,
            District = location.District,
            State = location.State,
            Country = location.Country,
            PostalCode = dto.PostalCode?.Trim(),
            IsActive = true
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(branch);
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches()
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        return Ok(await _context.Branches.AsNoTracking()
            .Where(x => x.HospitalId == hospitalId)
            .OrderByDescending(x => x.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBranch(int id)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        var branch = await _context.Branches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        return branch == null ? NotFound() : Ok(branch);
    }

    [HttpGet("subscription/capacity")]
    public async Task<IActionResult> GetCapacity()
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        var (subscription, activeCount) = await GetBranchCapacityAsync(hospitalId);
        if (subscription == null)
            return Ok(new { subscriptionActive = false, activeBranches = activeCount, availableSlots = 0 });
        return Ok(new
        {
            subscriptionActive = true,
            activeBranches = activeCount,
            maxBranches = subscription.PurchasedMaxBranches,
            availableSlots = Math.Max(0, subscription.PurchasedMaxBranches - activeCount),
            subscriptionId = subscription.Id
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBranch(int id, [FromBody] BranchDto dto)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        if (dto == null) return BadRequest(new { message = "Branch details are required." });
        var branch = await _context.Branches
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        if (branch == null) return NotFound();

        var (location, error) = await LookupPostalCodeAsync(dto.PostalCode);
        if (error != null) return BadRequest(new { message = error });

        branch.Name = dto.Name;
        branch.Phone = dto.Phone;
        branch.Email = dto.Email;
        branch.Address = dto.Address;
        branch.City = location!.City;
        branch.District = location.District;
        branch.State = location.State;
        branch.Country = location.Country;
        branch.PostalCode = dto.PostalCode?.Trim();

        await _context.SaveChangesAsync();
        return Ok(branch);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockHospitalAsync(hospitalId);
        var branch = await _context.Branches
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        if (branch == null) return NotFound();
        // The first branch created during onboarding is the Main Branch.
        // Existing Branch model does not expose a reliable IsMainBranch flag;
        // protect the earliest branch in this clinic.
        var mainBranchId = await _context.Branches
            .Where(x => x.HospitalId == hospitalId)
            .OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
        if (id == mainBranchId)
            return Conflict(new { message = "Main Branch cannot be deactivated." });
        if (!branch.IsActive)
            return Ok(new { message = "Branch is already inactive.", branchId = branch.Id });

        // Prevent orphaning scheduled future appointments before deactivation.
        var today = DateTime.Today;
        var hasFutureAppointments = await _context.Appointments.AsNoTracking()
            .AnyAsync(x => x.HospitalId == hospitalId && x.BranchId == id && x.Date >= today &&
                           x.Status != "Cancelled" && x.Status != "Canceled");
        if (hasFutureAppointments)
            return Conflict(new { message = "This branch has current or future non-cancelled appointments. Reschedule or resolve them before deactivation." });

        branch.IsActive = false;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Branch deactivated successfully.", branchId = branch.Id });
    }

    [HttpPut("{id:int}/reactivate")]
    public async Task<IActionResult> ReactivateBranch(int id)
    {
        var hospitalId = await GetAuthorizedHospitalIdAsync();
        if (hospitalId <= 0) return Forbid();
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockHospitalAsync(hospitalId);
        var branch = await _context.Branches
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == hospitalId);
        if (branch == null) return NotFound();
        if (branch.IsActive)
            return Ok(new { message = "Branch is already active.", branchId = branch.Id });

        var (subscription, activeCount) = await GetBranchCapacityAsync(hospitalId);
        if (subscription == null)
            return Conflict(new { message = "An active clinic subscription is required to reactivate branches." });
        if (activeCount >= subscription.PurchasedMaxBranches)
            return Conflict(new
            {
                message = "Active branch limit reached. Upgrade your subscription or deactivate another eligible branch.",
                activeBranches = activeCount,
                maxBranches = subscription.PurchasedMaxBranches
            });

        branch.IsActive = true;
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { message = "Branch reactivated successfully.", branchId = branch.Id });
    }
}
