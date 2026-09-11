using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Sections.Commands;

public record DeleteSectionCommand(Guid Id) : IRequest;

public class DeleteSectionCommandHandler : IRequestHandler<DeleteSectionCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteSectionCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteSectionCommand request, CancellationToken cancellationToken)
    {
        // FirstOrDefaultAsync (not FindAsync) so the tenant query filter applies.
        var section = await _context.Sections
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        // Refuse while students still occupy seats — a clear 400 instead of an FK 500.
        // Cancelled enrollments don't hold seats (their SectionId is also cleared on cancel).
        var enrolledCount = section.Enrollments.Count(e => e.Status != EnrollmentStatus.Cancelled);
        if (enrolledCount > 0)
            throw new InvalidOperationException($"Section has {enrolledCount} enrolled students — reassign them first.");

        _context.Sections.Remove(section);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
