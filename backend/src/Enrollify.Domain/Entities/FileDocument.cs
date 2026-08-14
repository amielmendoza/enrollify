using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class FileDocument : TenantEntity
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSize { get; set; }
    public byte[] Data { get; set; } = default!;
}
