using System.Linq.Expressions;
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Reports.Queries;

public record EnrollmentRegisterRowDto(
    Guid EnrollmentId,
    Guid StudentId,
    string? Lrn,
    string StudentName,
    string GradeLevel,
    string? SectionName,        // null when unassigned
    string Status,              // EnrollmentStatus name (self-describing in JSON and CSV)
    string? PaymentPlan,
    decimal? AssessedTotal,     // null pre-assessment
    decimal TotalPaid,
    decimal? Balance,           // null pre-assessment — never a faked zero
    DateTime CreatedAt,
    string StudentNumber = "");

public record RegisterStatusCountDto(string Status, int Count);

public record RegisterGradeCountDto(string GradeLevel, int Count);

public record EnrollmentRegisterSummaryDto(
    int TotalCount,
    List<RegisterStatusCountDto> ByStatus,
    List<RegisterGradeCountDto> ByGrade);

public record EnrollmentRegisterDto(
    List<EnrollmentRegisterRowDto> Items,
    int TotalCount,
    EnrollmentRegisterSummaryDto Summary);

/// <summary>
/// Shared filter for the enrollment register, reused by the paged query and the CSV export
/// so the two can never disagree. SchoolYear may be null in the incoming request — it is
/// resolved to the tenant's active year server-side (the register is a per-year document).
/// Cancelled enrollments are EXCLUDED by default: the register is an official document of
/// who is enrolling/enrolled; pass IncludeCancelled to audit cancellations (an explicit
/// Status == Cancelled filter still needs IncludeCancelled, or it matches nothing).
/// </summary>
public record EnrollmentRegisterFilter(
    string? SchoolYear,
    string? GradeLevel,
    EnrollmentStatus? Status,
    Guid? SectionId,
    bool IncludeCancelled = false);

/// <summary>
/// Shared filter/ordering/projection for the enrollment register (paged query + CSV export).
/// Money columns are batched — one grouped query for approved-payment sums, one for
/// non-voided adjustment nets, one fetch of the year's active PaymentTerms — never per-row.
/// Balance composition mirrors GetBalanceQuery/LedgerCalculator exactly:
/// EffectiveTotal(plan, assessedTotal, term) + adjustment net − approved payments, with the same
/// live-catalog fallback for enrollments assessed before fee snapshots existed.
/// </summary>
internal static class EnrollmentRegisterReport
{
    /// <summary>
    /// e => GradeLevel == "Kindergarten" ? 0 : GradeLevel == "Grade 1" ? 1 : … : int.MaxValue,
    /// built from GradeLevels.All so it translates to a SQL CASE (and stays in sync with the list).
    /// </summary>
    private static readonly Expression<Func<Enrollment, int>> GradeRank = BuildGradeRank();

    private static Expression<Func<Enrollment, int>> BuildGradeRank()
    {
        var e = Expression.Parameter(typeof(Enrollment), "e");
        var grade = Expression.Property(e, nameof(Enrollment.GradeLevel));
        Expression body = Expression.Constant(int.MaxValue);
        for (var i = GradeLevels.All.Count - 1; i >= 0; i--)
        {
            body = Expression.Condition(
                Expression.Equal(grade, Expression.Constant(GradeLevels.All[i])),
                Expression.Constant(i),
                body);
        }
        return Expression.Lambda<Func<Enrollment, int>>(body, e);
    }

    public static async Task<string> ResolveSchoolYearAsync(IApplicationDbContext context, string? schoolYear, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(schoolYear))
            return schoolYear;

        var active = await context.SchoolYears
            .Where(sy => sy.IsActive)
            .Select(sy => sy.Name)
            .FirstOrDefaultAsync(ct);

        return active ?? throw new InvalidOperationException(
            "No schoolYear was given and no active school year is configured. Pass schoolYear explicitly or activate one in Settings.");
    }

    public static IQueryable<Enrollment> Filter(IApplicationDbContext context, EnrollmentRegisterFilter filter, string schoolYear)
    {
        var query = context.Enrollments.Where(e => e.SchoolYear == schoolYear);

        if (!filter.IncludeCancelled)
            query = query.Where(e => e.Status != EnrollmentStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(filter.GradeLevel))
            query = query.Where(e => e.GradeLevel == filter.GradeLevel);

        if (filter.Status != null)
            query = query.Where(e => e.Status == filter.Status);

        if (filter.SectionId != null)
            query = query.Where(e => e.SectionId == filter.SectionId);

        return query;
    }

    /// <summary>
    /// Deterministic register order: canonical grade (Kindergarten, Grade 1 … Grade 12 — not
    /// text order, which puts Grade 10 before Grade 2), unrecognized grades last by name, then
    /// student name, then Id tie-break so page boundaries can't duplicate/skip rows and the
    /// CSV order matches the pages.
    /// </summary>
    public static IOrderedQueryable<Enrollment> Order(IQueryable<Enrollment> query) =>
        query.OrderBy(GradeRank)
            .ThenBy(e => e.GradeLevel)
            .ThenBy(e => e.Student.LastName)
            .ThenBy(e => e.Student.FirstName)
            .ThenBy(e => e.Student.MiddleName)
            .ThenBy(e => e.Id);

    /// <summary>Projects enrollments (already filtered/ordered/paged by the caller) into register rows.</summary>
    public static async Task<List<EnrollmentRegisterRowDto>> ProjectAsync(
        IApplicationDbContext context, IQueryable<Enrollment> enrollments, string schoolYear, CancellationToken ct)
    {
        var raw = await enrollments
            .Select(e => new
            {
                e.Id,
                e.StudentId,
                e.Student.LRN,
                e.Student.StudentNumber,
                e.Student.LastName,
                e.Student.FirstName,
                e.Student.MiddleName,
                e.GradeLevel,
                SectionName = e.Section != null ? e.Section.Name : null,
                e.Status,
                e.PaymentPlan,
                e.AssessedTotal,
                e.CreatedAt
            })
            .ToListAsync(ct);

        var ids = raw.Select(r => r.Id).ToList();

        // Batched money loads — grouped sums, never one query per row.
        var paidById = await context.Payments
            .Where(p => ids.Contains(p.EnrollmentId) && p.Status == "Approved")
            .GroupBy(p => p.EnrollmentId)
            .Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Sum, ct);

        var adjNetById = await context.LedgerAdjustments
            .Where(a => ids.Contains(a.EnrollmentId) && !a.IsVoided)
            .GroupBy(a => a.EnrollmentId)
            .Select(g => new { g.Key, Net = g.Sum(a => a.Type == "Debit" ? a.Amount : -a.Amount) })
            .ToDictionaryAsync(g => g.Key, g => g.Net, ct);

        var termByPlan = (await context.PaymentTerms
                .Where(t => t.SchoolYear == schoolYear && t.IsActive)
                .ToListAsync(ct))
            .GroupBy(t => t.PlanType)
            .ToDictionary(g => g.Key, g => g.First());

        // Enrollments assessed before fee snapshots existed (AssessedTotal null at Assessed or
        // later) fall back to the year's live fee catalog — exactly like GetBalanceQuery — so the
        // register and the enrollment's own balance never disagree. One grouped query.
        var legacyGrades = raw
            .Where(r => r.AssessedTotal == null && IsAssessedOrLater(r.Status))
            .Select(r => r.GradeLevel).Distinct().ToList();
        var catalogByGrade = legacyGrades.Count == 0
            ? new Dictionary<string, decimal>()
            : await context.Fees
                .Where(f => f.SchoolYear == schoolYear && f.IsActive && legacyGrades.Contains(f.GradeLevel))
                .GroupBy(f => f.GradeLevel)
                .Select(g => new { g.Key, Total = g.Sum(f => f.Amount) })
                .ToDictionaryAsync(g => g.Key, g => g.Total, ct);

        return raw.Select(r =>
        {
            var paid = paidById.GetValueOrDefault(r.Id);
            var adjNet = adjNetById.GetValueOrDefault(r.Id);
            var assessed = r.AssessedTotal
                ?? (IsAssessedOrLater(r.Status) ? catalogByGrade.GetValueOrDefault(r.GradeLevel) : (decimal?)null);

            // Balance only exists once charges do: pre-assessment rows report null, not 0.
            decimal? balance = null;
            if (assessed != null)
            {
                var term = r.PaymentPlan != null ? termByPlan.GetValueOrDefault(r.PaymentPlan) : null;
                balance = PaymentsCalculator.EffectiveTotal(r.PaymentPlan, assessed.Value, term) + adjNet - paid;
            }

            // Same composition as Student.FullName (computed property, not translatable in SQL).
            return new EnrollmentRegisterRowDto(
                r.Id, r.StudentId, r.LRN,
                $"{r.LastName}, {r.FirstName} {r.MiddleName}".TrimEnd(),
                r.GradeLevel, r.SectionName, r.Status.ToString(), r.PaymentPlan,
                assessed, paid, balance, r.CreatedAt, StudentNumber: r.StudentNumber);
        }).ToList();
    }

    // Assessed, Approved, Paid or Enrolled — charges exist. (Cancelled is excluded: whether a
    // legacy cancelled enrollment was ever assessed can't be known.)
    private static bool IsAssessedOrLater(EnrollmentStatus status) =>
        status is EnrollmentStatus.Assessed or EnrollmentStatus.Approved or EnrollmentStatus.Paid or EnrollmentStatus.Enrolled;
}

public record GetEnrollmentRegisterQuery(
    EnrollmentRegisterFilter Filter, int Page = 1, int PageSize = 50
) : IRequest<EnrollmentRegisterDto>;

public class GetEnrollmentRegisterQueryHandler : IRequestHandler<GetEnrollmentRegisterQuery, EnrollmentRegisterDto>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentRegisterQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentRegisterDto> Handle(GetEnrollmentRegisterQuery request, CancellationToken cancellationToken)
    {
        var schoolYear = await EnrollmentRegisterReport.ResolveSchoolYearAsync(_context, request.Filter.SchoolYear, cancellationToken);
        var filtered = EnrollmentRegisterReport.Filter(_context, request.Filter, schoolYear);

        // Summary always covers the FULL filtered set, independent of paging.
        var totalCount = await filtered.CountAsync(cancellationToken);

        var byStatusRaw = await filtered
            .GroupBy(e => e.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byStatus = byStatusRaw
            .OrderBy(s => (int)s.Key)
            .Select(s => new RegisterStatusCountDto(s.Key.ToString(), s.Count))
            .ToList();

        var byGradeRaw = await filtered
            .GroupBy(e => e.GradeLevel)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var canonical = GradeLevels.All.ToList();
        var byGrade = byGradeRaw
            .OrderBy(g => canonical.IndexOf(g.Key) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(g => g.Key)
            .Select(g => new RegisterGradeCountDto(g.Key, g.Count))
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var items = await EnrollmentRegisterReport.ProjectAsync(
            _context,
            EnrollmentRegisterReport.Order(filtered).Skip((page - 1) * pageSize).Take(pageSize),
            schoolYear,
            cancellationToken);

        return new EnrollmentRegisterDto(items, totalCount,
            new EnrollmentRegisterSummaryDto(totalCount, byStatus, byGrade));
    }
}
