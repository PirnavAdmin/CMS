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
    public BillingController(AppDbContext context) => _context = context;
    private int HospitalId => int.TryParse(User.FindFirstValue("HospitalId"), out var id) ? id : 0;
    private int? BranchId => int.TryParse(User.FindFirstValue("BranchId"), out var id) ? id : null;

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments()
    {
        var q = _context.Appointments.AsNoTracking().Include(x=>x.Patient).Include(x=>x.Doctor)
            .Where(x=>x.HospitalId==HospitalId);
        if (User.IsInRole("Receptionist") && BranchId.HasValue) q=q.Where(x=>x.BranchId==BranchId.Value);
        return Ok(await q.OrderByDescending(x=>x.Date).Select(x=>new { x.Id, patientName=x.Patient.Name, doctorName=x.Doctor.Name, x.Date, x.Status, x.BranchId }).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> GetBills([FromQuery] string? billingType = null)
    {
        var q = _context.Billings.AsNoTracking().Include(x=>x.Patient).Include(x=>x.Doctor).Where(x=>x.HospitalId==HospitalId);
        if (BranchId.HasValue && User.IsInRole("Receptionist")) q=q.Where(x=>x.BranchId==BranchId.Value);
        if (!string.IsNullOrWhiteSpace(billingType)) q=q.Where(x=>x.BillingType==billingType);
        return Ok(await q.OrderByDescending(x=>x.CreatedAt).Select(x=>new { x.Id,x.BillingType,patientName=x.Patient.Name,doctorName=x.Doctor.Name,x.SubTotal,x.GstPercentage,x.GstAmount,x.TotalAmount,x.PaymentMode,x.Status,x.CreatedAt }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CreateBill(CreateBillingDto dto)
    {
        var allowed = new[] { "OP", "Lab", "Pharmacy" };
        var type = allowed.FirstOrDefault(x=>x.Equals(dto.BillingType,StringComparison.OrdinalIgnoreCase));
        if (type == null) return BadRequest(new { message = "BillingType must be OP, Lab, or Pharmacy" });
        if (dto.GstPercentage < 0 || dto.GstPercentage > 100) return BadRequest(new { message = "GST percentage must be between 0 and 100" });
        var appointment = await _context.Appointments.Include(x=>x.Doctor).FirstOrDefaultAsync(x=>x.Id==dto.AppointmentId && x.HospitalId==HospitalId);
        if (appointment == null) return NotFound(new { message = "Appointment not found" });
        if (User.IsInRole("Receptionist") && BranchId.HasValue && appointment.BranchId != BranchId.Value) return Forbid();
        decimal consultation = type == "OP" ? (dto.ConsultationCharge > 0 ? dto.ConsultationCharge : appointment.Doctor.Fees) : 0;
        decimal lab = type == "Lab" ? dto.LabCharge : 0;
        decimal medicine = type == "Pharmacy" ? dto.MedicineCharge : 0;
        decimal subtotal = consultation + lab + medicine;
        decimal gst = Math.Round(subtotal * dto.GstPercentage / 100m, 2);
        var bill = new Billing { AppointmentId=appointment.Id,PatientId=appointment.PatientId,DoctorId=appointment.DoctorId,HospitalId=HospitalId,BranchId=appointment.BranchId,BillingType=type,ConsultationCharge=consultation,LabCharge=lab,MedicineCharge=medicine,SubTotal=subtotal,GstPercentage=dto.GstPercentage,GstAmount=gst,TotalAmount=subtotal+gst,PaymentMode=dto.PaymentMode ?? "Cash",Status=dto.Status,CreatedAt=DateTime.UtcNow };
        _context.Billings.Add(bill);
        await _context.SaveChangesAsync();
        return Ok(new { message=$"{type} bill created successfully", bill.Id,bill.BillingType,bill.SubTotal,bill.GstPercentage,bill.GstAmount,bill.TotalAmount });
    }
}
