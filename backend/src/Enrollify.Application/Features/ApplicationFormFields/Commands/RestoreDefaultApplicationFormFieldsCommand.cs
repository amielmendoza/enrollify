using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Interfaces;
using MediatR;

namespace Enrollify.Application.Features.ApplicationFormFields.Commands;

/// <summary>
/// Idempotently re-seeds the default application form fields for the current admin's tenant.
/// Safe to call any time — fields whose key already exists are skipped, custom fields are untouched.
/// Returns the number of newly inserted defaults.
/// </summary>
public record RestoreDefaultApplicationFormFieldsCommand : IRequest<int>;

public class RestoreDefaultApplicationFormFieldsCommandHandler : IRequestHandler<RestoreDefaultApplicationFormFieldsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public RestoreDefaultApplicationFormFieldsCommandHandler(IApplicationDbContext context, ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public async Task<int> Handle(RestoreDefaultApplicationFormFieldsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant context — cannot restore defaults.");

        var added = await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(_context, tenantId, cancellationToken);
        if (added > 0) await _context.SaveChangesAsync(cancellationToken);
        return added;
    }
}
