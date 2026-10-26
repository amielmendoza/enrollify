using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Reports.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Reports.DepEd;

public record QuickCountRowDto(
    string GradeLevel, int Male, int Female, int NotSet, int Total,
    int MaleChange, int FemaleChange, int NotSetChange, int TotalChange);

public record NotSetLearnerDto(Guid EnrollmentId, Guid StudentId, string Name, string GradeLevel);

public record QuickCountDto(
    SchoolHeaderDto Header, DateOnly AsOf, IReadOnlyList<QuickCountRowDto> Rows, QuickCountRowDto Total,
    IReadOnlyList<NotSetLearnerDto> NotSetLearners);

public record GetQuickCountQuery(Guid TenantId, string? SchoolYear, DateOnly? AsOf) : IRequest<QuickCountDto>;

/// <summary>Same shape as <see cref="GetQuickCountQuery"/>; the CSV export reuses the same builder.</summary>
public record GetQuickCountExportQuery(Guid TenantId, string? SchoolYear, DateOnly? AsOf) : IRequest<QuickCountDto>;

/// <summary>
/// Shared quick-count builder so the on-screen report and the CSV export never diverge.
/// Enrollment and history reads rely on the global tenant filter; the Tenant row (not a tenant-filtered
/// entity) is loaded by id for the school header only.
/// </summary>
internal static class QuickCountReport
{
    private const string TotalLabel = "Total";

    public static async Task<QuickCountDto> BuildAsync(
        IApplicationDbContext context, Guid tenantId, string? requestedYear, DateOnly? requestedAsOf, CancellationToken ct)
    {
        var schoolYear = await EnrollmentRegisterReport.ResolveSchoolYearAsync(context, requestedYear, ct);

        var tenant = await context.Tenants.FirstAsync(t => t.Id == tenantId, ct);
        var header = new SchoolHeaderDto(
            tenant.Name, tenant.DepEdSchoolId, tenant.DepEdRegionName, tenant.DepEdDivision, tenant.DepEdDistrict, schoolYear);

        var asOf = requestedAsOf ?? DateOnly.FromDateTime(DepEdRules.ManilaDate(DateTime.UtcNow));
        var day = asOf.ToDateTime(TimeOnly.MinValue);
        var previousDay = day.AddDays(-1);

        var enrollments = await context.Enrollments
            .Include(e => e.Student)
            .Where(e => e.SchoolYear == schoolYear)
            .ToListAsync(ct);

        // CountedOn expects only that enrollment's own history rows, so group once by EnrollmentId.
        var historyByEnrollment = (await context.EnrollmentStatusHistories
                .Where(h => h.Enrollment.SchoolYear == schoolYear)
                .ToListAsync(ct))
            .ToLookup(h => h.EnrollmentId);

        // Per grade: [on asOf, on the day before] x [Male, Female, NotSet].
        var grid = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var notSet = new List<(string Grade, NotSetLearnerDto Learner)>();

        foreach (var e in enrollments)
        {
            var history = historyByEnrollment[e.Id].ToList();
            var countedToday = DepEdRules.CountedOn(e, history, day);
            var countedBefore = DepEdRules.CountedOn(e, history, previousDay);
            if (!countedToday && !countedBefore)
                continue;

            var sex = DepEdRules.Sex(e.Student.Gender);
            var col = sex switch { "Male" => 0, "Female" => 1, _ => 2 };

            if (!grid.TryGetValue(e.GradeLevel, out var cells))
                grid[e.GradeLevel] = cells = new int[6];

            if (countedToday)
            {
                cells[col]++;
                if (col == 2)
                    notSet.Add((e.GradeLevel, new NotSetLearnerDto(
                        e.Id, e.StudentId,
                        DepEdRules.LearnerName(e.Student.LastName, e.Student.FirstName, e.Student.ExtensionName, e.Student.MiddleName),
                        e.GradeLevel)));
            }
            if (countedBefore)
                cells[3 + col]++;
        }

        var canonical = GradeLevels.All.ToList();
        var rows = grid
            .OrderBy(kv => canonical.IndexOf(kv.Key) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => ToRow(kv.Key, kv.Value))
            .ToList();

        var totals = new int[6];
        foreach (var cells in grid.Values)
            for (var i = 0; i < 6; i++)
                totals[i] += cells[i];

        var learners = notSet
            .OrderBy(n => canonical.IndexOf(n.Grade) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(n => n.Grade, StringComparer.Ordinal)
            .ThenBy(n => n.Learner.Name, StringComparer.OrdinalIgnoreCase)
            .Select(n => n.Learner)
            .ToList();

        return new QuickCountDto(header, asOf, rows, ToRow(TotalLabel, totals), learners);
    }

    private static QuickCountRowDto ToRow(string grade, int[] c)
    {
        var total = c[0] + c[1] + c[2];
        var totalBefore = c[3] + c[4] + c[5];
        return new QuickCountRowDto(
            grade, c[0], c[1], c[2], total,
            c[0] - c[3], c[1] - c[4], c[2] - c[5], total - totalBefore);
    }
}

public class GetQuickCountQueryHandler : IRequestHandler<GetQuickCountQuery, QuickCountDto>
{
    private readonly IApplicationDbContext _context;

    public GetQuickCountQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<QuickCountDto> Handle(GetQuickCountQuery request, CancellationToken cancellationToken) =>
        QuickCountReport.BuildAsync(_context, request.TenantId, request.SchoolYear, request.AsOf, cancellationToken);
}

public class GetQuickCountExportQueryHandler : IRequestHandler<GetQuickCountExportQuery, QuickCountDto>
{
    private readonly IApplicationDbContext _context;

    public GetQuickCountExportQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<QuickCountDto> Handle(GetQuickCountExportQuery request, CancellationToken cancellationToken) =>
        QuickCountReport.BuildAsync(_context, request.TenantId, request.SchoolYear, request.AsOf, cancellationToken);
}
