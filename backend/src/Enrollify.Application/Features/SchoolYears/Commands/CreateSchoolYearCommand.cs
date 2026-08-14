using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolYears;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Commands;

public record CreateSchoolYearCommand(string Name, DateTime StartDate, DateTime EndDate) : IRequest<SchoolYearDto>;

public class CreateSchoolYearCommandValidator : AbstractValidator<CreateSchoolYearCommand>
{
    public CreateSchoolYearCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20)
            .Matches(@"^\d{4}-\d{4}$").WithMessage("Name must be in format YYYY-YYYY");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate);
    }
}

public class CreateSchoolYearCommandHandler : IRequestHandler<CreateSchoolYearCommand, SchoolYearDto>
{
    private readonly IApplicationDbContext _context;

    public CreateSchoolYearCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SchoolYearDto> Handle(CreateSchoolYearCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.SchoolYears.AnyAsync(sy => sy.Name == request.Name, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"School year '{request.Name}' already exists.");

        var schoolYear = new SchoolYear
        {
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false
        };

        _context.SchoolYears.Add(schoolYear);
        await _context.SaveChangesAsync(cancellationToken);

        return new SchoolYearDto(schoolYear.Id, schoolYear.Name, schoolYear.StartDate, schoolYear.EndDate, schoolYear.IsActive, schoolYear.CreatedAt);
    }
}
