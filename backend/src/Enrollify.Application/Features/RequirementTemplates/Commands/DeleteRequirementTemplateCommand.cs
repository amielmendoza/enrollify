using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.RequirementTemplates.Commands;

public record DeleteRequirementTemplateCommand(Guid Id) : IRequest<bool>;

public class DeleteRequirementTemplateCommandHandler : IRequestHandler<DeleteRequirementTemplateCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteRequirementTemplateCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteRequirementTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.RequirementTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Template not found.");

        _context.RequirementTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
