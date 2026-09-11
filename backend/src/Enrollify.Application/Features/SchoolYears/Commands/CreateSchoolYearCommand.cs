using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolYears;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Commands;

/// <summary>
/// Creates a school year and provisions it: the standard payment-term triple is ALWAYS
/// ensured (copied from CopyFromSchoolYear when given, else from the tenant's most recent
/// year with terms, else the standard defaults), and when CopyFromSchoolYear is given the
/// source year's active fees and sections can be cloned into the new year.
/// </summary>
public record CreateSchoolYearCommand(
    string Name, DateTime StartDate, DateTime EndDate,
    string? CopyFromSchoolYear = null, bool IncludeFees = true, bool IncludeSections = true
) : IRequest<SchoolYearDto>;

public class CreateSchoolYearCommandValidator : AbstractValidator<CreateSchoolYearCommand>
{
    public CreateSchoolYearCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20)
            .Matches(@"^\d{4}-\d{4}$").WithMessage("Name must be in format YYYY-YYYY");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate);
        RuleFor(x => x.CopyFromSchoolYear)
            .Must((cmd, copyFrom) => string.IsNullOrEmpty(copyFrom) || copyFrom != cmd.Name)
            .WithMessage("Cannot copy a school year from itself.");
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

        await EnsurePaymentTermsAsync(request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.CopyFromSchoolYear))
        {
            if (request.IncludeFees)
                await CopyFeesAsync(request.CopyFromSchoolYear, request.Name, cancellationToken);
            if (request.IncludeSections)
                await CopySectionsAsync(request.CopyFromSchoolYear, request.Name, cancellationToken);
        }

        // One save → one transaction: the year and everything provisioned with it land together.
        await _context.SaveChangesAsync(cancellationToken);

        return new SchoolYearDto(schoolYear.Id, schoolYear.Name, schoolYear.StartDate, schoolYear.EndDate, schoolYear.IsActive, schoolYear.CreatedAt);
    }

    /// <summary>
    /// The new year always ends up with the Full/Monthly/Quarterly triple. Term values come
    /// from CopyFromSchoolYear when it has terms, else the tenant's most recent year that
    /// has any, else the standard defaults — per plan type.
    /// </summary>
    private async Task EnsurePaymentTermsAsync(CreateSchoolYearCommand request, CancellationToken ct)
    {
        var sourceTerms = new List<PaymentTerm>();

        if (!string.IsNullOrWhiteSpace(request.CopyFromSchoolYear))
        {
            sourceTerms = await _context.PaymentTerms
                .Where(t => t.SchoolYear == request.CopyFromSchoolYear)
                .ToListAsync(ct);
        }

        if (sourceTerms.Count == 0)
        {
            var latestYearWithTerms = await _context.PaymentTerms
                .Where(t => t.SchoolYear != request.Name)
                .Select(t => t.SchoolYear)
                .Distinct()
                .OrderByDescending(name => name)
                .FirstOrDefaultAsync(ct);

            if (latestYearWithTerms != null)
                sourceTerms = await _context.PaymentTerms
                    .Where(t => t.SchoolYear == latestYearWithTerms)
                    .ToListAsync(ct);
        }

        var existingPlanTypes = await _context.PaymentTerms
            .Where(t => t.SchoolYear == request.Name)
            .Select(t => t.PlanType)
            .ToListAsync(ct);

        foreach (var standard in DefaultPaymentTerms.Standard)
        {
            if (existingPlanTypes.Contains(standard.PlanType)) continue;

            var source = sourceTerms.FirstOrDefault(t => t.PlanType == standard.PlanType);
            _context.PaymentTerms.Add(new PaymentTerm
            {
                SchoolYear = request.Name,
                PlanType = standard.PlanType,
                DownPaymentPercent = source?.DownPaymentPercent ?? standard.DownPaymentPercent,
                InterestRatePercent = source?.InterestRatePercent ?? standard.InterestRatePercent,
                DiscountPercent = source?.DiscountPercent ?? standard.DiscountPercent,
                InstallmentCount = source?.InstallmentCount ?? standard.InstallmentCount,
                IsActive = true
            });
        }
    }

    private async Task CopyFeesAsync(string fromYear, string toYear, CancellationToken ct)
    {
        var sourceFees = await _context.Fees
            .Where(f => f.SchoolYear == fromYear && f.IsActive)
            .ToListAsync(ct);

        var taken = (await _context.Fees
                .Where(f => f.SchoolYear == toYear)
                .Select(f => new { f.Name, f.GradeLevel })
                .ToListAsync(ct))
            .Select(x => (x.Name, x.GradeLevel))
            .ToHashSet();

        foreach (var fee in sourceFees)
        {
            if (!taken.Add((fee.Name, fee.GradeLevel))) continue; // skip (name, grade) duplicates

            _context.Fees.Add(new Fee
            {
                Name = fee.Name,
                Description = fee.Description,
                Amount = fee.Amount,
                SchoolYear = toYear,
                GradeLevel = fee.GradeLevel,
                IsActive = true
            });
        }
    }

    private async Task CopySectionsAsync(string fromYear, string toYear, CancellationToken ct)
    {
        var sourceSections = await _context.Sections
            .Where(s => s.SchoolYear == fromYear && s.IsActive)
            .ToListAsync(ct);

        var taken = (await _context.Sections
                .Where(s => s.SchoolYear == toYear)
                .Select(s => new { s.Name, s.GradeLevel })
                .ToListAsync(ct))
            .Select(x => (x.Name, x.GradeLevel))
            .ToHashSet();

        foreach (var section in sourceSections)
        {
            if (!taken.Add((section.Name, section.GradeLevel))) continue; // skip (name, grade) duplicates

            // Fresh rows: new Ids, no enrollments carried over.
            _context.Sections.Add(new Section
            {
                Name = section.Name,
                GradeLevel = section.GradeLevel,
                SchoolYear = toYear,
                Capacity = section.Capacity,
                Adviser = section.Adviser,
                IsActive = true
            });
        }
    }
}
