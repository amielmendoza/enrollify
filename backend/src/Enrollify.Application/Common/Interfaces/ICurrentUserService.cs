namespace Enrollify.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Role { get; }
}
