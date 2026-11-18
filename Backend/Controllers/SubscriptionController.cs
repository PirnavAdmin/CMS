using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public SubscriptionController(AppDbContext context, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
    }

    // Subscription dashboard alerts are exclusively targeted at SuperAdmin.
    // They are saved in the same transaction as the triggering change whenever possible.
    private void QueueSuperAdminSubscriptionNotification(string title, string message)
    {
        _context.Notifications.Add(new Notification
        {
            Title = title,
            Message = message,
            TargetRoles = "SuperAdmin",
            TargetUsers = null,
            PatientId = null,
            IsRead = false,
            IsSent = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    // ============================================================
    // PAYMENTS - REQUIRE VERIFIED CMS RAZORPAY PAYMENT
    // A simulated Paid status is not proof of captured payment.
    // This guard must be used by both renewal and upgrade activation.
    // ============================================================
    private static bool HasVerifiedCmsGatewayPayment(SubscriptionPurchase purchase)
    {
        return purchase.PaymentStatus == "Paid"
            && purchase.PaidAt.HasValue
            && purchase.IsGatewayPaymentVerified
            && purchase.GatewayVerifiedAt.HasValue
            && (purchase.GatewayMode == "Test" || purchase.GatewayMode == "Live")
            && !string.IsNullOrWhiteSpace(purchase.GatewayOrderId)
            && !string.IsNullOrWhiteSpace(purchase.GatewayPaymentId);
    }

    // ============================================================
    // PUBLIC - ACTIVE SUBSCRIPTION PLANS (NO LOGIN REQUIRED)
    // GET /api/subscriptions/public/plans
    // ============================================================

    [AllowAnonymous]
    [HttpGet("public/plans")]
    public async Task<IActionResult> GetPublicPlans()
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(x => x.IsActive && x.IncludesCms && x.Price > 0 &&
                        (x.DurationMonths == 1 || x.DurationMonths == 3 ||
                         x.DurationMonths == 6 || x.DurationMonths == 12))
            .OrderBy(x => x.Price)
            .ThenBy(x => x.DurationMonths)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                planType = x.PlanType,
                price = x.Price,
                durationMonths = x.DurationMonths,
                includesCms = x.IncludesCms,
                includesLab = x.IncludesLab,
                includesPharmacy = x.IncludesPharmacy,
                maxBranches = x.MaxBranches,
                dailyAppointmentLimit = x.DailyAppointmentLimit
            })
            .ToListAsync();

        return Ok(plans);
    }

    // ============================================================
    // SUPER ADMIN - GET ALL SUBSCRIPTION PLANS
    // ============================================================

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("plans")]
    [HttpGet("admin/plans")]
    public async Task<IActionResult> Plans()
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(x => x.PlanType)
            .ThenBy(x => x.DurationMonths)
            .ThenBy(x => x.Price)
            .ToListAsync();

        return Ok(plans);
    }

    // ============================================================
    // SUPER ADMIN - CREATE SUBSCRIPTION PLAN
    // ============================================================

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("plans")]
    [HttpPost("admin/plans")]
    public async Task<IActionResult> CreatePlan(
        PlanUpsertDto dto)
    {
        var validation = ValidatePlan(dto);
        if (validation != null) return BadRequest(new { message = validation });

        // Database unique index must cover PlanType + all module flags + duration.
        // Serializable isolation adds protection against concurrent requests.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);

        var planType = NormalizePlanType(dto.PlanType)!;
        if (await _context.SubscriptionPlans.AnyAsync(x =>
            x.PlanType == planType &&
            x.IncludesCms == dto.IncludesCms &&
            x.IncludesLab == dto.IncludesLab &&
            x.IncludesPharmacy == dto.IncludesPharmacy &&
            x.DurationMonths == dto.DurationMonths))
            return Conflict(new { message = "This plan type, module combination and duration already exists. Update its price instead." });

        var plan = new SubscriptionPlan
        {
            Name = dto.Name.Trim(),
            PlanType = planType,
            DurationMonths = dto.DurationMonths,
            IncludesCms = true,
            IncludesLab = dto.IncludesLab,
            IncludesPharmacy = dto.IncludesPharmacy,
            Price = dto.Price,
            MaxBranches = dto.MaxBranches,
            DailyAppointmentLimit = dto.DailyAppointmentLimit,
            IsActive = dto.IsActive
        };

        _context.SubscriptionPlans.Add(plan);

        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsPlanDuplicateKeyViolation(ex))
        {
            await transaction.RollbackAsync();
            return Conflict(new
            {
                message = "This plan type, module combination and duration already exists. Update its price instead."
            });
        }

        return Ok(plan);
    }

    // ============================================================
    // SUPER ADMIN - UPDATE SUBSCRIPTION PLAN
    // ============================================================

    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("plans/{id:int}")]
    [HttpPut("admin/plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(
        int id,
        PlanUpsertDto dto)
    {
        var plan = await _context.SubscriptionPlans
            .FindAsync(id);

        if (plan == null)
        {
            return NotFound(new
            {
                message = "Subscription plan not found."
            });
        }

        var validation = ValidatePlan(dto);
        if (validation != null) return BadRequest(new { message = validation });

        // Purchased plans must not be repurposed into a different module combination.
        // Keep their original duration and entitlements for historical references.
        if (!string.Equals(plan.PlanType, NormalizePlanType(dto.PlanType), StringComparison.OrdinalIgnoreCase) ||
            !plan.IncludesCms ||
            plan.DurationMonths != dto.DurationMonths ||
            plan.IncludesLab != dto.IncludesLab ||
            plan.IncludesPharmacy != dto.IncludesPharmacy)
            return Conflict(new { message = "Plan type, module combination and duration cannot be changed. Edit price/status or create another plan." });

        plan.Name = dto.Name.Trim();
        plan.Price = dto.Price;
        plan.MaxBranches = dto.MaxBranches;
        plan.DailyAppointmentLimit = dto.DailyAppointmentLimit;
        plan.IsActive = dto.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsPlanDuplicateKeyViolation(ex))
        {
            return Conflict(new
            {
                message = "This plan type, module combination and duration already exists. Update its price instead."
            });
        }

        return Ok(plan);
    }

    // SUPER ADMIN - UPDATE ONLY PRICE / ACTIVE STATUS
    [Authorize(Roles = "SuperAdmin")]
    [HttpPut("admin/plans/{id:int}/price")]
    public async Task<IActionResult> UpdatePlanPrice(int id, [FromBody] PlanPriceDto dto)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(id);
        if (plan == null) return NotFound(new { message = "Plan not found." });
        if (dto == null || dto.Price < 0.01m || dto.Price > 999999999m)
            return BadRequest(new { message = "Price must be between 0.01 and 999999999." });
        if (!plan.IncludesCms || !IsValidDuration(plan.DurationMonths))
            return Conflict(new { message = "This legacy plan is not a valid CMS subscription offering." });
        plan.Price = dto.Price;
        if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;
        plan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(plan);
    }

    public sealed class PlanPriceDto
    {
        public decimal Price { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class PlanUpsertDto
    {
        public string PlanType { get; set; } = "Basic";
        public string Name { get; set; } = string.Empty;
        public int DurationMonths { get; set; }
        public bool IncludesCms { get; set; } = true;
        public bool IncludesLab { get; set; }
        public bool IncludesPharmacy { get; set; }
        public decimal Price { get; set; }
        public int MaxBranches { get; set; } = 1;
        public int DailyAppointmentLimit { get; set; } = 20;
        public bool IsActive { get; set; } = true;
    }

    private static string? ValidatePlan(PlanUpsertDto? dto)
    {
        if (dto == null) return "Plan details are required.";
        if (NormalizePlanType(dto.PlanType) == null)
            return "Plan type must be Basic, Super, or Premium.";
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 120)
            return "Plan name is required (maximum 120 characters).";
        if (dto.DurationMonths != 1 && dto.DurationMonths != 3 &&
            dto.DurationMonths != 6 && dto.DurationMonths != 12)
            return "Duration must be 1, 3, 6, or 12 months.";
        if (!dto.IncludesCms) return "CMS is mandatory for every plan.";
        if (dto.MaxBranches < 1 || dto.MaxBranches > 10000) return "MaxBranches must be between 1 and 10000.";
        if (dto.DailyAppointmentLimit < 1 || dto.DailyAppointmentLimit > 1000000) return "DailyAppointmentLimit must be between 1 and 1000000.";
        if (dto.Price < 0.01m || dto.Price > 999999999m)
            return "Price must be between 0.01 and 999999999.";
        return null;
    }

    // The database unique index is the final guard against simultaneous inserts.
    // Only translate violations of the subscription-plan combination index into 409;
    // other database failures must not be misreported as duplicate plans.
    private static bool IsPlanDuplicateKeyViolation(DbUpdateException exception)
    {
        var sqlException = exception.GetBaseException() as Microsoft.Data.SqlClient.SqlException;
        if (sqlException == null || (sqlException.Number != 2601 && sqlException.Number != 2627))
            return false;

        return sqlException.Message.Contains(
            "IX_SubscriptionPlans_PlanType_IncludesCms_IncludesLab_IncludesPharmacy_DurationMonths",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePlanType(string? value)
    {
        if (string.Equals(value?.Trim(), "Basic", StringComparison.OrdinalIgnoreCase)) return "Basic";
        if (string.Equals(value?.Trim(), "Super", StringComparison.OrdinalIgnoreCase)) return "Super";
        if (string.Equals(value?.Trim(), "Premium", StringComparison.OrdinalIgnoreCase)) return "Premium";
        return null;
    }

    // ============================================================
    // SUPER ADMIN - GET ALL CLINIC SUBSCRIPTIONS
    // ============================================================

    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("all")]
    public async Task<IActionResult> All()
    {
        var subscriptions =
            await _context.ClinicSubscriptions
                .AsNoTracking()

                .Include(x => x.Hospital)

                .Include(x => x.SubscriptionPlan)

                .OrderByDescending(x => x.CreatedAt)

                .Select(x => new
                {
                    x.Id,

                    x.HospitalId,

                    clinicName =
                        x.Hospital != null
                            ? x.Hospital.Name
                            : null,

                    x.SubscriptionPlanId,

                    planName =
                        x.SubscriptionPlan != null
                            ? x.SubscriptionPlan.Name
                            : null,
                    planType = x.SubscriptionPlan != null ? x.SubscriptionPlan.PlanType : null,

                    x.StartDate,

                    x.EndDate,

                    x.Amount,

                    x.Status,

                    x.IncludesCms,
                    x.IncludesLab,
                    x.IncludesPharmacy,
                    x.PurchasedMaxBranches,
                    x.PurchasedDailyAppointmentLimit
                })

                .ToListAsync();

        return Ok(subscriptions);
    }

    // ============================================================
    // CLINIC USERS - GET CURRENT CLINIC SUBSCRIPTION
    // ============================================================

    [Authorize(
        Roles = "Admin,Doctor,Receptionist,LabTechnician")]
    [HttpGet("my")]
    public async Task<IActionResult> My()
    {
        // --------------------------------------------------------
        // Get HospitalId from JWT
        // --------------------------------------------------------

        var userId = GetCurrentUserId();
        var currentUser = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);
        var hospitalId = currentUser?.HospitalId ?? 0;

        if (hospitalId <= 0)
        {
            return Ok(new
            {
                active = false,

                onboardingRequired = true,

                includesCms = false,
                includesLab = false,
                includesPharmacy = false,

                message =
                    "Clinic assignment not found."
            });
        }

        var now = DateTime.UtcNow;

        // --------------------------------------------------------
        // Find ACTIVE + CURRENT subscription
        // --------------------------------------------------------

        var subscription =
            await _context.ClinicSubscriptions
                .AsNoTracking()

                .Include(x => x.SubscriptionPlan)

                .Where(x =>
                    x.HospitalId == hospitalId &&

                    x.Status == "Active" &&

                    x.StartDate <= now &&

                    x.EndDate > now)

                .OrderByDescending(x => x.CreatedAt)

                .Select(x => new
                {
                    active = true,

                    subscriptionId = x.Id,

                    hospitalId = x.HospitalId,

                    x.StartDate,

                    x.EndDate,

                    x.Amount,

                    x.Status,

                    // Frontend uses this value
                    // to show/hide Lab module.
                    includesCms = x.IncludesCms,
                    includesLab = x.IncludesLab,
                    includesPharmacy = x.IncludesPharmacy,
                    maxBranches = x.PurchasedMaxBranches,
                    dailyAppointmentLimit = x.PurchasedDailyAppointmentLimit,

                    planId =
                        x.SubscriptionPlanId,

                    planName =
                        x.SubscriptionPlan != null
                            ? x.SubscriptionPlan.Name
                            : null,
                    planType = x.SubscriptionPlan != null ? x.SubscriptionPlan.PlanType : null
                })

                .FirstOrDefaultAsync();

        // --------------------------------------------------------
        // No active/current subscription
        // --------------------------------------------------------

        if (subscription == null)
        {
            return Ok(new
            {
                active = false,

                onboardingRequired = false,

                includesCms = false,
                includesLab = false,
                includesPharmacy = false,

                message =
                    "No active subscription assigned to this clinic."
            });
        }

        // --------------------------------------------------------
        // Active subscription found
        // --------------------------------------------------------

        return Ok(subscription);
    }

    // ============================================================
    // CLINIC SUBSCRIPTION DETAILS BY HOSPITAL ID
    // GET /api/clinic-subscriptions/{hospitalId}
    // Compatibility endpoint for the Admin Console.
    // ============================================================
    [Authorize(Roles = "SuperAdmin,Admin,Doctor,Receptionist,LabTechnician")]
    [HttpGet("~/api/clinic-subscriptions/{hospitalId:int}")]
    public async Task<IActionResult> GetClinicSubscription(int hospitalId)
    {
        if (hospitalId <= 0)
            return BadRequest(new { message = "Valid hospital ID is required." });

        // A clinic user may read only their own clinic subscription.
        // SuperAdmin can inspect any clinic.
        if (!User.IsInRole("SuperAdmin"))
        {
            var assignedHospitalId = await _context.Users.AsNoTracking()
                .Where(x => x.Id == GetCurrentUserId() && x.IsActive)
                .Select(x => x.HospitalId)
                .FirstOrDefaultAsync();
            if (assignedHospitalId != hospitalId)
            {
                return Forbid();
            }
        }

        var now = DateTime.UtcNow;
        var subscription = await _context.ClinicSubscriptions
            .AsNoTracking()
            .Where(x => x.HospitalId == hospitalId &&
                        x.Status == "Active" &&
                        x.StartDate <= now &&
                        x.EndDate > now)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                id = x.Id,
                subscriptionId = x.Id,
                hospitalId = x.HospitalId,
                clinicName = x.Hospital != null ? x.Hospital.Name : null,
                subscriptionPlanId = x.SubscriptionPlanId,
                planId = x.SubscriptionPlanId,
                planName = x.SubscriptionPlan != null ? x.SubscriptionPlan.Name : null,
                planType = x.SubscriptionPlan != null ? x.SubscriptionPlan.PlanType : null,
                x.StartDate,
                x.EndDate,
                x.Amount,
                x.Status,
                x.IncludesCms,
                x.IncludesLab,
                x.IncludesPharmacy,
                maxBranches = x.PurchasedMaxBranches,
                dailyAppointmentLimit = x.PurchasedDailyAppointmentLimit,
                active = true
            })
            .FirstOrDefaultAsync();

        if (subscription == null)
            return NotFound(new { message = "No current active subscription found for this clinic." });

        return Ok(subscription);
    }


    // ============================================================
    // PUBLIC - CREATE DIRECT SUBSCRIPTION CHECKOUT
    // POST /api/subscriptions/checkout
    // This records a pending purchase. It DOES NOT process or verify payment.
    // ============================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] DirectSubscriptionCheckoutDto dto)
    {
        if (dto == null) return BadRequest(new { message = "Checkout details are required." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized(new { message = "Invalid Admin token." });
        var admin = await _context.Users.FirstOrDefaultAsync(x => x.Id == adminId && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.EmailVerified) return StatusCode(403, new { message = "Verify your email before purchasing a subscription." });
        if (admin.HospitalId.HasValue) return Conflict(new { message = "This Admin already has a clinic. Use renewal or upgrade checkout." });
        if (dto.SubscriptionPlanId <= 0) return BadRequest(new { message = "Valid subscription plan is required." });
        var method = NormalizePaymentMethod(dto.PaymentMethod);
        if (method == null) return BadRequest(new { message = "Payment method must be UPI, Card, or NetBanking." });
        var plan = await _context.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.SubscriptionPlanId && x.IsActive);
        if (plan == null) return NotFound(new { message = "Active subscription plan not found." });
        if (plan.Price <= 0 || !IsValidDuration(plan.DurationMonths) || !plan.IncludesCms)
            return BadRequest(new { message = "Selected subscription plan is not available for checkout." });
        var existing = await _context.SubscriptionPurchases.FirstOrDefaultAsync(x => x.AdminUserId == admin.Id && x.PurchaseType == "Initial" && x.HospitalId == null && x.ActivatedAt == null);
        if (existing != null)
            return Conflict(new { message = "An Initial subscription purchase already exists. Retry or complete that purchase instead of creating another.", purchaseId = existing.Id, paymentStatus = existing.PaymentStatus });
        var purchase = new SubscriptionPurchase
        {
            Name = admin.Name ?? string.Empty, Email = admin.Email ?? string.Empty, MobileNumber = admin.MobileNumber ?? string.Empty,
            AdminUserId = admin.Id, AdminCreationStatus = "Created",
            SubscriptionPlanId = plan.Id, PurchaseType = "Initial", PurchasedDurationMonths = plan.DurationMonths,
            PurchasedIncludesCms = true, PurchasedIncludesLab = plan.IncludesLab, PurchasedIncludesPharmacy = plan.IncludesPharmacy,
            PurchasedMaxBranches = plan.MaxBranches, PurchasedDailyAppointmentLimit = plan.DailyAppointmentLimit,
            Amount = plan.Price, GrossNewPlanAmount = plan.Price, FinalPayableAmount = plan.Price,
            PaymentMethod = method, PaymentStatus = "Pending", CreatedAt = DateTime.UtcNow
        };
        _context.SubscriptionPurchases.Add(purchase);
        await _context.SaveChangesAsync();
        QueueSuperAdminSubscriptionNotification("Initial subscription checkout created", $"Admin {admin.Email} created Initial purchase #{purchase.Id} for plan {plan.Name}; payment pending.");
        await _context.SaveChangesAsync();
        return StatusCode(201, new
        {
            message = "Initial subscription purchase created. Continue to Razorpay payment.",
            purchaseId = purchase.Id, planId = plan.Id, planName = plan.Name, planType = plan.PlanType,
            durationMonths = plan.DurationMonths, includesCms = true, includesLab = plan.IncludesLab,
            includesPharmacy = plan.IncludesPharmacy, maxBranches = purchase.PurchasedMaxBranches,
            dailyAppointmentLimit = purchase.PurchasedDailyAppointmentLimit, amount = purchase.Amount,
            currency = "INR", paymentMethod = purchase.PaymentMethod, paymentStatus = purchase.PaymentStatus, paymentGatewayReady = true
        });
    }

    public sealed class DirectSubscriptionCheckoutDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public int SubscriptionPlanId { get; set; }
        public string? PaymentMethod { get; set; }
    }


    // ============================================================
    // PUBLIC - LIST PAYMENT METHODS (GATEWAY NOT YET CONFIGURED)
    // GET /api/subscriptions/payment-methods
    // ============================================================
    [AllowAnonymous]
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var settings = await _context.CmsOwnerMerchantSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1);
        var mode = settings?.Mode;
        var prefix = $"Payments:CmsOwner:Razorpay:{mode}:";
        var configured = settings != null && settings.IsEnabled && settings.KycStatus == "Approved" && !string.IsNullOrWhiteSpace(settings.ProviderMerchantAccountId) &&
            (mode == "Test" || mode == "Live") &&
            !string.IsNullOrWhiteSpace(_configuration[prefix + "KeyId"]) &&
            !string.IsNullOrWhiteSpace(_configuration[prefix + "KeySecret"]);
        return Ok(new
        {
            gatewayConfigured = configured,
            merchant = "CMS Owner",
            mode = configured ? mode : null,
            paymentMethods = new[]
            {
                new { code = "UPI", name = "UPI", enabled = configured, liveProcessingAvailable = configured && mode == "Live" },
                new { code = "Card", name = "Debit / Credit Card", enabled = configured, liveProcessingAvailable = configured && mode == "Live" },
                new { code = "NetBanking", name = "Net Banking", enabled = configured, liveProcessingAvailable = configured && mode == "Live" }
            }
        });
    }

    // ============================================================
    // PUBLIC - DEVELOPMENT-ONLY DUMMY PAYMENT SUCCESS / FAILURE
    // POST /api/subscriptions/payments/initiate
    // Does not collect money. Failed purchases may be retried.
    // ============================================================
    // Development simulation is restricted to SuperAdmin. Never expose a
    // public endpoint that can mark an arbitrary purchase as Paid.
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("payments/initiate")]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiateSubscriptionPaymentDto dto)
    {
        if (dto == null || dto.PurchaseId <= 0)
            return BadRequest(new { message = "Valid purchase ID is required." });

        // Never simulate a payment outside Development, including repeat calls.
        if (!_environment.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Dummy payments are available only in Development. No money was collected."
            });

        var method = NormalizePaymentMethod(dto.PaymentMethod);
        if (method == null)
            return BadRequest(new { message = "Payment method must be UPI, Card, or NetBanking." });

        var outcome = dto.SimulateResult?.Trim();
        if (!string.Equals(outcome, "Success", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "simulateResult must be Success or Failed." });

        // Lock the purchase row until the transaction completes to avoid two
        // concurrent requests both confirming a payment.
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var purchase = await _context.SubscriptionPurchases
            .FromSqlInterpolated($"SELECT * FROM [SubscriptionPurchases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {dto.PurchaseId}")
            .FirstOrDefaultAsync();

        if (purchase == null)
            return NotFound(new { message = "Subscription purchase not found." });

        if (!string.Equals(purchase.PaymentMethod, method, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Payment method does not match checkout selection." });

        if (purchase.PaymentStatus == "Paid")
            return Conflict(new
            {
                purchaseId = purchase.Id,
                paymentStatus = purchase.PaymentStatus,
                paidAt = purchase.PaidAt,
                message = "This purchase is already paid. Another payment cannot be simulated."
            });

        if (purchase.PaymentStatus != "Pending" && purchase.PaymentStatus != "Failed")
            return Conflict(new { message = "Only pending or failed purchases can be simulated." });

        var success = string.Equals(outcome, "Success", StringComparison.OrdinalIgnoreCase);
        var attemptedAt = DateTime.UtcNow;
        var reference = $"SIM-{attemptedAt:yyyyMMddHHmmss}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";

        purchase.PaymentAttemptCount += 1;
        purchase.LastPaymentAttemptAt = attemptedAt;
        purchase.TransactionReference = reference;
        purchase.PaymentFailureReason = success ? null : "Simulated payment failure (Development only).";
        purchase.PaymentStatus = success ? "Paid" : "Failed";
        purchase.PaidAt = success ? attemptedAt : null;
        // A simulated transaction is not a verified external gateway transaction.
        purchase.GatewayPaymentId = null;
        QueueSuperAdminSubscriptionNotification(
            success ? "Subscription Demo Payment Successful" : "Subscription Demo Payment Failed",
            $"Subscription purchase #{purchase.Id} ({purchase.PurchaseType}) for {purchase.Name}: " +
            $"{(success ? "simulated payment succeeded" : "simulated payment failed")}. " +
            $"Amount: ₹{purchase.Amount:F2}. Development simulation only; no real payment collected.");
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            purchaseId = purchase.Id,
            amount = purchase.Amount,
            currency = "INR",
            paymentMethod = purchase.PaymentMethod,
            paymentStatus = purchase.PaymentStatus,
            paidAt = purchase.PaidAt,
            transactionReference = purchase.TransactionReference,
            paymentFailureReason = purchase.PaymentFailureReason,
            paymentAttemptCount = purchase.PaymentAttemptCount,
            lastPaymentAttemptAt = purchase.LastPaymentAttemptAt,
            simulatedPayment = true,
            realPaymentCollected = false,
            gatewayConfigured = false,
            paymentGatewayReady = false,
            liveProcessingAvailable = false,
            canRetry = !success,
            eligibleForAdminCreation = success && purchase.PurchaseType == "Initial" && purchase.AdminCreationStatus == "Pending",
            message = success
                ? "Dummy payment successful. No real money collected. Awaiting Super Admin account creation."
                : "Dummy payment failed. No real money collected. Retry using the same purchase ID."
        });
    }

    // ============================================================
    // PUBLIC DEVELOPMENT SIMULATION: records Paid or Failed.
    // Simulated Paid is NOT a verified real payment; never expose in production.
    // POST /api/subscriptions/demo/payments/confirm
    // ============================================================
    [AllowAnonymous]
    [HttpPost("demo/payments/confirm")]
    public async Task<IActionResult> ConfirmDemoPayment([FromBody] ConfirmDemoPaymentDto dto)
    {
        if (!DemoPreviewEnabled())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Payment simulation is disabled." });

        if (dto == null || dto.PurchaseId <= 0 || string.IsNullOrWhiteSpace(dto.DemoToken))
            return BadRequest(new { message = "Purchase ID and demo token are required." });

        var method = NormalizePaymentMethod(dto.PaymentMethod);
        if (method == null)
            return BadRequest(new { message = "Payment method must be UPI, Card, or NetBanking." });

        var success = string.Equals(dto.SimulateResult?.Trim(), "Success", StringComparison.OrdinalIgnoreCase);
        var failed = string.Equals(dto.SimulateResult?.Trim(), "Failed", StringComparison.OrdinalIgnoreCase);
        if (!success && !failed)
            return BadRequest(new { message = "simulateResult must be Success or Failed." });

        // Restrict client-supplied reasons to a short set of safe demo codes.
        var failureReason = dto.FailureReason?.Trim();
        var permittedReasons = new[] { "SIMULATED_BANK_DECLINED", "SIMULATED_INSUFFICIENT_FUNDS",
            "SIMULATED_TIMEOUT", "SIMULATED_UPI_FAILED", "SIMULATED_NETWORK_ERROR" };
        if (failed && !permittedReasons.Contains(failureReason, StringComparer.OrdinalIgnoreCase))
            failureReason = "SIMULATED_BANK_DECLINED";

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var purchase = await _context.SubscriptionPurchases
            .FromSqlInterpolated($"SELECT * FROM [SubscriptionPurchases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {dto.PurchaseId}")
            .FirstOrDefaultAsync();

        if (purchase == null || !ValidateDemoToken(purchase, dto.DemoToken))
            return Unauthorized(new { message = "Invalid or expired demo payment token." });

        if (purchase.PurchaseType != "Initial")
            return Conflict(new { message = "Public demo confirmation supports initial checkout only." });
        if (!string.Equals(purchase.PaymentMethod, method, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Payment method does not match checkout." });
        if (purchase.PaymentStatus != "Pending" && purchase.PaymentStatus != "Failed")
            return Conflict(new { message = "Purchase is already completed.", paymentStatus = purchase.PaymentStatus });

        var now = DateTime.UtcNow;
        purchase.PaymentAttemptCount++;
        purchase.LastPaymentAttemptAt = now;
        purchase.TransactionReference = $"DEMO-{now:yyyyMMddHHmmss}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
        purchase.PaymentFailureReason = success ? null : failureReason;
        purchase.PaymentStatus = success ? "Paid" : "Failed";
        // DEVELOPMENT SIMULATION ONLY: PaidAt represents simulated completion, not gateway settlement.
        purchase.PaidAt = success ? now : null;
        purchase.GatewayPaymentId = null;
        QueueSuperAdminSubscriptionNotification(
            success ? "Subscription Demo Payment Successful" : "Subscription Demo Payment Failed",
            $"Initial subscription purchase #{purchase.Id} for {purchase.Name}: " +
            $"{(success ? "simulated payment succeeded" : "simulated payment failed")}. " +
            $"Amount: ₹{purchase.Amount:F2}. Demo only; no real payment collected.");
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            purchaseId = purchase.Id,
            paymentStatus = purchase.PaymentStatus,
            paymentMethod = purchase.PaymentMethod,
            amount = purchase.Amount,
            transactionReference = purchase.TransactionReference,
            paymentFailureReason = purchase.PaymentFailureReason,
            paymentAttemptCount = purchase.PaymentAttemptCount,
            simulatedPayment = true,
            realPaymentCollected = false,
            eligibleForAdminCreation = success,
            canRetry = !success,
            message = success
                ? "Simulated payment marked Paid. SuperAdmin can now create the Admin. No real money collected."
                : "Demo payment failed. You can try again using the same purchase ID."
        });
    }

    public sealed class ConfirmDemoPaymentDto
    {
        public int PurchaseId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? SimulateResult { get; set; }
        public string? FailureReason { get; set; }
        public string? DemoToken { get; set; }
    }

    // This feature deliberately does not use DemoPayments:Enabled.
    // Development-only simulation; writes Paid/PaidAt for end-to-end testing. Never enable for real customers.
    private bool DemoPreviewEnabled() =>
        _environment.IsDevelopment() &&
        _configuration.GetValue<bool>("DemoPayments:PreviewEnabled") &&
        Encoding.UTF8.GetByteCount(_configuration["DemoPayments:SigningKey"] ?? "") >= 32;

    private string CreateDemoToken(SubscriptionPurchase purchase)
    {
        // Use stable fields. SQL Server can change DateTime tick precision
        // between checkout and the subsequent payment confirmation request.
        var payload = $"CMS-DEMO-V2:{purchase.Id}:{purchase.PaymentMethod}";
        var key = Encoding.UTF8.GetBytes(_configuration["DemoPayments:SigningKey"]!);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(signature).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private bool ValidateDemoToken(SubscriptionPurchase purchase, string suppliedToken)
    {
        // CreatedAt is stored in UTC; SQL Server typically reads it back
        // with DateTimeKind.Unspecified, so interpret it explicitly as UTC.
        var createdAtUtc = DateTime.SpecifyKind(purchase.CreatedAt, DateTimeKind.Utc);
        if (createdAtUtc.AddMinutes(30) < DateTime.UtcNow)
            return false;

        var expected = CreateDemoToken(purchase);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken.Trim());
        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    // ============================================================
    // ADMIN - RENEWAL CHECKOUT (PAYMENT NOT PROCESSED HERE)
    // POST /api/subscriptions/renewals/checkout
    // ============================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("renewals/checkout")]
    public async Task<IActionResult> RenewalCheckout([FromBody] RenewalCheckoutDto dto)
    {
        if (dto == null || dto.SubscriptionPlanId <= 0)
            return BadRequest(new { message = "Valid subscription plan is required." });

        var userId = GetCurrentUserId();
        var admin = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.HospitalId.HasValue)
            return Conflict(new { message = "Create your clinic before renewing." });

        var method = NormalizePaymentMethod(dto.PaymentMethod);
        if (method == null)
            return BadRequest(new { message = "Payment method must be UPI, Card, or NetBanking." });

        var plan = await _context.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.SubscriptionPlanId && x.IsActive);
        if (plan == null || plan.Price <= 0 || !IsValidDuration(plan.DurationMonths) || !plan.IncludesCms)
            return BadRequest(new { message = "Valid active paid plan is required." });

        // Downgrades need an explicit active-branch selection workflow before activation.
        var currentActive = await _context.ClinicSubscriptions.AsNoTracking()
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.HospitalId == admin.HospitalId && x.Status == "Active" &&
                        x.StartDate <= DateTime.UtcNow && x.EndDate > DateTime.UtcNow)
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync();
        if (currentActive != null && Tier(plan.PlanType) < Tier(currentActive.SubscriptionPlan?.PlanType))
            return Conflict(new { message = "Downgrade requires choosing the branches that remain active. This workflow is not implemented yet." });

        var existing = await _context.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.AdminUserId == admin.Id && x.HospitalId == admin.HospitalId &&
                        (x.PaymentStatus == "Pending" || x.PaymentStatus == "Failed" ||
                         (x.PaymentStatus == "Paid" && x.ActivatedAt == null)) &&
                        x.AdminCreationStatus == "Created" && x.PurchaseType == "Renewal")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        if (existing != null)
            return Conflict(new { message = "An unactivated renewal already exists. Retry its payment or activate it before creating another.", purchaseId = existing.Id, paymentStatus = existing.PaymentStatus });

        var purchase = new SubscriptionPurchase
        {
            Name = admin.Name ?? "Clinic Admin",
            Email = (admin.Email ?? string.Empty).Trim().ToLowerInvariant(),
            MobileNumber = admin.MobileNumber ?? string.Empty,
            SubscriptionPlanId = plan.Id,
            PurchaseType = "Renewal",
            PurchasedDurationMonths = plan.DurationMonths,
            PurchasedIncludesCms = true,
            PurchasedIncludesLab = plan.IncludesLab,
            PurchasedIncludesPharmacy = plan.IncludesPharmacy,
            PurchasedMaxBranches = plan.MaxBranches,
            PurchasedDailyAppointmentLimit = plan.DailyAppointmentLimit,
            Amount = plan.Price,
            GrossNewPlanAmount = plan.Price,
            FinalPayableAmount = plan.Price,
            PaymentMethod = method,
            PaymentStatus = "Pending",
            AdminCreationStatus = "Created",
            AdminUserId = admin.Id,
            HospitalId = admin.HospitalId,
            CreatedAt = DateTime.UtcNow
        };
        _context.SubscriptionPurchases.Add(purchase);
        await _context.SaveChangesAsync();
        QueueSuperAdminSubscriptionNotification(
            "Subscription Renewal Checkout Created",
            $"Renewal checkout #{purchase.Id} created for clinic #{purchase.HospitalId}, " +
            $"Admin #{admin.Id}. Plan: {plan.Name}; amount: ₹{purchase.Amount:F2}. " +
            "Payment pending; no money collected.");
        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "Renewal checkout created. No payment was collected.",
            purchaseId = purchase.Id,
            hospitalId = purchase.HospitalId,
            subscriptionPlanId = plan.Id,
            planName = plan.Name,
            planType = plan.PlanType,
            durationMonths = plan.DurationMonths,
            includesCms = true,
            includesLab = plan.IncludesLab,
            includesPharmacy = plan.IncludesPharmacy,
            maxBranches = purchase.PurchasedMaxBranches,
            dailyAppointmentLimit = purchase.PurchasedDailyAppointmentLimit,
            amount = purchase.Amount,
            paymentMethod = purchase.PaymentMethod,
            paymentStatus = purchase.PaymentStatus,
            paymentGatewayReady = false
        });
    }

    // ============================================================
    // ADMIN - UPGRADE QUOTE AND CHECKOUT
    // Only Basic -> Super/Premium and Super -> Premium.
    // No payment is collected here. Amounts are computed server-side.
    // ============================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("upgrades/checkout")]
    public async Task<IActionResult> UpgradeCheckout([FromBody] RenewalCheckoutDto dto)
    {
        if (dto == null || dto.SubscriptionPlanId <= 0)
            return BadRequest(new { message = "Valid new subscription plan is required." });

        var admin = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == GetCurrentUserId() && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.HospitalId.HasValue) return Conflict(new { message = "Clinic onboarding required." });
        var method = NormalizePaymentMethod(dto.PaymentMethod);
        if (method == null) return BadRequest(new { message = "Invalid payment method." });

        var now = DateTime.UtcNow;
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var current = await _context.ClinicSubscriptions
            .Include(x => x.SubscriptionPlan)
            .Where(x => x.HospitalId == admin.HospitalId && x.Status == "Active" &&
                        x.StartDate <= now && x.EndDate > now && x.SupersededAt == null)
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync();
        if (current == null) return Conflict(new { message = "An active subscription is required for an upgrade." });
        var plan = await _context.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == dto.SubscriptionPlanId && x.IsActive && x.IncludesCms);
        if (plan == null || plan.Price <= 0 || !IsValidDuration(plan.DurationMonths))
            return BadRequest(new { message = "Valid active new plan required." });
        var oldTier = Tier(current.SubscriptionPlan?.PlanType);
        var newTier = Tier(plan.PlanType);
        if (oldTier < 1 || newTier <= oldTier)
            return Conflict(new { message = "Upgrade requires a higher tier: Basic to Super/Premium, or Super to Premium." });
        if (await _context.SubscriptionPurchases.AnyAsync(x =>
            x.HospitalId == admin.HospitalId && x.PurchaseType == "Upgrade" &&
            x.ActivatedAt == null && (x.PaymentStatus == "Pending" || x.PaymentStatus == "Paid")))
            return Conflict(new { message = "Complete or cancel the existing upgrade checkout first." });

        // Use elapsed ticks rather than rounded whole days for the unused portion.
        // Current.Amount is the eligible paid amount stored on the active subscription.
        var totalTicks = (current.EndDate - current.StartDate).Ticks;
        var remainingTicks = Math.Max(0L, (current.EndDate - now).Ticks);
        var credit = totalTicks <= 0 ? 0m : decimal.Round(
            current.Amount * remainingTicks / totalTicks, 2, MidpointRounding.AwayFromZero);
        credit = Math.Min(Math.Max(0m, credit), plan.Price);
        var payable = plan.Price - credit;

        var purchase = new SubscriptionPurchase
        {
            Name = admin.Name ?? "Clinic Admin",
            Email = (admin.Email ?? string.Empty).Trim().ToLowerInvariant(),
            MobileNumber = admin.MobileNumber ?? string.Empty,
            SubscriptionPlanId = plan.Id,
            PurchaseType = "Upgrade",
            OriginalSubscriptionId = current.Id,
            OriginalPaidAmount = current.Amount,
            UnusedCreditAmount = credit,
            GrossNewPlanAmount = plan.Price,
            FinalPayableAmount = payable,
            Amount = payable,
            UpgradeMode = "FreshTerm",
            PurchasedDurationMonths = plan.DurationMonths,
            PurchasedIncludesCms = true,
            PurchasedIncludesLab = plan.IncludesLab,
            PurchasedIncludesPharmacy = plan.IncludesPharmacy,
            PurchasedMaxBranches = plan.MaxBranches,
            PurchasedDailyAppointmentLimit = plan.DailyAppointmentLimit,
            PaymentMethod = method,
            PaymentStatus = "Pending",
            AdminCreationStatus = "Created",
            AdminUserId = admin.Id,
            HospitalId = admin.HospitalId,
            CreatedAt = now
        };
        _context.SubscriptionPurchases.Add(purchase);
        await _context.SaveChangesAsync();
        QueueSuperAdminSubscriptionNotification("Subscription Upgrade Checkout Created",
            $"Upgrade checkout #{purchase.Id} for clinic #{purchase.HospitalId}; payable ₹{payable:F2}, credit ₹{credit:F2}. Payment pending.");
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new
        {
            purchaseId = purchase.Id,
            originalSubscriptionId = current.Id,
            newPlanId = plan.Id,
            planType = plan.PlanType,
            durationMonths = plan.DurationMonths,
            grossAmount = plan.Price,
            unusedCredit = credit,
            amount = payable,
            maxBranches = purchase.PurchasedMaxBranches,
            dailyAppointmentLimit = purchase.PurchasedDailyAppointmentLimit,
            paymentStatus = "Pending",
            paymentGatewayReady = false,
            message = payable == 0m
                ? "Zero-balance upgrade quote created. Requires trusted server-side authorization before activation."
                : "Upgrade checkout created. No payment collected."
        });
    }

    // PAYMENTS - Only independently verified Razorpay purchases can activate.
    // A simulated Paid flag cannot activate a real subscription.
    [Authorize(Roles = "Admin")]
    [HttpPost("upgrades/{purchaseId:int}/activate")]
    public async Task<IActionResult> ActivatePaidUpgrade(int purchaseId)
    {
        var admin = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == GetCurrentUserId() && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.HospitalId.HasValue) return Conflict(new { message = "Clinic onboarding required." });
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var purchase = await _context.SubscriptionPurchases
            .FromSqlInterpolated($"SELECT * FROM [SubscriptionPurchases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {purchaseId}")
            .FirstOrDefaultAsync(x => x.PurchaseType == "Upgrade" && x.AdminUserId == admin.Id && x.HospitalId == admin.HospitalId);
        if (purchase == null) return NotFound(new { message = "Upgrade purchase not found." });
        if (purchase.ActivatedAt.HasValue)
            return Conflict(new { message = "Upgrade already activated." });
        // PAYMENTS - UPGRADE ACTIVATION SECURITY
        if (!HasVerifiedCmsGatewayPayment(purchase))
            return Conflict(new
            {
                message = "Upgrade requires a verified Razorpay payment.",
                purchaseId = purchase.Id,
                paymentStatus = purchase.PaymentStatus,
                isGatewayPaymentVerified = purchase.IsGatewayPaymentVerified
            });
        if (!purchase.OriginalSubscriptionId.HasValue)
            return Conflict(new { message = "Original subscription missing." });
        var current = await _context.ClinicSubscriptions
            .FromSqlInterpolated($"SELECT * FROM [ClinicSubscriptions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {purchase.OriginalSubscriptionId.Value}")
            .FirstOrDefaultAsync();
        var now = DateTime.UtcNow;
        if (current == null || current.HospitalId != admin.HospitalId || current.Status != "Active" ||
            current.StartDate > now || current.EndDate <= now || current.SupersededAt.HasValue)
            return Conflict(new { message = "Original subscription is no longer current. Requote the upgrade." });
        // Requote when the original term has changed or a concurrent renewal exists.
        if (await _context.ClinicSubscriptions.AnyAsync(x =>
            x.HospitalId == admin.HospitalId && x.Id != current.Id && x.Status == "Active" &&
            x.StartDate <= now && x.EndDate > now && x.SupersededAt == null))
            return Conflict(new { message = "Another active subscription exists. Review subscription history." });
        var replacement = new ClinicSubscription
        {
            HospitalId = current.HospitalId,
            SubscriptionPlanId = purchase.SubscriptionPlanId,
            StartDate = now,
            EndDate = now.AddMonths(purchase.PurchasedDurationMonths),
            Amount = purchase.Amount,
            Status = "Active",
            IncludesCms = purchase.PurchasedIncludesCms,
            IncludesLab = purchase.PurchasedIncludesLab,
            IncludesPharmacy = purchase.PurchasedIncludesPharmacy,
            PurchasedMaxBranches = purchase.PurchasedMaxBranches,
            PurchasedDailyAppointmentLimit = purchase.PurchasedDailyAppointmentLimit,
            CreatedByUserId = admin.Id,
            CreatedAt = now
        };
        _context.ClinicSubscriptions.Add(replacement);
        await _context.SaveChangesAsync();
        current.Status = "Superseded";
        current.SupersededAt = now;
        current.SupersededBySubscriptionId = replacement.Id;
        purchase.ActivatedAt = now;
        QueueSuperAdminSubscriptionNotification("Subscription Upgrade Activated",
            $"Upgrade #{purchase.Id} activated for clinic #{current.HospitalId} after verified Razorpay payment.");
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new
        {
            message = "Verified paid upgrade activated successfully.",
            purchaseId = purchase.Id,
            subscriptionId = replacement.Id,
            startDate = replacement.StartDate,
            endDate = replacement.EndDate,
            maxBranches = replacement.PurchasedMaxBranches,
            dailyAppointmentLimit = replacement.PurchasedDailyAppointmentLimit
        });
    }

    private static int Tier(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        "BASIC" => 1,
        "SUPER" => 2,
        "PREMIUM" => 3,
        _ => 0
    };

    // ============================================================
    // ADMIN - RENEWAL PURCHASE HISTORY
    // GET /api/subscriptions/renewals/history
    // ============================================================
    [Authorize(Roles = "Admin")]
    [HttpGet("renewals/history")]
    public async Task<IActionResult> RenewalHistory()
    {
        var admin = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == GetCurrentUserId() && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.HospitalId.HasValue)
            return Ok(Array.Empty<object>());

        var history = await _context.SubscriptionPurchases.AsNoTracking()
            .Where(x => x.AdminUserId == admin.Id && x.HospitalId == admin.HospitalId &&
                        x.AdminCreationStatus == "Created" && x.PurchaseType == "Renewal")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                purchaseId = x.Id,
                x.SubscriptionPlanId,
                planName = x.SubscriptionPlan.Name,
                planType = x.SubscriptionPlan.PlanType,
                x.Amount,
                x.PurchasedMaxBranches,
                x.PurchasedDailyAppointmentLimit,
                x.PaymentMethod,
                x.PaymentStatus,
                x.TransactionReference,
                x.PaymentFailureReason,
                x.PaymentAttemptCount,
                x.LastPaymentAttemptAt,
                x.CreatedAt,
                x.PaidAt,
                x.ActivatedAt
            })
            .ToListAsync();
        return Ok(history);
    }

    // ============================================================
    // ADMIN - APPLY VERIFIED PAID RENEWAL
    // POST /api/subscriptions/renewals/{purchaseId}/activate
    // The payment must ALREADY be independently verified by the backend.
    // ============================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("renewals/{purchaseId:int}/activate")]
    public async Task<IActionResult> ActivatePaidRenewal(int purchaseId)
    {
        if (purchaseId <= 0) return BadRequest(new { message = "Invalid purchase ID." });
        var userId = GetCurrentUserId();

        // Serializable transaction prevents concurrent activation of the same purchase.
        await using var tx = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var admin = await _context.Users.FirstOrDefaultAsync(x =>
            x.Id == userId && x.Role == "Admin" && x.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.HospitalId.HasValue)
            return Conflict(new { message = "Clinic onboarding is required." });

        // Lock this renewal purchase for the duration of the transaction.
        // Without UPDLOCK, simultaneous activation requests can both read
        // ActivatedAt == null before either one commits.
        var purchase = await _context.SubscriptionPurchases
            .FromSqlInterpolated($"SELECT * FROM [SubscriptionPurchases] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {purchaseId}")
            .Include(x => x.SubscriptionPlan)
            .FirstOrDefaultAsync(x => x.AdminUserId == admin.Id &&
                x.HospitalId == admin.HospitalId && x.AdminCreationStatus == "Created" && x.PurchaseType == "Renewal");
        if (purchase == null) return NotFound(new { message = "Renewal purchase not found." });
        if (purchase.PurchaseType != "Renewal")
            return Conflict(new { message = "Initial subscription purchases cannot be activated as renewals." });
        if (purchase.ActivatedAt.HasValue)
            return Conflict(new { message = "Renewal already activated." });
        // PAYMENTS - RENEWAL ACTIVATION SECURITY
        if (!HasVerifiedCmsGatewayPayment(purchase))
            return Conflict(new
            {
                message = "Renewal requires a verified Razorpay payment.",
                purchaseId = purchase.Id,
                paymentStatus = purchase.PaymentStatus,
                isGatewayPaymentVerified = purchase.IsGatewayPaymentVerified
            });
        // A purchase used for initial clinic onboarding must never be reactivated as a renewal.
        // Initial purchases are created before the Admin/clinic exists; renewals
        // are created after clinic assignment. ActivatedAt also guards replay.
        var hasOriginalClinicSubscription = await _context.ClinicSubscriptions
            .AnyAsync(x => x.HospitalId == admin.HospitalId && x.CreatedByUserId == admin.Id);
        if (!hasOriginalClinicSubscription)
            return Conflict(new { message = "Initial clinic subscription must be activated through clinic onboarding." });
        if (purchase.SubscriptionPlan == null ||
            !IsValidDuration(purchase.PurchasedDurationMonths) ||
            !purchase.PurchasedIncludesCms)
            return Conflict(new { message = "Invalid purchased plan or CMS entitlement." });

        var now = DateTime.UtcNow;
        var activeSubscriptions = await _context.ClinicSubscriptions
            .Where(x => x.HospitalId == admin.HospitalId && x.Status == "Active")
            .ToListAsync();
        var lastEnd = activeSubscriptions
            .Where(x => x.EndDate > now)
            .Select(x => x.EndDate)
            .DefaultIfEmpty(now)
            .Max();

        // Future renewal starts when current paid time ends; otherwise starts now.
        var start = lastEnd > now ? lastEnd : now;
        var end = start.AddMonths(purchase.PurchasedDurationMonths);

        // Keep existing future subscriptions as historical records; they will
        // become effective when their StartDate arrives.
        var renewal = new ClinicSubscription
        {
            HospitalId = admin.HospitalId.Value,
            SubscriptionPlanId = purchase.SubscriptionPlanId,
            StartDate = start,
            EndDate = end,
            Amount = purchase.Amount,
            IncludesCms = purchase.PurchasedIncludesCms,
            IncludesLab = purchase.PurchasedIncludesLab,
            IncludesPharmacy = purchase.PurchasedIncludesPharmacy,
            PurchasedMaxBranches = purchase.PurchasedMaxBranches,
            PurchasedDailyAppointmentLimit = purchase.PurchasedDailyAppointmentLimit,
            Status = "Active",
            CreatedByUserId = admin.Id,
            CreatedAt = now
        };
        _context.ClinicSubscriptions.Add(renewal);
        purchase.ActivatedAt = now;
        QueueSuperAdminSubscriptionNotification(
            "Subscription Renewal Activated",
            $"Subscription renewal purchase #{purchase.Id} activated for clinic #{renewal.HospitalId}. " +
            $"Amount: ₹{renewal.Amount:F2}; coverage: {renewal.StartDate:yyyy-MM-dd} " +
            $"through {renewal.EndDate:yyyy-MM-dd} UTC.");
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "Paid renewal activated successfully.",
            purchaseId = purchase.Id,
            subscriptionId = renewal.Id,
            hospitalId = renewal.HospitalId,
            startDate = renewal.StartDate,
            endDate = renewal.EndDate,
            includesCms = renewal.IncludesCms,
            includesLab = renewal.IncludesLab,
            includesPharmacy = renewal.IncludesPharmacy,
            maxBranches = renewal.PurchasedMaxBranches,
            dailyAppointmentLimit = renewal.PurchasedDailyAppointmentLimit,
            status = renewal.StartDate > now ? "Scheduled" : "Active"
        });
    }

    public sealed class RenewalCheckoutDto
    {
        public int SubscriptionPlanId { get; set; }
        public string? PaymentMethod { get; set; }
    }

    private static bool IsValidDuration(int months) =>
        months == 1 || months == 3 || months == 6 || months == 12;

    private int GetCurrentUserId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static string? NormalizePaymentMethod(string? value) =>
        new[] { "UPI", "Card", "NetBanking" }.FirstOrDefault(x =>
            string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    public sealed class InitiateSubscriptionPaymentDto
    {
        public int PurchaseId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? SimulateResult { get; set; }
    }
}

















