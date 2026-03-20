using Enrollify.Domain.Interfaces;

namespace Enrollify.Infrastructure.Services;

public class TenantProvider : ITenantProvider
{
    private Guid _tenantId;

    public Guid GetTenantId() => _tenantId;

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
