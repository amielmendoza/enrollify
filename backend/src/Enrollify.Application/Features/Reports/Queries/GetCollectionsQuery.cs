using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Reports.Queries;

public record CollectionRowDto(
    Guid PaymentId,
    DateTime PaymentDate,
    string StudentName,
    string GradeLevel,
    string SchoolYear,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? ReceivedBy,
    Guid EnrollmentId);

public record CollectionMethodSummaryDto(string Method, decimal Amount, int Count);

public record CollectionDaySummaryDto(DateTime Date, decimal Amount, int Count);

public record CollectionsSummaryDto(
    decimal TotalAmount,
    int TotalCount,
    List<CollectionMethodSummaryDto> ByMethod,
    List<CollectionDaySummaryDto> ByDay);

public record CollectionsDto(
    List<CollectionRowDto> Rows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    CollectionsSummaryDto Summary);

/// <summary>
/// Shared filter/projection for the collections journal, reused by the paged query and
/// the CSV export so the two can never disagree.
/// Date basis is PaymentDate — the date the money was received (the cashier's collection
/// date). ReviewedAt is merely when a registrar verified the payment, so it is NOT used
/// for journal placement.
/// </summary>
internal static class CollectionsReport
{
    public static IQueryable<Payment> Filter(IApplicationDbContext context, DateTime from, DateTime to, string? method)
    {
        if (from.Date > to.Date)
            throw new InvalidOperationException("'from' must be on or before 'to'.");

        // Inclusive dates: [from start-of-day, to end-of-day] via an exclusive upper bound.
        var fromDate = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var query = context.Payments
            .Where(p => p.Status == "Approved" && p.PaymentDate >= fromDate && p.PaymentDate < toExclusive);

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(p => p.PaymentMethod == method);

        return query;
    }

    /// <summary>Projects payments (already ordered/paged by the caller) into journal rows.</summary>
    public static async Task<List<CollectionRowDto>> ProjectAsync(IQueryable<Payment> payments, CancellationToken ct)
    {
        var raw = await payments
            .Select(p => new
            {
                p.Id,
                p.PaymentDate,
                p.Amount,
                p.PaymentMethod,
                p.ReferenceNumber,
                p.ReviewedBy,
                p.EnrollmentId,
                p.Enrollment.GradeLevel,
                p.Enrollment.SchoolYear,
                p.Enrollment.Student.LastName,
                p.Enrollment.Student.FirstName,
                p.Enrollment.Student.MiddleName
            })
            .ToListAsync(ct);

        // Same composition as Student.FullName (computed property, not translatable in SQL).
        return raw
            .Select(p => new CollectionRowDto(
                p.Id, p.PaymentDate,
                $"{p.LastName}, {p.FirstName} {p.MiddleName}".TrimEnd(),
                p.GradeLevel, p.SchoolYear, p.Amount, p.PaymentMethod,
                p.ReferenceNumber, p.ReviewedBy, p.EnrollmentId))
            .ToList();
    }
}

public record GetCollectionsQuery(
    DateTime From, DateTime To, string? Method, int Page = 1, int PageSize = 50
) : IRequest<CollectionsDto>;

public class GetCollectionsQueryHandler : IRequestHandler<GetCollectionsQuery, CollectionsDto>
{
    private readonly IApplicationDbContext _context;

    public GetCollectionsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<CollectionsDto> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var filtered = CollectionsReport.Filter(_context, request.From, request.To, request.Method);

        // Summary always covers the FULL filtered set, independent of paging.
        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalAmount = await filtered.SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var byMethodRaw = await filtered
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byMethod = byMethodRaw
            .OrderByDescending(m => m.Amount)
            .Select(m => new CollectionMethodSummaryDto(m.Method, m.Amount, m.Count))
            .ToList();

        var byDayRaw = await filtered
            .GroupBy(p => p.PaymentDate.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);
        var byDay = byDayRaw
            .OrderBy(d => d.Date)
            .Select(d => new CollectionDaySummaryDto(d.Date, d.Amount, d.Count))
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // ThenBy Id makes ordering deterministic on PaymentDate ties, so page boundaries
        // can't duplicate/skip tied rows and the CSV export order matches the pages.
        var rows = await CollectionsReport.ProjectAsync(
            filtered.OrderBy(p => p.PaymentDate).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize),
            cancellationToken);

        return new CollectionsDto(rows, page, pageSize, totalCount, totalPages,
            new CollectionsSummaryDto(totalAmount, totalCount, byMethod, byDay));
    }
}
