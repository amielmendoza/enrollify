using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Subdomain { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
