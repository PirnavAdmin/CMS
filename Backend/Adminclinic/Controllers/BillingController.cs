using AuthDemo.Authorization;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[RequirePermission("Billing")]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,Admin")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _context;

    public BillingController(AppDbContext context)
    {
        _context = context;
    }

    private int HospitalId =>
        int.TryParse(
            User.FindFirstValue("HospitalId"),
            out var id)
            ? id
            : 0;

    private int? BranchId =>
        int.TryParse(
            User.FindFirstValue("BranchId"),
            out var id)
            ? id
            : null;

    private static string? NormalizeBillingType(string? billingType)
    {
        if (string.IsNullOrWhiteSpace(billingType))
        {
            return null;
        }

        var allowedTypes = new[]
        {
            "OP",
            "Lab",
            "Pharmacy"
        };

        return allowedTypes.FirstOrDefault(x =>
            x.Equals(
                billingType.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private IActionResult? ValidateScope()
    {
        if (HospitalId <= 0)
        {
            return Unauthorized(new
            {
                message = "Hospital information is missing from token."
            });
        }

        if (User.IsInRole("Receptionist") &&
            !BranchId.HasValue)
        {
            return Forbid();
        }

        return null;
    }

    // =====================================================
    // GET APPOINTMENTS AVAILABLE FOR BILLING
    // =====================================================

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var query = _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Branch)
            .Where(x =>
                x.HospitalId == HospitalId);

        if (User.IsInRole("Receptionist"))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;

            query = query.Where(x =>
                x.Date.Date == selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var appointments = await query
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.StartTime)
            .Select(x => new
            {
                appointmentId = x.Id,
                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,
                branchName = x.Branch != null
                    ? x.Branch.Name
                    : null,

                date = x.Date,
                startTime = x.StartTime,
                consultationFee = x.ConsultationFee > 0
                    ? x.ConsultationFee
                    : x.Doctor.Fees,
                status = x.Status,
                paymentStatus = x.PaymentStatus
            })
            .ToListAsync();

        return Ok(appointments);
    }

    // =====================================================
    // GET PRESCRIBED MEDICINES FOR PHARMACY BILLING
    // GET /api/Billing/appointments/{appointmentId}/prescription
    // Receptionist is restricted to the assigned branch.
    // Prices are intentionally not invented here because PrescriptionItem
    // currently stores medicine instructions, not medicine selling prices.
    // =====================================================

    [HttpGet("appointments/{appointmentId:int}/prescription")]
    public async Task<IActionResult> GetAppointmentPrescription(int appointmentId)
    {
        var scopeError = ValidateScope();
        if (scopeError != null)
        {
            return scopeError;
        }

        var appointment = await _context.Appointments
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .FirstOrDefaultAsync(x =>
                x.Id == appointmentId &&
                x.HospitalId == HospitalId);

        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (User.IsInRole("Receptionist") &&
            appointment.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .Include(x => x.Medicines)
            .Where(x =>
                x.AppointmentId == appointmentId &&
                x.HospitalId == HospitalId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (prescription == null)
        {
            return NotFound(new
            {
                message = "No prescription was found for this appointment."
            });
        }

        return Ok(new
        {
            prescriptionId = prescription.Id,
            appointmentId = prescription.AppointmentId,
            patientId = appointment.PatientId,
            patientName = appointment.Patient?.Name,
            doctorId = appointment.DoctorId,
            doctorName = appointment.Doctor?.Name,
            branchId = appointment.BranchId,
            diagnosis = prescription.Diagnosis,
            instructions = prescription.Instructions,
            followUpDate = prescription.FollowUpDate,
            status = prescription.Status,
            medicines = prescription.Medicines.Select(m => new
            {
                id = m.Id,
                medicineName = m.MedicineName,
                dosage = m.Dosage,
                frequency = m.Frequency,
                duration = m.Duration,
                notes = m.Notes
            })
        });
    }

    // =====================================================
    // GET BILL BY ID
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBillById(int id)
    {
        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x => x.Id == id && x.HospitalId == HospitalId);

        if (User.IsInRole("Receptionist") && BranchId.HasValue)
        {
            query = query.Where(x => x.BranchId == BranchId.Value);
        }

        var bill = await query
            .Select(x => new
            {
                x.Id,
                x.AppointmentId,
                x.PatientId,
                PatientName = x.Patient.Name,
                x.DoctorId,
                DoctorName = x.Doctor.Name,
                x.BranchId,
                x.BillingType,
                x.ConsultationCharge,
                x.LabCharge,
                x.MedicineCharge,
                x.SubTotal,
                discountPercentage = x.Discount,
                discountAmount = Math.Round(x.SubTotal * x.Discount / 100m, 2),
                taxableAmount = x.SubTotal - Math.Round(x.SubTotal * x.Discount / 100m, 2),
                x.GstPercentage,
                x.GstAmount,
                x.TotalAmount,
                x.PaymentMode,
                x.Status,
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Bill not found."
            });
        }

        return Ok(bill);
    }

    // =====================================================
    // GET BILLS
    // Supports OP, Lab and Pharmacy filters
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetBills(
        [FromQuery] string? billingType = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var normalizedType =
            NormalizeBillingType(billingType);

        if (!string.IsNullOrWhiteSpace(billingType) &&
            normalizedType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x =>
                x.HospitalId == HospitalId);

        if (User.IsInRole("Receptionist"))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (normalizedType != null)
        {
            query = query.Where(x =>
                x.BillingType == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim().ToLower();

            query = query.Where(x =>
                x.Status.ToLower() ==
                normalizedStatus);
        }

        if (date.HasValue)
        {
            var selectedDate =
                date.Value.Date;

            query = query.Where(x =>
                x.CreatedAt.Date ==
                selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var bills = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                billId = x.Id,
                billNumber = "BILL-" + x.Id,

                billingType = x.BillingType,

                appointmentId = x.AppointmentId,
                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,

                consultationCharge =
                    x.ConsultationCharge,

                labCharge =
                    x.LabCharge,

                medicineCharge =
                    x.MedicineCharge,

                subTotal =
                    x.SubTotal,

                discountPercentage =
                    x.Discount,

                discountAmount =
                    Math.Round(x.SubTotal * x.Discount / 100m, 2),

                taxableAmount =
                    x.SubTotal - Math.Round(x.SubTotal * x.Discount / 100m, 2),

                gstPercentage =
                    x.GstPercentage,

                gstAmount =
                    x.GstAmount,

                totalAmount =
                    x.TotalAmount,

                paymentMode =
                    x.PaymentMode,

                status =
                    x.Status,

                createdAt =
                    x.CreatedAt
            })
            .ToListAsync();

        return Ok(bills);
    }

    // =====================================================
    // GET OP BILLS
    // GET /api/Billing/op
    // =====================================================

    [HttpGet("op")]
    public async Task<IActionResult> GetOpBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        return await GetBillsByType(
            "OP",
            status,
            date,
            search);
    }

    // =====================================================
    // GET LAB BILLS
    // GET /api/Billing/lab
    // =====================================================

    [HttpGet("lab")]
    public async Task<IActionResult> GetLabBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        return await GetBillsByType(
            "Lab",
            status,
            date,
            search);
    }

    // =====================================================
    // GET PHARMACY BILLS
    // GET /api/Billing/pharmacy
    // =====================================================

    [HttpGet("pharmacy")]
    public async Task<IActionResult> GetPharmacyBills(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] string? search = null)
    {
        return await GetBillsByType(
            "Pharmacy",
            status,
            date,
            search);
    }

    // =====================================================
    // SHARED GET-BY-TYPE HELPER
    // Not an API endpoint
    // =====================================================

    private async Task<IActionResult> GetBillsByType(
        string billingType,
        string? status = null,
        DateTime? date = null,
        string? search = null)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var normalizedType =
            NormalizeBillingType(billingType);

        if (normalizedType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        var query = _context.Billings
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Appointment)
            .Where(x =>
                x.HospitalId == HospitalId &&
                x.BillingType == normalizedType);

        if (User.IsInRole("Receptionist"))
        {
            query = query.Where(x =>
                x.BranchId == BranchId!.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                status.Trim().ToLower();

            query = query.Where(x =>
                x.Status.ToLower() ==
                normalizedStatus);
        }

        if (date.HasValue)
        {
            var selectedDate =
                date.Value.Date;

            query = query.Where(x =>
                x.CreatedAt.Date ==
                selectedDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(x =>
                x.Patient.Name.Contains(term) ||
                x.Patient.PatientCode.Contains(term) ||
                x.Patient.Phone.Contains(term) ||
                x.Doctor.Name.Contains(term));
        }

        var bills = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                billId = x.Id,
                billNumber = "BILL-" + x.Id,

                billingType = x.BillingType,

                appointmentId = x.AppointmentId,
                appointmentNumber =
                    x.Appointment != null
                        ? x.Appointment.TokenNumber
                        : null,

                patientId = x.PatientId,
                patientName = x.Patient.Name,
                patientCode = x.Patient.PatientCode,
                patientPhone = x.Patient.Phone,

                doctorId = x.DoctorId,
                doctorName = x.Doctor.Name,

                hospitalId = x.HospitalId,
                branchId = x.BranchId,

                consultationCharge =
                    x.ConsultationCharge,

                labCharge =
                    x.LabCharge,

                medicineCharge =
                    x.MedicineCharge,

                subTotal =
                    x.SubTotal,

                discountPercentage =
                    x.Discount,

                discountAmount =
                    Math.Round(x.SubTotal * x.Discount / 100m, 2),

                taxableAmount =
                    x.SubTotal - Math.Round(x.SubTotal * x.Discount / 100m, 2),

                gstPercentage =
                    x.GstPercentage,

                gstAmount =
                    x.GstAmount,

                totalAmount =
                    x.TotalAmount,

                paymentMode =
                    x.PaymentMode,

                status =
                    x.Status,

                createdAt =
                    x.CreatedAt
            })
            .ToListAsync();

        return Ok(bills);
    }

    // =====================================================
    // CREATE OP BILL
    // POST /api/Billing/op
    // =====================================================

    [HttpPost("op")]
    public async Task<IActionResult> CreateOpBill(
        [FromBody] CreateBillingDto dto)
    {
        dto.BillingType = "OP";

        // OP billing should never receive Lab/Pharmacy amounts.
        dto.LabCharge = 0;
        dto.MedicineCharge = 0;

        return await CreateBill(dto);
    }

    // =====================================================
    // CREATE LAB BILL
    // POST /api/Billing/lab
    // =====================================================

    [HttpPost("lab")]
    public async Task<IActionResult> CreateLabBill(
        [FromBody] CreateBillingDto dto)
    {
        dto.BillingType = "Lab";

        // Lab amount is always calculated by the backend
        // from the prescribed LabOrders for this appointment.
        dto.ConsultationCharge = 0;
        dto.LabCharge = 0;
        dto.MedicineCharge = 0;

        return await CreateBill(dto);
    }

    // =====================================================
    // CREATE PHARMACY BILL
    // POST /api/Billing/pharmacy
    // =====================================================

    [HttpPost("pharmacy")]
    public async Task<IActionResult> CreatePharmacyBill(
        [FromBody] CreateBillingDto dto)
    {
        dto.BillingType = "Pharmacy";

        // Pharmacy billing should never receive OP/Lab amounts.
        dto.ConsultationCharge = 0;
        dto.LabCharge = 0;

        return await CreateBill(dto);
    }

    // =====================================================
    // CREATE BILL (GENERIC / BACKWARD COMPATIBILITY)
    // POST /api/Billing
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> CreateBill(
        [FromBody] CreateBillingDto dto)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var billingType =
            NormalizeBillingType(dto.BillingType);

        if (billingType == null)
        {
            return BadRequest(new
            {
                message =
                    "BillingType must be OP, Lab, or Pharmacy."
            });
        }

        if (dto.AppointmentId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Valid AppointmentId is required."
            });
        }

        if (dto.GstPercentage < 0 ||
            dto.GstPercentage > 100)
        {
            return BadRequest(new
            {
                message =
                    "GST percentage must be between 0 and 100."
            });
        }

        var appointment =
            await _context.Appointments
                .Include(x => x.Doctor)
                .Include(x => x.Patient)
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.AppointmentId &&
                    x.HospitalId == HospitalId);

        if (appointment == null)
        {
            return NotFound(new
            {
                message =
                    "Appointment not found."
            });
        }

        if (User.IsInRole("Receptionist") &&
            appointment.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        // Keep one bill of each type per appointment.
        // OP is normally created automatically when consultation payment succeeds.
        // Lab and Pharmacy are created after the doctor orders tests/medicines.
        var existingBill = await _context.Billings
            .AsNoTracking()
            .AnyAsync(x =>
                x.AppointmentId == appointment.Id &&
                x.HospitalId == HospitalId &&
                x.BillingType == billingType);

        if (existingBill)
        {
            return BadRequest(new
            {
                message = $"A {billingType} bill already exists for this appointment. Update or collect payment on the existing bill."
            });
        }

        decimal consultationCharge = 0;
        decimal labCharge = 0;
        decimal medicineCharge = 0;

        switch (billingType)
        {
            case "OP":
                consultationCharge =
                    dto.ConsultationCharge > 0
                        ? dto.ConsultationCharge
                        : appointment.Doctor.Fees;
                break;

            case "Lab":
                // Do not accept the Lab amount from the frontend.
                // Calculate it from the tests prescribed for this appointment.
                labCharge = await _context.LabOrders
                    .AsNoTracking()
                    .Where(x =>
                        x.AppointmentId == appointment.Id &&
                        x.HospitalId == HospitalId &&
                        x.Status != "Cancelled")
                    .SumAsync(x => (decimal?)x.Price) ?? 0m;

                if (labCharge <= 0)
                {
                    return BadRequest(new
                    {
                        message = "No prescribed Lab tests with a valid price were found for this appointment."
                    });
                }

                break;

            case "Pharmacy":
                var hasPrescriptionMedicines = await _context.Prescriptions
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.AppointmentId == appointment.Id &&
                        x.HospitalId == HospitalId &&
                        x.Medicines.Any());

                if (!hasPrescriptionMedicines)
                {
                    return BadRequest(new
                    {
                        message = "No prescribed medicines were found for this appointment."
                    });
                }

                if (dto.MedicineCharge <= 0)
                {
                    return BadRequest(new
                    {
                        message =
                            "Medicine charge must be greater than zero."
                    });
                }

                medicineCharge =
                    dto.MedicineCharge;
                break;
        }

        var subTotal =
            consultationCharge +
            labCharge +
            medicineCharge;

        if (dto.Discount < 0 ||
            dto.Discount > 100)
        {
            return BadRequest(new
            {
                message = "Discount percentage must be between 0 and 100."
            });
        }

        // Discount is stored as percentage.
        // Example: Discount = 10 means 10%.
        var discountPercentage = dto.Discount;

        var discountAmount = Math.Round(
            subTotal *
            discountPercentage /
            100m,
            2);

        var taxableAmount =
            subTotal - discountAmount;

        var gstAmount = Math.Round(
            taxableAmount *
            dto.GstPercentage /
            100m,
            2);

        var totalAmount =
            taxableAmount + gstAmount;

        var status =
            string.IsNullOrWhiteSpace(dto.Status)
                ? "Pending"
                : dto.Status.Trim();

        if (!status.Equals(
                "Pending",
                StringComparison.OrdinalIgnoreCase) &&
            !status.Equals(
                "Paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Status must be Pending or Paid."
            });
        }

        status = status.Equals(
            "Paid",
            StringComparison.OrdinalIgnoreCase)
            ? "Paid"
            : "Pending";

        var bill = new Billing
        {
            AppointmentId =
                appointment.Id,

            PatientId =
                appointment.PatientId,

            DoctorId =
                appointment.DoctorId,

            HospitalId =
                HospitalId,

            BranchId =
                appointment.BranchId,

            BillingType =
                billingType,

            ConsultationCharge =
                consultationCharge,

            LabCharge =
                labCharge,

            MedicineCharge =
                medicineCharge,

            SubTotal =
                subTotal,

            Discount =
                discountPercentage,

            GstPercentage =
                dto.GstPercentage,

            GstAmount =
                gstAmount,

            TotalAmount =
                totalAmount,

            PaymentMode =
                string.IsNullOrWhiteSpace(
                    dto.PaymentMode)
                    ? "Cash"
                    : dto.PaymentMode.Trim(),

            Status =
                status,

            CreatedAt =
                DateTime.UtcNow
        };

        _context.Billings.Add(bill);

        await _context.SaveChangesAsync();

        // Create a patient-specific notification for every generated bill.
        _context.Notifications.Add(new Notification
        {
            PatientId = bill.PatientId,
            Title = $"{bill.BillingType} Bill Generated",
            Message = $"{bill.BillingType} bill BILL-{bill.Id} has been generated. Total amount: ₹{bill.TotalAmount:F2}.",
            IsRead = false,
            IsSent = true,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                $"{billingType} bill created successfully.",

            billId =
                bill.Id,

            billingType =
                bill.BillingType,

            appointmentId =
                bill.AppointmentId,

            patientId =
                bill.PatientId,

            branchId =
                bill.BranchId,

            consultationCharge =
                bill.ConsultationCharge,

            labCharge =
                bill.LabCharge,

            medicineCharge =
                bill.MedicineCharge,

            subTotal =
                bill.SubTotal,

            discountPercentage =
                bill.Discount,

            discountAmount =
                Math.Round(
                    bill.SubTotal *
                    bill.Discount /
                    100m,
                    2),

            taxableAmount =
                bill.SubTotal -
                Math.Round(
                    bill.SubTotal *
                    bill.Discount /
                    100m,
                    2),

            gstPercentage =
                bill.GstPercentage,

            gstAmount =
                bill.GstAmount,

            totalAmount =
                bill.TotalAmount,

            paymentMode =
                bill.PaymentMode,

            status =
                bill.Status
        });
    }

    // =====================================================
    // UPDATE PAYMENT STATUS
    // =====================================================

    [HttpPatch("{billId:int}/payment")]
    public async Task<IActionResult> UpdatePayment(
        int billId,
        [FromBody] PatientPortalPaymentDto dto)
    {
        var scopeError = ValidateScope();

        if (scopeError != null)
        {
            return scopeError;
        }

        var bill = await _context.Billings
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x =>
                x.Id == billId &&
                x.HospitalId == HospitalId);

        if (bill == null)
        {
            return NotFound(new
            {
                message = "Bill not found."
            });
        }

        if (User.IsInRole("Receptionist") &&
            bill.BranchId != BranchId!.Value)
        {
            return Forbid();
        }

        bill.Status = "Paid";

        bill.PaymentMode =
            string.IsNullOrWhiteSpace(dto.PaymentMode)
                ? "Cash"
                : dto.PaymentMode.Trim();

        if (bill.BillingType == "OP" &&
            bill.Appointment != null)
        {
            bill.Appointment.PaymentStatus =
                "Paid";

            if (bill.Appointment.Status ==
                "Pending")
            {
                bill.Appointment.Status =
                    "Confirmed";
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Payment updated successfully.",

            billId =
                bill.Id,

            billingType =
                bill.BillingType,

            status =
                bill.Status,

            paymentMode =
                bill.PaymentMode
        });
    }


    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBill(int id, [FromBody] CreateBillingDto dto)
    {
        var bill = await _context.Billings
            .Include(x => x.Appointment)
            .Include(x => x.Doctor)
            .FirstOrDefaultAsync(x => x.Id == id && x.HospitalId == HospitalId);

        if (bill == null)
            return NotFound(new { message = "Bill not found." });

        if (User.IsInRole("Receptionist") &&
            BranchId.HasValue &&
            bill.BranchId != BranchId.Value)
        {
            return Forbid();
        }

        var allowed = new[] { "OP", "Lab", "Pharmacy" };

        var type = allowed.FirstOrDefault(x =>
            x.Equals(dto.BillingType, StringComparison.OrdinalIgnoreCase));

        if (type == null)
            return BadRequest(new
            {
                message = "BillingType must be OP, Lab or Pharmacy."
            });

        bill.BillingType = type;

        bill.ConsultationCharge = type == "OP"
            ? (dto.ConsultationCharge > 0
                ? dto.ConsultationCharge
                : bill.Doctor?.Fees ?? 0)
            : 0;

        if (type == "Lab")
        {
            var calculatedLabCharge = await _context.LabOrders
                .AsNoTracking()
                .Where(x =>
                    x.AppointmentId == bill.AppointmentId &&
                    x.HospitalId == HospitalId &&
                    x.Status != "Cancelled")
                .SumAsync(x => (decimal?)x.Price) ?? 0m;

            if (calculatedLabCharge <= 0)
            {
                return BadRequest(new
                {
                    message = "No prescribed Lab tests with a valid price were found for this appointment."
                });
            }

            bill.LabCharge = calculatedLabCharge;
        }
        else
        {
            bill.LabCharge = 0;
        }

        if (type == "Pharmacy")
        {
            var hasPrescriptionMedicines = await _context.Prescriptions
                .AsNoTracking()
                .AnyAsync(x =>
                    x.AppointmentId == bill.AppointmentId &&
                    x.HospitalId == HospitalId &&
                    x.Medicines.Any());

            if (!hasPrescriptionMedicines)
            {
                return BadRequest(new
                {
                    message = "No prescribed medicines were found for this appointment."
                });
            }

            if (dto.MedicineCharge <= 0)
            {
                return BadRequest(new
                {
                    message = "Medicine charge must be greater than zero."
                });
            }
        }

        bill.MedicineCharge = type == "Pharmacy"
            ? dto.MedicineCharge
            : 0;

        bill.SubTotal =
            bill.ConsultationCharge +
            bill.LabCharge +
            bill.MedicineCharge;

        if (dto.Discount < 0 ||
            dto.Discount > 100)
        {
            return BadRequest(new
            {
                message = "Discount percentage must be between 0 and 100."
            });
        }

        // Discount is stored as percentage.
        bill.Discount = dto.Discount;
        bill.GstPercentage = dto.GstPercentage;

        var discountAmount = Math.Round(
            bill.SubTotal *
            bill.Discount /
            100m,
            2);

        var taxableAmount =
            bill.SubTotal - discountAmount;

        bill.GstAmount = Math.Round(
            taxableAmount *
            dto.GstPercentage / 100m,
            2);

        bill.TotalAmount =
            taxableAmount +
            bill.GstAmount;

        bill.PaymentMode =
            dto.PaymentMode ?? bill.PaymentMode;

        bill.Status =
            dto.Status;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Bill updated successfully.",
            bill.Id,
            bill.BillingType,
            bill.SubTotal,
            discountPercentage = bill.Discount,
            discountAmount,
            taxableAmount,
            bill.GstPercentage,
            bill.GstAmount,
            bill.TotalAmount,
            bill.Status
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBill(int id)
    {
        var bill = await _context.Billings
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.HospitalId == HospitalId);

        if (bill == null)
            return NotFound(new
            {
                message = "Bill not found."
            });

        if (User.IsInRole("Receptionist") &&
            BranchId.HasValue &&
            bill.BranchId != BranchId.Value)
        {
            return Forbid();
        }

        _context.Billings.Remove(bill);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Bill deleted successfully."
        });
    }
}