using Enrollify.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Commands;

public record ResetTenantUserPasswordCommand(Guid TenantId, Guid UserId, string NewPassword) : IRequest<bool>;

public class ResetTenantUserPasswordCommandValidator : AbstractValidator<ResetTenantUserPasswordCommand>
{
    public ResetTenantUserPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ResetTenantUserPasswordCommandHandler : IRequestHandler<ResetTenantUserPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public ResetTenantUserPasswordCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(ResetTenantUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found in this school.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
