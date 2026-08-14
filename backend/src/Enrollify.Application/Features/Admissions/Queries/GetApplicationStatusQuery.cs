using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Queries;

/// <summary>
/// Anonymous status lookup by application number for the public /apply flow.
/// TenantId comes from the school slug in the URL (not the tenant provider), so the
/// query bypasses the global filter and scopes explicitly, like the other slug-based handlers.
/// </summary>
public record GetApplicationStatusQuery(Guid TenantId, string ApplicationNumber) : IRequest<ApplicationStatusDto>;

public class GetApplicationStatusQueryHandler : IRequestHandler<GetApplicationStatusQuery, ApplicationStatusDto>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationStatusQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ApplicationStatusDto> Handle(GetApplicationStatusQuery request, CancellationToken cancellationToken)
    {
        var number = request.ApplicationNumber.Trim();

        var app = await _context.AdmissionApplications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.TenantId == request.TenantId && a.ApplicationNumber == number, cancellationToken)
            ?? throw new KeyNotFoundException("No application found with that application number.");

        var lastInitial = string.IsNullOrEmpty(app.LastName) ? "" : $" {app.LastName[0]}.";

        return new ApplicationStatusDto(
            app.ApplicationNumber,
            $"{app.FirstName}{lastInitial}",
            app.GradeLevel,
            app.SchoolYear,
            app.Status,
            app.CreatedAt,
            app.ReviewedAt,
            app.ReviewNotes);
    }
}
