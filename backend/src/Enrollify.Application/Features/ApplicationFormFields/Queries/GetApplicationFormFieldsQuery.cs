using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ApplicationFormFields;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ApplicationFormFields.Queries;

/// <summary>
/// Returns all configured fields for the current tenant, ordered by Section then DisplayOrder.
/// Used both by the admin Settings page (sees everything) and the public /apply form
/// (filters to IsVisible on the client).
/// </summary>
public record GetApplicationFormFieldsQuery(Guid TenantId) : IRequest<List<ApplicationFormFieldDto>>;

public class GetApplicationFormFieldsQueryHandler : IRequestHandler<GetApplicationFormFieldsQuery, List<ApplicationFormFieldDto>>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationFormFieldsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ApplicationFormFieldDto>> Handle(GetApplicationFormFieldsQuery request, CancellationToken cancellationToken)
    {
        // Use IgnoreQueryFilters so the public /apply endpoint (no tenant context) can fetch
        // configuration scoped via the explicit TenantId parameter the controller passes in.
        var fields = await _context.ApplicationFormFields.IgnoreQueryFilters()
            .Where(f => f.TenantId == request.TenantId)
            .OrderBy(f => f.Section).ThenBy(f => f.DisplayOrder).ThenBy(f => f.Label)
            .ToListAsync(cancellationToken);

        return fields.Select(f => new ApplicationFormFieldDto(
            f.Id, f.FieldKey, f.Label, f.FieldType, f.Section, f.AppliesTo,
            f.IsRequired, f.IsVisible, f.IsBuiltIn, f.IsCore, f.DisplayOrder, f.Options, f.HelpText))
            .ToList();
    }
}
