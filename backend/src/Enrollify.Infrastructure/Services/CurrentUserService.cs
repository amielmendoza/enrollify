using System.Security.Claims;
using Enrollify.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Enrollify.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return id != null ? Guid.Parse(id) : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue("TenantId");
            return id != null ? Guid.Parse(id) : null;
        }
    }

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
}
