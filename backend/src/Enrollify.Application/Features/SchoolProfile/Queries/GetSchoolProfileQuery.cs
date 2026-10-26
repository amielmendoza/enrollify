using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolProfile;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolProfile.Queries;

/// <summary>
/// Reads the current school's DepEd profile. Tenant is not a TenantEntity, so the global
/// query filter does not scope it: the caller passes the current tenant id (from the JWT)
/// and the handler selects that tenant's own row by primary key.
/// </summary>
public record GetSchoolProfileQuery(Guid TenantId) : IRequest<SchoolProfileDto>;

public class GetSchoolProfileQueryHandler : IRequestHandler<GetSchoolProfileQuery, SchoolProfileDto>
{
    private readonly IApplicationDbContext _context;

    public GetSchoolProfileQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<SchoolProfileDto> Handle(GetSchoolProfileQuery request, CancellationToken cancellationToken)
    {
        var t = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("School not found.");

        return new SchoolProfileDto(t.Name, t.DepEdSchoolId, t.DepEdRegionCode, t.DepEdRegionName,
            t.DepEdDivision, t.DepEdDistrict, t.SchoolHeadName);
    }
}
