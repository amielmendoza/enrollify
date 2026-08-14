using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Sections;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Sections.Commands;

public record UpdateSectionCommand(
    Guid Id, string Name, string GradeLevel, string SchoolYear, int Capacity, string? Adviser, bool IsActive
) : IRequest<SectionDto>;

public class UpdateSectionCommandValidator : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GradeLevel).NotEmpty();
        RuleFor(x => x.SchoolYear).NotEmpty();
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class UpdateSectionCommandHandler : IRequestHandler<UpdateSectionCommand, SectionDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateSectionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SectionDto> Handle(UpdateSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await _context.Sections
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        if (request.Capacity < section.CurrentCount)
            throw new InvalidOperationException(
                $"Cannot set capacity to {request.Capacity}: the section currently has {section.CurrentCount} enrolled students.");

        section.Name = request.Name;
        section.GradeLevel = request.GradeLevel;
        section.SchoolYear = request.SchoolYear;
        section.Capacity = request.Capacity;
        section.Adviser = request.Adviser;
        section.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new SectionDto(section.Id, section.Name, section.GradeLevel, section.SchoolYear,
            section.Capacity, section.CurrentCount, section.Adviser, section.IsActive);
    }
}
