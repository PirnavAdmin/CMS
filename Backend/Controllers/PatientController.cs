using AuthDemo.Authorization;
using AuthDemo.Helpers;

using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuthDemo.Controllers;

[ApiController]
[RequirePermission("Patients")]

[Route("api/[controller]")]
[Authorize]
public class PatientController
    : ControllerBase
{
    private readonly AppDbContext
        _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public PatientController(
        AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }


    // Resolves a PIN during save. The frontend can use GET /api/Location/pincode/{pincode}
    // to populate the address form before submission.
    private async Task<(string? Address, string? Error)> ResolveAddressAsync(CreatePatientDto dto)
    {
        var street = dto.Address?.Trim();
        var pin = dto.PostalCode?.Trim();
        // Backward compatibility: existing patients can still be saved with just Address.
        if (string.IsNullOrWhiteSpace(pin)) return (street, null);
        if (!Regex.IsMatch(pin, @"^[1-9]\d{5}$"))
            return (null, "Enter a valid 6-digit Indian PIN code.");
        try
        {
            using var response = await _httpClientFactory.CreateClient()
                .GetAsync($"https://api.postalpincode.in/pincode/{pin}", HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return (null, "Postal lookup service is unavailable. Please try again.");
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: HttpContext.RequestAborted);
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                return (null, "Postal service returned no results.");
            var item = json.RootElement[0];
            if (!item.TryGetProperty("Status", out var status) || status.GetString() != "Success" ||
                !item.TryGetProperty("PostOffice", out var offices) ||
                offices.ValueKind != JsonValueKind.Array || offices.GetArrayLength() == 0)
                return (null, "PIN code not found.");
            static string Read(JsonElement obj, string key) =>
                obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim() ?? "" : "";
            var first = offices[0];
            var district = Read(first, "District");
            var block = Read(first, "Block");
            var city = string.IsNullOrWhiteSpace(block) || block.Equals("NA", StringComparison.OrdinalIgnoreCase)
                ? district : block;
            var area = dto.Area?.Trim();
            if (!string.IsNullOrWhiteSpace(area) &&
                !offices.EnumerateArray().Any(x => string.Equals(Read(x, "Name"), area, StringComparison.OrdinalIgnoreCase)))
                return (null, "Selected area does not belong to this PIN code.");
            // Area is optional because a PIN can have multiple post offices.
            var parts = new[] { street, area, city, district, Read(first, "State"),
                Read(first, "Country"), pin };
            return (string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)), null);
        }
        catch (OperationCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return (null, "Postal lookup timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            return (null, "Could not contact postal lookup service. Please try again.");
        }
        catch (JsonException)
        {
            return (null, "Postal lookup returned invalid data.");
        }
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

    // =====================================================
    // GET DOCTOR ID
    // =====================================================

    private int GetDoctorId()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "DoctorId"
            );

        if (claim == null)
        {
            return 0;
        }

        return int.Parse(
            claim.Value
        );
    }

    // =====================================================
    // GET ROLE
    // =====================================================

    private string GetRole()
    {
        var claim =
            User.Claims.FirstOrDefault(
                x => x.Type ==
                    "role"
            );

        if (claim == null)
        {
            return "";
        }

        return claim.Value;
    }

    // =====================================================
    // CREATE PATIENT
    // =====================================================

    [Authorize(Roles =
        "Admin,Receptionist")]


    [HttpPost]
    public async Task<IActionResult>
        Create(
            CreatePatientDto dto)
    {
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
        var hospitalId =
            GetHospitalId();

        var patientCode =
            $"P-{new Random()
                .Next(10000, 99999)}";
        var age = DateTime.Today.Year - dto.DateOfBirth.Value.Year;

        if (dto.DateOfBirth.Value.Date >
            DateTime.Today.AddYears(-age))
        {
            age--;
        }

        var (resolvedAddress, addressError) = await ResolveAddressAsync(dto);
        if (addressError != null) return BadRequest(new { message = addressError });

        var patient =
            new Patient
            {
                PatientCode =
                    patientCode,

                Name =
                    dto.Name,

                Phone =
                    dto.Phone,

                Age = age,
                Gender =
                    dto.Gender,

                Email =
                    dto.Email,

                Address =
                     resolvedAddress,

                BloodGroup =
                    dto.BloodGroup,

                DateOfBirth =
                    dto.DateOfBirth,

                EmergencyContactName =
                    dto.EmergencyContactName,

                EmergencyContactPhone =
                    dto.EmergencyContactPhone,

                HospitalId =
                    hospitalId
            };

        _context.Patients
            .Add(patient);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Patient created successfully",

            patientId =
                patient.Id,

            patientCode
        });
    }

    // =====================================================
    // GET ALL PATIENTS
    // =====================================================


    [HttpGet]
    public async Task<IActionResult>
        GetAll()
    {
        var hospitalId =
            GetHospitalId();

        var role =
            GetRole();

        var doctorId =
            GetDoctorId();

        var query =
            _context.Patients

                .Where(x =>
                    x.HospitalId ==
                    hospitalId
                );

        // =========================================
        // DOCTOR ONLY HIS PATIENTS
        // =========================================

        if (role == "Doctor")
        {
            var patientIds =
                await _context.Appointments

                    .Where(x =>
                        x.DoctorId ==
                        doctorId
                    )

                    .Select(x =>
                        x.PatientId
                    )

                    .Distinct()

                    .ToListAsync();

            query =
                query.Where(x =>
                    patientIds
                        .Contains(x.Id)
                );
        }


        var patients =
            await query

                .OrderByDescending(x =>
                    x.CreatedAt
                )

                .Select(x =>
                    new
                    {
                        x.Id,

                        x.PatientCode,

                        x.Name,

                        x.Phone,

                        x.Age,

                        x.Gender,

                        x.Email,

                        x.Address,

                        x.BloodGroup,

                        x.DateOfBirth,

                        x.EmergencyContactName,

                        x.EmergencyContactPhone,

                        lastVisit =
                            _context.Appointments

                                .Where(a =>
                                    a.PatientId ==
                                    x.Id
                                )

                                .OrderByDescending(a =>
                                    a.Date
                                )

                                .Select(a =>
                                    a.Date
                                        .ToString(
                                            "dd MMM yyyy"
                                        )
                                )

                                .FirstOrDefault()
                    })

                .ToListAsync();

        return Ok(patients);


    }

    // =====================================================
    // GET PATIENT DETAILS
    // =====================================================


    [HttpGet("{id}")]
    public async Task<IActionResult>
        GetById(
            int id)
    {
        var hospitalId =
            GetHospitalId();

        var patient =
            await _context.Patients

                .Include(x =>
                    x.MedicalHistories)

                .Include(x =>
                    x.Appointments)

                .ThenInclude(x =>
                    x.Doctor)

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (patient == null)
        {
            return NotFound(
                "Patient not found"
            );
        }

        var prescriptions =
            await _context.Prescriptions

                .Include(x =>
                    x.Medicines)

                .Where(x =>
                    x.PatientId ==
                    id
                )

                .OrderByDescending(x =>
                    x.CreatedAt
                )

                .ToListAsync();

        return Ok(new
        {
            patient.Id,

            patient.PatientCode,

            patient.Name,

            patient.Phone,

            patient.Email,

            patient.Address,

            patient.Age,

            patient.Gender,

            patient.BloodGroup,

            patient.DateOfBirth,

            patient.EmergencyContactName,

            patient.EmergencyContactPhone,

            medicalHistory =
                patient.MedicalHistories

                    .OrderByDescending(x =>
                        x.CreatedAt
                    )

                    .Select(x =>
                        new
                        {
                            x.Allergies,

                            x.ChronicDiseases,

                            x.CurrentMedications,

                            x.Surgeries
                        })

                    .FirstOrDefault(),

            previousVisits =
                patient.Appointments

                    .OrderByDescending(x =>
                        x.Date
                    )

                    .Select(x =>
                        new
                        {
                            appointmentId =
                                x.Id,

                            date =
                                x.Date
                                    .ToString(
                                        "dd MMM yyyy"
                                    ),

                            doctorName =
                                x.Doctor.Name,

                            symptoms =
                                x.ChiefComplaints,

                            status =
                                x.Status
                        }),

            pastPrescriptions =
                prescriptions.Select(x =>
                    new
                    {
                        x.Id,

                        x.Diagnosis,

                        x.Instructions,

                        x.FollowUpDate,

                        x.Status,

                        medicines =
                            x.Medicines.Select(m =>
                                new
                                {
                                    m.MedicineName,

                                    m.Dosage,

                                    m.Frequency,

                                    m.Duration,

                                    m.Notes
                                })
                    })
        });
    }

    // =====================================================
    // UPDATE PATIENT
    // =====================================================

    [Authorize(Roles =
        "Admin,Receptionist")]

    [HttpPut("{id}")]
    public async Task<IActionResult>
        Update(
            int id,

            CreatePatientDto dto)
    {
        var hospitalId =
            GetHospitalId();

        var patient =
            await _context.Patients

                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (patient == null)
        {
            return NotFound(
                "Patient not found"
            );
        }

        var (resolvedAddress, addressError) = await ResolveAddressAsync(dto);
        if (addressError != null) return BadRequest(new { message = addressError });

        patient.Name =
            dto.Name;

        patient.Phone =
            dto.Phone;
        var age = DateTime.Today.Year - dto.DateOfBirth.Value.Year;

        if (dto.DateOfBirth.Value.Date >
            DateTime.Today.AddYears(-age))
        {
            age--;
        }

        patient.Age = age;

        patient.Gender =
            dto.Gender;

        patient.Email =
            dto.Email;

        patient.Address =
             resolvedAddress;

        patient.BloodGroup =
            dto.BloodGroup;

        patient.DateOfBirth =
            dto.DateOfBirth;

        patient.EmergencyContactName =
            dto.EmergencyContactName;

        patient.EmergencyContactPhone =
            dto.EmergencyContactPhone;

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Patient updated successfully"
        });
    }

    // =====================================================
    // DELETE PATIENT
    // =====================================================

    [Authorize(Roles = "Admin,Receptionist")]

    [HttpDelete("{id}")]
    public async Task<IActionResult>
    Delete(int id)
    {
        var hospitalId =
            GetHospitalId();

        // =====================================
        // GET PATIENT
        // =====================================

        var patient =
            await _context.Patients
                .FirstOrDefaultAsync(x =>

                    x.Id == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (patient == null)
        {
            return NotFound(new
            {
                message =
                    "Patient not found"
            });
        }

        // =====================================
        // CHECK APPOINTMENTS
        // =====================================

        var hasAppointments =
            await _context.Appointments
                .AnyAsync(x =>

                    x.PatientId == id &&

                    x.HospitalId ==
                    hospitalId
                );

        if (hasAppointments)
        {
            return BadRequest(new
            {
                message =
                    "Patient cannot be deleted because appointments are associated with this patient."
            });
        }

        // =====================================
        // DELETE PATIENT
        // =====================================

        _context.Patients
            .Remove(patient);

        await _context
            .SaveChangesAsync();

        return Ok(new
        {
            message =
                "Patient deleted successfully"
        });
    }
    [HttpGet("blood-groupsDropdown")]
    public IActionResult GetBloodGroups()
    {
        return Ok(new[]
        {
        "A+","A-",
        "B+","B-",
        "AB+","AB-",
        "O+","O-"
    });
    }

}
