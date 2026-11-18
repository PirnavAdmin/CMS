using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

using Microsoft.EntityFrameworkCore;
using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services;
using AuthDemo.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// RAZORPAY - PERSISTENT LOCAL WINDOWS DATA PROTECTION
// ======================================================
// Keep this key ring secure and backed up. Windows uses DPAPI; Linux
// deployments persist the key ring in a private Docker volume.
var dataProtectionFolder = OperatingSystem.IsWindows()
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Adminclinic", "DataProtectionKeys")
    : Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH") ?? "/var/lib/adminclinic/dataprotection";

Directory.CreateDirectory(dataProtectionFolder);

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("Adminclinic-Razorpay")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionFolder));

if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}

builder.Services.AddScoped<RazorpayCredentialStore>();

// ======================================================
// 1. DATABASE CONFIGURATION
// ======================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ======================================================
// 2. REGISTER SERVICES
// ======================================================

builder.Services.AddScoped<EmailHelper>();
builder.Services.AddScoped<JwtHelper>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<PharmacyOtcSalesService>();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<
    AuthDemo.Services.CmsWebhookRetryBackgroundService>();

// PAYMENTS - Shared HTTP client factory for location lookups and Razorpay.
// Register once; all services use the same factory.
builder.Services.AddHttpClient();
// ======================================================
// 3. CONTROLLERS
// ======================================================

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });

builder.Services.AddEndpointsApiExplorer();

//======================================================
//PAYMENTMERCHANT
//======================================================
builder.Services.AddScoped<
    AuthDemo.Services.PaymentMerchantRouter>();
builder.Services.AddScoped<AuthDemo.Services.HospitalRazorpayGatewayService>();
// Shared hospital Razorpay order creation service.
builder.Services.AddScoped<HospitalPaymentOrderService>();
// Read-only reconciliation of uncertain hospital Razorpay refunds.
builder.Services.AddScoped<HospitalRefundReconciliationService>();
// Change #19: Hospital settlement reconciliation (manual invocation only).
// Do not enable automatic matching until gateway evidence is verified.
// Shared SQL Server lock for manual and background settlement reconciliation.
builder.Services.AddScoped<HospitalSettlementReconciliationLockService>();
builder.Services.AddScoped<HospitalSettlementReconciliationService>();
// Change #19: opt-in background reconciliation; disabled unless explicitly configured.
builder.Services.AddHostedService<HospitalSettlementReconciliationBackgroundService>();
builder.Services.AddHostedService<AuthDemo.Services.RazorpaySettlementSyncBackgroundService>();
builder.Services.AddScoped<HospitalPaymentRecoveryService>();
builder.Services.AddScoped<HospitalPaymentRecoveryLockService>();
builder.Services.AddHostedService<HospitalReconciliationDiscoveryWorker>();
builder.Services.AddScoped<HospitalPaymentAllocationService>();
builder.Services.AddScoped<HospitalPaymentRecoveryExecutor>();




// =====================================================
// PAYMENTS - CMS OWNER RAZORPAY SERVICES
// =====================================================

builder.Services.AddScoped<
    AuthDemo.Services.CmsSubscriptionRazorpayService>();

// =====================================================
// PAYMENTS - CMS SUBSCRIPTION WEBHOOK VERIFICATION
// =====================================================

builder.Services.AddScoped<
    AuthDemo.Services.CmsRazorpayWebhookVerifier>();

// =====================================================
// PAYMENTS - CMS RAZORPAY PAYMENT RECONCILIATION
// =====================================================

builder.Services.AddScoped<
    CmsRazorpayPaymentReconciliationService>();

// =====================================================
// PAYMENTS - CMS WEBHOOK RECONCILIATION SERVICE
// =====================================================

builder.Services.AddScoped<
    CmsRazorpayReconciliationService>();

// ======================================================
// 4. CORS CONFIGURATION
// ======================================================

// For public ngrok testing, configure the exact frontend
// origins in appsettings.Development.json.
// Do not allow every origin in a publicly exposed API.

var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        // No browser cross-origin requests are allowed until
        // explicit origins have been configured.
    });
});

// ======================================================
// 5. SWAGGER CONFIGURATION
// ======================================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Clinic API",
        Version = "v1"
    });

    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter JWT Token"
        }
    );

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType
                                    .SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        }
    );
});

// ======================================================
// 6. JWT AUTHENTICATION
// ======================================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must contain at least 32 UTF-8 bytes."
    );
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,

                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

// ======================================================
// 7. RATE LIMITING FOR DEMO PAYMENT
// ======================================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("admin-registration", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddFixedWindowLimiter(
        "demo-payment",
        limiter =>
        {
            limiter.PermitLimit = 10;

            limiter.Window =
                TimeSpan.FromMinutes(1);

            limiter.QueueLimit = 0;

            limiter.AutoReplenishment = true;
        }
    );
});

// ======================================================
// 8. VALIDATE DEMO PAYMENT CONFIGURATION
// ======================================================

var demoPaymentsEnabled =
    builder.Configuration.GetValue<bool>(
        "DemoPayments:Enabled"
    );

if (demoPaymentsEnabled)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Demo payments can only be enabled in Development."
        );
    }

    var connectionString =
        builder.Configuration.GetConnectionString(
            "DefaultConnection"
        );

    var sqlConnectionBuilder =
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            connectionString
        );

    var databaseName =
        sqlConnectionBuilder.InitialCatalog;

    if (string.IsNullOrWhiteSpace(databaseName) ||
        !databaseName.StartsWith(
            "CMS_DEMO_",
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Demo payments require a separate SQL Server " +
            "database whose name starts with CMS_DEMO_."
        );
    }

    var demoSigningKey =
        builder.Configuration["DemoPayments:SigningKey"];

    if (string.IsNullOrWhiteSpace(demoSigningKey) ||
        Encoding.UTF8.GetByteCount(demoSigningKey) < 32)
    {
        throw new InvalidOperationException(
            "DemoPayments:SigningKey must contain " +
            "at least 32 UTF-8 bytes."
        );
    }
}

// ======================================================
// 8A. VALIDATE PREVIEW-ONLY DEMO PAYMENTS
// ======================================================
// PreviewEnabled enables public frontend simulation in the existing CMSDB.
// In Development only, success writes Paid and failure writes Failed.
// THIS DOES NOT COLLECT OR VERIFY REAL MONEY. Disable before production.
// No manual SuperAdmin payment approval is required.
var demoPreviewEnabled = builder.Configuration.GetValue<bool>(
    "DemoPayments:PreviewEnabled");

if (demoPreviewEnabled)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "DemoPayments:PreviewEnabled is allowed only in Development.");
    }

    if (demoPaymentsEnabled)
    {
        throw new InvalidOperationException(
            "Enable either DemoPayments:PreviewEnabled or DemoPayments:Enabled, not both.");
    }

    var previewSigningKey = builder.Configuration["DemoPayments:SigningKey"];
    if (string.IsNullOrWhiteSpace(previewSigningKey) ||
        Encoding.UTF8.GetByteCount(previewSigningKey) < 32)
    {
        throw new InvalidOperationException(
            "DemoPayments:SigningKey must contain at least 32 UTF-8 bytes when preview mode is enabled.");
    }
}

// ======================================================
// 9. BUILD APPLICATION
// ======================================================

var app = builder.Build();

// ======================================================
// 10. DATABASE + SUPER ADMIN SEED
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Apply migrations separately after reviewing
    // the existing database.
    //
    // await context.Database.MigrateAsync();

    var superAdmin = await context.Users
        .FirstOrDefaultAsync(
            x => x.Role == "SuperAdmin"
        );

    // Development only. Configure BootstrapSuperAdmin:Email and
    // BootstrapSuperAdmin:Password using User Secrets or environment variables.
    // Existing SuperAdmin credentials are never overwritten.

    var bootstrapEmail = builder.Configuration["BootstrapSuperAdmin:Email"];
    var bootstrapPassword = builder.Configuration["BootstrapSuperAdmin:Password"];

    if (superAdmin == null &&
        app.Environment.IsDevelopment() &&
        !string.IsNullOrWhiteSpace(bootstrapEmail) &&
        !string.IsNullOrWhiteSpace(bootstrapPassword))
    {
        context.Users.Add(new User
        {
            Name = "Super Admin",

            MobileNumber = "0000000000",

            Email = bootstrapEmail.Trim(),

            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(bootstrapPassword),

            Role = "SuperAdmin",

            HospitalId = null,

            IsActive = true,

            // Fixed SuperAdmin credentials are not subject to first-login rotation.
            MustChangePassword = false
        });

        await context.SaveChangesAsync();
    }
}

// ======================================================
// 11. SWAGGER MIDDLEWARE
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

// ======================================================
// 12. COMMON MIDDLEWARE
// ======================================================

// TLS is terminated by the public reverse proxy in Docker deployments.
if (!string.Equals(Environment.GetEnvironmentVariable("BEHIND_PROXY"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowLocalhost");

app.UseRateLimiter();

app.UseAuthentication();

// ======================================================
// 13. FIRST-LOGIN PASSWORD CHANGE ENFORCEMENT
// ======================================================
// Check the database on each authenticated API request, rather than
// trusting a potentially stale MustChangePassword JWT claim.
// This runs before subscription enforcement and authorization.
app.Use(async (httpContext, next) =>
{
    var path = httpContext.Request.Path;

    if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        httpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null ||
        httpContext.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    // Password changes and logout must remain possible while restricted.
    // Login, OTP and reset endpoints are already marked [AllowAnonymous].
    var isPasswordChange =
        path.Equals("/api/Auth/change-password", StringComparison.OrdinalIgnoreCase) &&
        HttpMethods.IsPost(httpContext.Request.Method);
    var isLogout =
        path.Equals("/api/Auth/logout", StringComparison.OrdinalIgnoreCase) &&
        HttpMethods.IsPost(httpContext.Request.Method);

    if (!int.TryParse(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var currentUserId))
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await httpContext.Response.WriteAsJsonAsync(new { message = "Invalid user identity." });
        return;
    }

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var account = await db.Users.AsNoTracking()
        .Where(x => x.Id == currentUserId)
        .Select(x => new { x.IsActive, x.MustChangePassword })
        .FirstOrDefaultAsync();

    if (account == null || !account.IsActive)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            message = "User account is inactive or unavailable."
        });
        return;
    }

    // SuperAdmin retains the configured fixed password; other users must
    // complete their first-login password change before accessing APIs.
    if (account.MustChangePassword &&
        !httpContext.User.IsInRole("SuperAdmin") &&
        !isPasswordChange &&
        !isLogout)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            message = "You must change your temporary password before continuing.",
            mustChangePassword = true,
            forcePasswordChange = true
        });
        return;
    }

    await next();
});

// ======================================================
// 13. SUBSCRIPTION ENFORCEMENT MIDDLEWARE
// ======================================================

// CMS is mandatory.
// Lab and Pharmacy are optional based on purchased plan.
//
// Public subscription checkout is allowed.
// SuperAdmin can manage purchases and Admin accounts.
// Admin can create clinic before subscription activation.

app.Use(async (httpContext, next) =>
{
    var path = httpContext.Request.Path;

    if (!path.StartsWithSegments(
        "/api",
        StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var endpoint = httpContext.GetEndpoint();

    // Public endpoints are allowed without login.

    if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>()
        != null)
    {
        await next();
        return;
    }

    // These controllers handle their own authorization.

    if (path.StartsWithSegments(
            "/api/Auth",
            StringComparison.OrdinalIgnoreCase) ||

        path.StartsWithSegments(
            "/api/subscriptions",
            StringComparison.OrdinalIgnoreCase) ||

        path.StartsWithSegments(
            "/api/clinic-subscriptions",
            StringComparison.OrdinalIgnoreCase) ||

        path.StartsWithSegments(
            "/api/superadmin",
            StringComparison.OrdinalIgnoreCase) ||

        path.StartsWithSegments(
            "/api/cms/subscription-payments",
            StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Allow authentication middleware/authorization
    // to handle unauthenticated requests.

    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    // SuperAdmin bypasses clinic subscription checks.

    if (httpContext.User.IsInRole("SuperAdmin"))
    {
        await next();
        return;
    }

    // ==================================================
    // 14. VALIDATE USER ID
    // ==================================================

    if (!int.TryParse(
        httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier
        ),
        out var userId))
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message = "Invalid user identity."
        });

        return;
    }

    var db = httpContext.RequestServices
        .GetRequiredService<AppDbContext>();

    var user = await db.Users
        .AsNoTracking()
        .Where(x => x.Id == userId)
        .Select(x => new
        {
            x.Id,
            x.Role,
            x.IsActive,
            x.HospitalId,
            x.MustChangePassword
        })
        .FirstOrDefaultAsync();

    if (user == null || !user.IsActive)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "User account is inactive or unavailable."
        });

        return;
    }

    // ==================================================
    // 15. ALLOW ADMIN CLINIC ONBOARDING
    // ==================================================

    if (HttpMethods.IsPost(httpContext.Request.Method) &&
        (
            path.Equals(
                "/api/clinics",
                StringComparison.OrdinalIgnoreCase) ||

            path.Equals(
                "/api/admin/onboarding/clinic",
                StringComparison.OrdinalIgnoreCase)
        ) &&
        user.Role == "Admin" &&
        !user.HospitalId.HasValue &&
        !user.MustChangePassword)
    {
        await next();
        return;
    }

    // Allow location lookups while an Admin is completing first-clinic setup.
    // Keep this narrowly scoped: GET only, Admin only, no clinic yet,
    // and only after mandatory password setup is complete.
    // The Location controller's own authorization still applies.
    if (HttpMethods.IsGet(httpContext.Request.Method) &&
        path.StartsWithSegments("/api/Location", StringComparison.OrdinalIgnoreCase) &&
        user.Role == "Admin" &&
        !user.HospitalId.HasValue &&
        !user.MustChangePassword)
    {
        await next();
        return;
    }

    // ==================================================
    // 16. CHECK CLINIC ASSIGNMENT
    // ==================================================

    if (!user.HospitalId.HasValue)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "Clinic onboarding is required before " +
                "accessing this module.",

            onboardingRequired =
                user.Role == "Admin"
        });

        return;
    }

    // ==================================================
    // 17. VALIDATE JWT CLINIC CLAIM
    // ==================================================

    if (!int.TryParse(
        httpContext.User.FindFirstValue("HospitalId"),
        out var tokenHospitalId) ||

        tokenHospitalId != user.HospitalId.Value)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "Clinic assignment has changed. " +
                "Refresh your login token."
        });

        return;
    }

    // ==================================================
    // 18. CHECK ACTIVE SUBSCRIPTION
    // ==================================================

    var now = DateTime.UtcNow;

    var subscription = await db.ClinicSubscriptions
        .AsNoTracking()
        .Where(x =>
            x.HospitalId == user.HospitalId.Value &&

            x.Status == "Active" &&

            x.StartDate <= now &&

            x.EndDate > now
        )
        .OrderByDescending(x => x.StartDate)
        .ThenByDescending(x => x.Id)
        .Select(x => new
        {
            x.Id,
            x.IncludesCms,
            x.IncludesLab,
            x.IncludesPharmacy
        })
        .FirstOrDefaultAsync();

    if (subscription == null)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "An active clinic subscription is required.",

            subscriptionRequired = true
        });

        return;
    }

    // ==================================================
    // 19. CMS ACCESS
    // ==================================================

    if (!subscription.IncludesCms)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "CMS access is not enabled for " +
                "this clinic subscription."
        });

        return;
    }

    // ==================================================
    // 20. LAB ACCESS
    // ==================================================

    if (path.StartsWithSegments(
            "/api/Lab",
            StringComparison.OrdinalIgnoreCase) &&

        !subscription.IncludesLab)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "Lab module is not included in " +
                "this clinic subscription."
        });

        return;
    }

    // ==================================================
    // 21. PHARMACY ACCESS
    // ==================================================

    if (path.StartsWithSegments(
            "/api/Pharmacy",
            StringComparison.OrdinalIgnoreCase) &&

        !subscription.IncludesPharmacy)
    {
        httpContext.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        await httpContext.Response.WriteAsJsonAsync(new
        {
            message =
                "Pharmacy module is not included in " +
                "this clinic subscription."
        });

        return;
    }

    await next();
});

// ======================================================
// 22. AUTHORIZATION
// ======================================================

app.UseAuthorization();

// ======================================================
// PRE-CLINIC ADMIN ONBOARDING RESTRICTION
// Admins without HospitalId may only register/login, browse plans,
// create/verify Initial subscription payments, refresh account status,
// and create their first clinic. All other operational APIs are blocked.
// ======================================================
app.Use(async (httpContext, next) =>
{
    var user = httpContext.User;
    if (user.Identity?.IsAuthenticated != true ||
        !string.Equals(user.FindFirstValue(ClaimTypes.Role), "Admin", StringComparison.OrdinalIgnoreCase))
    { await next(); return; }

    var hospitalClaim = user.FindFirst("HospitalId")?.Value;
    if (!string.IsNullOrWhiteSpace(hospitalClaim)) { await next(); return; }

    var path = httpContext.Request.Path.Value ?? string.Empty;
    var method = httpContext.Request.Method;
    var allowed =
        path.StartsWith("/api/auth/admin/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase) ||
        (path.Equals("/api/subscriptions/public/plans", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(method)) ||
        (path.Equals("/api/subscriptions/payment-methods", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(method)) ||
        (path.Equals("/api/subscriptions/checkout", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method)) ||
        (path.StartsWith("/api/cms/subscription-payments/create-order/", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method)) ||
        (path.Equals("/api/cms/subscription-payments/verify", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method)) ||
        (path.StartsWith("/api/cms/subscription-payments/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/status", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(method)) ||
        (path.Equals("/api/clinics", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(method));

    if (!allowed)
    {
        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(new { message = "Complete email verification, subscription payment and clinic onboarding before accessing operational APIs.", onboardingRequired = true });
        return;
    }
    await next();
});

// ======================================================
// 23. MAP CONTROLLERS
// ======================================================

app.MapControllers();

if (string.Equals(Environment.GetEnvironmentVariable("RUN_DB_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ======================================================
// 24. RUN APPLICATION
// ======================================================

app.Run();

