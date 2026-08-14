using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public FilesController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "application/pdf" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Only JPG, PNG, GIF, and PDF files are allowed" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        var doc = new FileDocument
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Data = ms.ToArray()
        };

        _context.FileDocuments.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);

        var fileUrl = $"/api/files/{doc.Id}";
        return Ok(new { fileName = file.FileName, fileUrl, fileSize = file.Length, contentType = file.ContentType });
    }

    // Tenant-scoped via the global query filter (tenant resolved from the JWT claim), so a
    // document GUID from one school is unreachable from another. Clients must fetch with the
    // Authorization header (blob download) — plain <a href> links won't authenticate.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _context.FileDocuments
            .Where(f => f.Id == id)
            .Select(f => new { f.Data, f.ContentType, f.FileName })
            .FirstOrDefaultAsync(cancellationToken);

        if (doc == null)
            return NotFound();

        return File(doc.Data, doc.ContentType, doc.FileName);
    }
}
