using Microsoft.EntityFrameworkCore;
using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using System.Text;


var builder = WebApplication.CreateBuilder(args);


// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration
            .GetConnectionString("DefaultConnection")
    )
);



// =====================================================
// SERVICES
// =====================================================

builder.Services.AddScoped<EmailHelper>();

builder.Services.AddScoped<JwtHelper>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IStaffService, StaffService>();


builder.Services.AddScoped<IAppointmentService, AppointmentService>();


// PaymentService depends on AppointmentService

builder.Services.AddScoped<IPaymentService, PaymentService>();


builder.Services.AddScoped<NotificationService>();



// =====================================================
// CONTROLLERS
// =====================================================

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });



builder.Services.AddEndpointsApiExplorer();



// =====================================================
// CORS
// =====================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowLocalhost",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});



// =====================================================
// SWAGGER + JWT
// =====================================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new()
        {
            Title = "Clinic API",
            Version = "v1"
        });


    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",

            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT",

            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header,

            Description =
                "Enter JWT Token"
        });



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
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,

                            Id = "Bearer"
                        }
                },

                Array.Empty<string>()
            }
        });
});



// =====================================================
// JWT AUTH
// =====================================================

var jwtKey =
    builder.Configuration["Jwt:Key"];


var key =
    Encoding.UTF8.GetBytes(jwtKey);



builder.Services

    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>
    {

        options.TokenValidationParameters =
            new TokenValidationParameters
            {

                ValidateIssuer = false,

                ValidateAudience = false,


                ValidateIssuerSigningKey = true,


                IssuerSigningKey =
                    new SymmetricSecurityKey(key),


                ValidateLifetime = true,


                ClockSkew =
                    TimeSpan.Zero
            };

    });



Console.WriteLine(
    builder.Configuration
        .GetConnectionString("DefaultConnection")
);



// =====================================================
// BUILD APP
// =====================================================

var app =
    builder.Build();




// =====================================================
// DATABASE MIGRATION + SUPER ADMIN SEED
// =====================================================

using (var scope = app.Services.CreateScope())
{

    var context =
        scope.ServiceProvider
        .GetRequiredService<AppDbContext>();


    context.Database.Migrate();



   




    var superAdmin =
        await context.Users
            .FirstOrDefaultAsync(x =>
                x.Email == "superadmin@gmail.com");



    if (superAdmin == null)
    {

        context.Users.Add(
            new User
            {

                Name =
                    "Super Admin",


                MobileNumber =
                    "0000000000",


                Email =
                    "superadmin@gmail.com",


                PasswordHash =
                    BCrypt.Net.BCrypt
                    .HashPassword("Super@123"),


                Role =
                    "SuperAdmin",

                HospitalId = null,


                IsActive =
                    true,


                MustChangePassword =
                    false

            });


        await context.SaveChangesAsync();

    }

}



// =====================================================
// MIDDLEWARE
// =====================================================


app.UseSwagger();

app.UseSwaggerUI();



if (!string.Equals(
        builder.Configuration["ASPNETCORE_DISABLE_HTTPS_REDIRECTION"],
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}


app.UseStaticFiles();



app.UseCors("AllowLocalhost");




app.UseAuthentication();


app.UseAuthorization();



app.MapControllers();



app.Run();
