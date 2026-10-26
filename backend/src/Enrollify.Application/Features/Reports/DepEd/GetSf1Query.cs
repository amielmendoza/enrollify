using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Reports.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Reports.DepEd;

/// <param name="Sex">"M", "F", or "" when not set.</param>
public record Sf1RowDto(
    string? Lrn, string Name, string Sex, DateTime? BirthDate, int? Age, string? MotherTongue, string? IpGroup,
    string? Religion, string? HouseStreet, string? Barangay, string? CityMunicipality, string? Province,
    string? FatherName, string? MotherMaidenName, string? GuardianName, string? GuardianRelationship,
    string? Contact, string? Remarks);

public record Sf1HeaderDto(
    SchoolHeaderDto School, string GradeLevel, string SectionName, string? Adviser, string? SchoolHeadName);

public record Sf1Dto(
    Sf1HeaderDto Header, IReadOnlyList<Sf1RowDto> Learners, int Male, int Female, int NotSet, int Total);

public record GetSf1Query(Guid TenantId, string? SchoolYear, Guid SectionId) : IRequest<Sf1Dto>;

/// <summary>Same shape as <see cref="GetSf1Query"/>; the CSV export reuses the same builder.</summary>
public record GetSf1ExportQuery(Guid TenantId, string? SchoolYear, Guid SectionId) : IRequest<Sf1Dto>;

/// <summary>
/// Shared SF1 builder so the on-screen working copy and the CSV export never diverge. Uses current
/// status (<see cref="DepEdRules.Listed"/>), so no status history is read. Section and enrollment reads
/// rely on the global tenant filter; the Tenant row (not a tenant-filtered entity) is loaded by id for the
/// school header and school head only.
/// </summary>
internal static class Sf1Report
{
    public static async Task<Sf1Dto> BuildAsync(
        IApplicationDbContext context, Guid tenantId, string? requestedYear, Guid sectionId, CancellationToken ct)
    {
        var schoolYear = await EnrollmentRegisterReport.ResolveSchoolYearAsync(context, requestedYear, ct);

        var tenant = await context.Tenants.FirstAsync(t => t.Id == tenantId, ct);
        var school = new SchoolHeaderDto(
            tenant.Name, tenant.DepEdSchoolId, tenant.DepEdRegionName, tenant.DepEdDivision, tenant.DepEdDistrict, schoolYear);

        var section = await context.Sections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new KeyNotFoundException("Section not found.");

        var yearRow = await context.SchoolYears.FirstOrDefaultAsync(sy => sy.Name == schoolYear, ct);
        var startYear = DepEdRules.StartYear(schoolYear, yearRow);

        var listed = (await context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.SchoolYear == schoolYear && e.SectionId == sectionId)
                .ToListAsync(ct))
            .Where(DepEdRules.Listed)
            .ToList();

        var learners = listed
            .Select(e => (Sex: DepEdRules.Sex(e.Student.Gender), Enrollment: e))
            .OrderBy(x => x.Sex switch { "Male" => 0, "Female" => 1, _ => 2 })
            .ThenBy(x => x.Enrollment.Student.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Enrollment.Student.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Enrollment.Student.MiddleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = learners.Select(x => Project(x.Enrollment, x.Sex, yearRow, schoolYear, startYear)).ToList();

        var male = learners.Count(x => x.Sex == "Male");
        var female = learners.Count(x => x.Sex == "Female");
        var notSet = learners.Count - male - female;

        var header = new Sf1HeaderDto(school, section.GradeLevel, section.Name, section.Adviser, tenant.SchoolHeadName);
        return new Sf1Dto(header, rows, male, female, notSet, rows.Count);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Sf1RowDto Project(
        Domain.Entities.Enrollment e, string sex, Domain.Entities.SchoolYear? yearRow, string schoolYear, int startYear)
    {
        var s = e.Student;
        var hasBirthDate = s.BirthDate != default;

        return new Sf1RowDto(
            Blank(s.LRN),
            DepEdRules.LearnerName(s.LastName, s.FirstName, s.ExtensionName, s.MiddleName),
            sex switch { "Male" => "M", "Female" => "F", _ => string.Empty },
            hasBirthDate ? s.BirthDate : null,
            DepEdRules.AgeOn(s.BirthDate, startYear),
            Blank(s.MotherTongue),
            s.IsIndigenousPeople ? Blank(s.IpCommunity) : null,
            Blank(s.Religion),
            Blank(s.Address),
            Blank(s.HomeAddress.BarangayName),
            Blank(s.HomeAddress.CityMunicipalityName),
            Blank(s.HomeAddress.ProvinceName),
            Blank(DepEdRules.FatherName(s)),
            Blank(DepEdRules.MotherMaidenName(s)),
            Blank(s.GuardianName),
            Blank(s.GuardianRelationship),
            DepEdRules.Contact(s.GuardianContact, s.MotherContactNumber, s.FatherContactNumber),
            Blank(DepEdRules.Remarks(e, s, yearRow, schoolYear)));
    }
}

public class GetSf1QueryHandler : IRequestHandler<GetSf1Query, Sf1Dto>
{
    private readonly IApplicationDbContext _context;

    public GetSf1QueryHandler(IApplicationDbContext context) => _context = context;

    public Task<Sf1Dto> Handle(GetSf1Query request, CancellationToken cancellationToken) =>
        Sf1Report.BuildAsync(_context, request.TenantId, request.SchoolYear, request.SectionId, cancellationToken);
}

public class GetSf1ExportQueryHandler : IRequestHandler<GetSf1ExportQuery, Sf1Dto>
{
    private readonly IApplicationDbContext _context;

    public GetSf1ExportQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<Sf1Dto> Handle(GetSf1ExportQuery request, CancellationToken cancellationToken) =>
        Sf1Report.BuildAsync(_context, request.TenantId, request.SchoolYear, request.SectionId, cancellationToken);
}
