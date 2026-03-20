using Enrollify.Domain.Interfaces;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, ApplicationDbContext dbContext)
    {
        // Skip tenant resolution for auth endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth"))
        {
            // Try to resolve tenant from header even for auth, but don't fail
            if (TryResolveTenantId(context, out var authTenantId))
            {
                tenantProvider.SetTenantId(authTenantId);
            }

            await _next(context);
            return;
        }

        if (!TryResolveTenantId(context, out var tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant could not be resolved. Provide X-Tenant-Id header." });
            return;
        }

        // Verify tenant exists
        var tenantExists = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && t.IsActive);

        if (!tenantExists)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant not found or inactive." });
            return;
        }

        tenantProvider.SetTenantId(tenantId);
        await _next(context);
    }

    private static bool TryResolveTenantId(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        // 1. Try from JWT claim
        var tenantClaim = context.User?.FindFirst("TenantId")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out tenantId))
            return true;

        // 2. Try from header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
            && Guid.TryParse(headerValue, out tenantId))
            return true;

        return false;
    }
}
