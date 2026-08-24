using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Common.Models;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

public record GetEnrollmentsQuery(
    string? SchoolYear, string? GradeLevel, EnrollmentStatus? Status,
    string? Search, int Page = 1, int PageSize = 20,
    bool PendingPaymentsOnly = false
) : IRequest<PagedResult<EnrollmentDto>>;

public class GetEnrollmentsQueryHandler : IRequestHandler<GetEnrollmentsQuery, PagedResult<EnrollmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EnrollmentDto>> Handle(GetEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SchoolYear))
            query = query.Where(e => e.SchoolYear == request.SchoolYear);

        if (!string.IsNullOrWhiteSpace(request.GradeLevel))
            query = query.Where(e => e.GradeLevel == request.GradeLevel);

        if (request.Status.HasValue)
            query = query.Where(e => e.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(e =>
                e.Student.FirstName.ToLower().Contains(search) ||
                e.Student.LastName.ToLower().Contains(search) ||
                e.Student.LRN.Contains(search));
        }

        // "Needs attention" view: only enrollments with at least one payment awaiting review.
        if (request.PendingPaymentsOnly)
            query = query.Where(e => e.Payments.Any(p => p.Status == "Pending"));

        var totalCount = await query.CountAsync(cancellationToken);

        var enrollments = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EnrollmentDto(
                e.Id, e.StudentId,
                e.Student.LastName + ", " + e.Student.FirstName,
                e.SchoolYear, e.GradeLevel, e.SectionId, e.Section != null ? e.Section.Name : null,
                e.Status, e.Remarks, e.PaymentPlan, e.CreatedAt, null,
                // Projected as a correlated Count subquery — no N+1.
                e.Payments.Count(p => p.Status == "Pending")))
            .ToListAsync(cancellationToken);

        return new PagedResult<EnrollmentDto>
        {
            Items = enrollments,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
