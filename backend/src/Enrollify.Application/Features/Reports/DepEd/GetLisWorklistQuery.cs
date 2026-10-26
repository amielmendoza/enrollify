using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Reports.Queries;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Reports.DepEd;

/// <summary>
/// School block printed at the top of every DepEd report (LIS worklist, quick count, SF1).
/// Fields are null where the school has not filled in its DepEd profile yet.
/// </summary>
public record SchoolHeaderDto(
    string? SchoolName, string? SchoolId, string? RegionName, string? Division, string? District, string SchoolYear);

public record WorklistSectionDto(
    Guid? SectionId, string GradeLevel, string SectionName, string? Adviser, int Listed, int Encoded, int Pending);

public record WorklistLearnerDto(
    Guid EnrollmentId, Guid StudentId, string? Lrn, bool LrnNeedsReview, string Name, string Sex,
    DateTime? BirthDate, string? LearnerType, DateTime FirstAttendance, IReadOnlyList<string> Tags,
    string? WithdrawalType, DateTime? WithdrawalDate, DateTime? LisEncodedAt, string? LisEncodedBy,
    IReadOnlyList<string> FixReasons);

public record WorklistSectionDetailDto(
    Guid? SectionId, string GradeLevel, string SectionName, string? Adviser,
    IReadOnlyList<WorklistLearnerDto> Continuing, IReadOnlyList<WorklistLearnerDto> SearchOrRegister);

public record LisWorklistDto(
    SchoolHeaderDto Header, IReadOnlyList<WorklistSectionDto> Sections, WorklistSectionDetailDto? Selected);

public record GetLisWorklistQuery(Guid TenantId, string? SchoolYear, Guid? SectionId, bool NoSection)
    : IRequest<LisWorklistDto>;

/// <summary>
/// The LIS worklist: what the registrar still has to encode in the DepEd LIS, by section. Listed
/// (current status Approved/Paid/Enrolled, withdrawn learners included) learners are split into
/// "Continuing" (find them in the LIS) and "Search or register" (new, transferee, returning or
/// unspecified), each carrying the fixes needed before they can be encoded.
/// The query takes <see cref="GetLisWorklistQuery.TenantId"/> only to load the school header:
/// <c>Tenant</c> is not a tenant-filtered entity. Enrollments and sections rely on the global filter.
/// </summary>
public class GetLisWorklistQueryHandler : IRequestHandler<GetLisWorklistQuery, LisWorklistDto>
{
    private const string Continuing = "Continuing";
    private const string New = "New";
    private const string NoSectionName = "No section";

    private readonly IApplicationDbContext _context;

    public GetLisWorklistQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LisWorklistDto> Handle(GetLisWorklistQuery request, CancellationToken cancellationToken)
    {
        var schoolYear = await EnrollmentRegisterReport.ResolveSchoolYearAsync(_context, request.SchoolYear, cancellationToken);

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == request.TenantId, cancellationToken);
        var header = new SchoolHeaderDto(
            tenant.Name, tenant.DepEdSchoolId, tenant.DepEdRegionName, tenant.DepEdDivision, tenant.DepEdDistrict, schoolYear);

        var yearRow = await _context.SchoolYears.FirstOrDefaultAsync(sy => sy.Name == schoolYear, cancellationToken);

        var canonical = GradeLevels.All.ToList();
        var sections = (await _context.Sections.Where(s => s.SchoolYear == schoolYear).ToListAsync(cancellationToken))
            .OrderBy(s => canonical.IndexOf(s.GradeLevel) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(s => s.GradeLevel, StringComparer.Ordinal)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var listed = (await _context.Enrollments
                .Include(e => e.Student)
                .Where(e => e.SchoolYear == schoolYear)
                .ToListAsync(cancellationToken))
            .Where(DepEdRules.Listed)
            .ToList();

        // A listed enrollment whose SectionId is not one of this year's sections (e.g. the section was
        // moved to another year after assignment) folds into the no-section group, so nobody vanishes.
        var sectionIds = sections.Select(s => s.Id).ToHashSet();
        var bySection = listed.ToLookup(e => e.SectionId is { } id && sectionIds.Contains(id) ? id : (Guid?)null);

        var rows = sections
            .Select(s => Count(s.Id, s.GradeLevel, s.Name, s.Adviser, bySection[s.Id]))
            .ToList();
        rows.Add(Count(null, string.Empty, NoSectionName, null, bySection[null]));

        WorklistSectionDetailDto? selected = null;
        if (request.SectionId is { } sectionId)
        {
            var section = sections.FirstOrDefault(s => s.Id == sectionId);
            if (section != null)
                selected = Detail(section.Id, section.GradeLevel, section.Name, section.Adviser, bySection[sectionId], yearRow, schoolYear);
        }
        else if (request.NoSection)
        {
            selected = Detail(null, string.Empty, NoSectionName, null, bySection[null], yearRow, schoolYear);
        }

        return new LisWorklistDto(header, rows, selected);
    }

    private static WorklistSectionDto Count(
        Guid? sectionId, string grade, string name, string? adviser, IEnumerable<Enrollment> learners)
    {
        var all = learners.ToList();
        var encoded = all.Count(e => e.LisEncodedAt != null);
        return new WorklistSectionDto(sectionId, grade, name, adviser, all.Count, encoded, all.Count - encoded);
    }

    private static WorklistSectionDetailDto Detail(
        Guid? sectionId, string grade, string name, string? adviser,
        IEnumerable<Enrollment> learners, SchoolYear? yearRow, string schoolYear)
    {
        var ordered = learners
            .OrderBy(e => e.Student.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Student.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Student.MiddleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var continuing = ordered.Where(IsContinuing).Select(e => Project(e, yearRow, schoolYear)).ToList();
        var searchOrRegister = ordered.Where(e => !IsContinuing(e)).Select(e => Project(e, yearRow, schoolYear)).ToList();

        return new WorklistSectionDetailDto(sectionId, grade, name, adviser, continuing, searchOrRegister);
    }

    private static bool IsLearnerType(Enrollment e, string type) =>
        string.Equals(e.LearnerType?.Trim(), type, StringComparison.OrdinalIgnoreCase);

    private static bool IsContinuing(Enrollment e) => IsLearnerType(e, Continuing);

    private static WorklistLearnerDto Project(Enrollment e, SchoolYear? yearRow, string schoolYear)
    {
        var s = e.Student;
        var sex = DepEdRules.Sex(s.Gender);
        var hasBirthDate = s.BirthDate != default;
        var lrn = string.IsNullOrWhiteSpace(s.LRN) ? null : s.LRN.Trim();
        var typeSet = !string.IsNullOrWhiteSpace(e.LearnerType);

        var fixes = new List<string>();
        if (sex == "NotSet") fixes.Add("Sex not set");
        if (!hasBirthDate) fixes.Add("No birth date");
        if (s.LrnNeedsReview) fixes.Add("LRN needs checking");
        // A New learner has no LRN yet by definition — LIS assigns it on registration.
        if (lrn == null && !IsLearnerType(e, New)) fixes.Add("No LRN");
        if (!typeSet) fixes.Add("Learner type not set");

        var tags = new List<string>();
        if (s.Is4PsBeneficiary) tags.Add("4Ps");
        if (IsLearnerType(e, "Returning")) tags.Add("Balik-aral");
        if (e.IsRepeater) tags.Add("Repeater");

        return new WorklistLearnerDto(
            e.Id, e.StudentId, lrn, s.LrnNeedsReview,
            DepEdRules.LearnerName(s.LastName, s.FirstName, s.ExtensionName, s.MiddleName),
            sex, hasBirthDate ? s.BirthDate : null, e.LearnerType,
            DepEdRules.EffectiveFirstAttendance(e, yearRow, schoolYear),
            tags, e.WithdrawalType, e.WithdrawalDate, e.LisEncodedAt, e.LisEncodedBy, fixes);
    }
}
