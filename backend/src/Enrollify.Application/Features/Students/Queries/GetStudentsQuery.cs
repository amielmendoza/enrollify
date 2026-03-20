using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Common.Models;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetStudentsQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<PagedResult<StudentDto>>;

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, PagedResult<StudentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStudentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Students.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(search) ||
                s.LastName.ToLower().Contains(search) ||
                s.LRN.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var students = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new StudentDto(s.Id, s.LRN, s.FirstName, s.MiddleName, s.LastName,
                s.BirthDate, s.Gender, s.Address, s.ContactNumber, s.Email,
                s.GuardianName, s.GuardianContact, s.LastName + ", " + s.FirstName + " " + s.MiddleName))
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentDto>
        {
            Items = students,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
