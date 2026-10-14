using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Common.Models;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetStudentsQuery(
    string? Search, int Page = 1, int PageSize = 20, bool IncludeInactive = false,
    bool MissingLrn = false, bool LrnNeedsReview = false) : IRequest<PagedResult<StudentDto>>;

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, PagedResult<StudentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStudentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Students.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        if (request.MissingLrn)
            query = query.Where(s => s.LRN == null);

        if (request.LrnNeedsReview)
            query = query.Where(s => s.LrnNeedsReview);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(search) ||
                s.LastName.ToLower().Contains(search) ||
                s.StudentNumber.Contains(search) ||
                (s.LRN != null && s.LRN.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Materialize then map: the DTO carries the learner profile (owned address, split
        // disability list), which isn't translatable inside a SQL projection. Pages are small.
        var students = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentDto>
        {
            Items = students.Select(StudentMapping.ToDto).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
