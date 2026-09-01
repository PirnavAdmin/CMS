using System.Security.Claims;
using AuthDemo.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string module)
        : base(typeof(RequirePermissionFilter))
    {
        Arguments = new object[] { module };
    }
}

public sealed class RequirePermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _module;
    private readonly AppDbContext _context;

    public RequirePermissionFilter(string module, AppDbContext context)
    {
        _module = module;
        _context = context;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
            return;

        var role = principal.FindFirstValue(ClaimTypes.Role)
                   ?? principal.FindFirstValue("role");

        // Admin and SuperAdmin manage the system and are not restricted by
        // Doctor/Receptionist user permissions.
        if (role is not ("Doctor" or "Receptionist"))
            return;

        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid user token" });
            return;
        }

        var permission = await _context.UserPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Module == _module);

        // Temporary default policy: if Admin has not saved a permission row yet,
        // Doctor/Receptionist receives full access to that module.
        // If a row exists, its saved true/false values are enforced.
        var allowed = permission == null ||
            (context.HttpContext.Request.Method.ToUpperInvariant() switch
            {
                "GET" or "HEAD" => permission.CanView,
                "POST" => permission.CanCreate,
                "PUT" or "PATCH" => permission.CanEdit,
                "DELETE" => permission.CanDelete,
                _ => false
            });

        if (!allowed)
        {
            context.Result = new ObjectResult(new
            {
                message = $"You do not have permission to perform this action in {_module}"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
