using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Common.Models;
using Enrollify.Application.DTOs.Admissions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Queries;

public record GetApplicationsQuery(string? Status, string? Search, int Page = 1, int PageSize = 20) : IRequest<PagedResult<ApplicationListDto>>;

public class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, PagedResult<ApplicationListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ApplicationListDto>> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AdmissionApplications.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(a => a.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(a =>
                a.FirstName.ToLower().Contains(search) ||
                a.LastName.ToLower().Contains(search) ||
                a.ApplicationNumber.ToLower().Contains(search) ||
                a.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new ApplicationListDto(
                a.Id, a.ApplicationNumber,
                a.LastName + ", " + a.FirstName + " " + (a.MiddleName ?? ""),
                a.Email, a.GradeLevel, a.SchoolYear, a.Status, a.CreatedAt, a.ReviewedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ApplicationListDto>
        {
            Items = items, TotalCount = totalCount, Page = request.Page, PageSize = request.PageSize
        };
    }
}
