using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuthDemo.Data;
using Microsoft.AspNetCore.Mvc;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationController : ControllerBase
{
    // Reuse connections rather than constructing a new HttpClient per request.
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Keep the existing constructor signature for compatibility with DI.
    public LocationController(AppDbContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
    }

    [HttpGet("pincode/{pincode}")]
    public async Task<IActionResult> GetLocation(string pincode, CancellationToken cancellationToken)
    {
        pincode = pincode?.Trim() ?? string.Empty;
        if (pincode.Length != 6 || !pincode.All(char.IsAsciiDigit))
        {
            return BadRequest(new { message = "Enter a valid 6-digit Indian PIN code." });
        }

        try
        {
            using var response = await Http.GetAsync(
                $"https://api.postalpincode.in/pincode/{pincode}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(502, new { message = "Postal lookup service is temporarily unavailable." });
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                return StatusCode(502, new { message = "Unexpected response from postal lookup service." });
            }

            var result = document.RootElement[0];
            if (!result.TryGetProperty("PostOffice", out var offices) ||
                offices.ValueKind != JsonValueKind.Array ||
                offices.GetArrayLength() == 0)
            {
                return NotFound(new { message = "No location found for this PIN code.", pincode });
            }

            var first = offices[0];
            static string Read(JsonElement item, string key)
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty(key, out var value) ||
                    value.ValueKind != JsonValueKind.String)
                    return string.Empty;
                return value.GetString()?.Trim() ?? string.Empty;
            }

            var areas = offices.EnumerateArray()
                .Select(o => Read(o, "Name"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToArray();

            // India Post does not provide a canonical City field. Block may be
            // useful as a locality/town, but is not always a city. Return an
            // empty city when it cannot be reliably determined.
            var block = Read(first, "Block");
            var city = string.Equals(block, "NA", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(block, "N/A", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : block;

            return Ok(new
            {
                pincode,
                areas,
                area = areas.Length == 1 ? areas[0] : string.Empty,
                city,
                district = Read(first, "District"),
                state = Read(first, "State"),
                country = Read(first, "Country")
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { message = "Postal lookup timed out. Please try again." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "Could not connect to postal lookup service." });
        }
        catch (JsonException)
        {
            return StatusCode(502, new { message = "Postal lookup service returned invalid data." });
        }
    }
}
