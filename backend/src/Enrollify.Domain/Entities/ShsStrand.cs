using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// One Senior High School track/strand a school offers (e.g. Academic – STEM). Deactivated,
/// never deleted, so an empty catalog always means "defaults never seeded". Enrollments store
/// the Track/Strand strings, so renaming or deactivating a row never rewrites history.
/// </summary>
public class ShsStrand : TenantEntity
{
    public string Track { get; set; } = default!;
    public string Strand { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
