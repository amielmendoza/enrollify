using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ApplicationFormFields.Commands;

public record DeleteApplicationFormFieldCommand(Guid Id) : IRequest<bool>;

public class DeleteApplicationFormFieldCommandHandler : IRequestHandler<DeleteApplicationFormFieldCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteApplicationFormFieldCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteApplicationFormFieldCommand request, CancellationToken cancellationToken)
    {
        var field = await _context.ApplicationFormFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Field not found.");

        // Built-ins are part of the application schema and cannot be deleted; admins should hide them instead.
        if (field.IsBuiltIn)
            throw new InvalidOperationException("Built-in fields cannot be deleted. Hide them instead by toggling visibility off.");

        _context.ApplicationFormFields.Remove(field);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
