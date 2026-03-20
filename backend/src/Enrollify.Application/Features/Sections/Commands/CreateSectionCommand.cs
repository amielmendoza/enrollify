using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Sections;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Enrollify.Application.Features.Sections.Commands;

public record CreateSectionCommand(
    string Name, string GradeLevel, string SchoolYear, int Capacity, string? Adviser
) : IRequest<SectionDto>;

public class CreateSectionCommandValidator : AbstractValidator<CreateSectionCommand>
{
    public CreateSectionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GradeLevel).NotEmpty();
        RuleFor(x => x.SchoolYear).NotEmpty();
        RuleFor(x => x.Capacity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class CreateSectionCommandHandler : IRequestHandler<CreateSectionCommand, SectionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateSectionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SectionDto> Handle(CreateSectionCommand request, CancellationToken cancellationToken)
    {
        var section = new Section
        {
            Name = request.Name,
            GradeLevel = request.GradeLevel,
            SchoolYear = request.SchoolYear,
            Capacity = request.Capacity,
            Adviser = request.Adviser
        };

        _context.Sections.Add(section);
        await _context.SaveChangesAsync(cancellationToken);

        return new SectionDto(section.Id, section.Name, section.GradeLevel, section.SchoolYear,
            section.Capacity, 0, section.Adviser, section.IsActive);
    }
}
