using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Fees;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Fees.Queries;

public record GetFeesQuery(string? SchoolYear, string? GradeLevel) : IRequest<List<FeeDto>>;

public class GetFeesQueryHandler : IRequestHandler<GetFeesQuery, List<FeeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetFeesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FeeDto>> Handle(GetFeesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Fees.Where(f => f.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SchoolYear))
            query = query.Where(f => f.SchoolYear == request.SchoolYear);

        if (!string.IsNullOrWhiteSpace(request.GradeLevel))
            query = query.Where(f => f.GradeLevel == request.GradeLevel);

        return await query
            .OrderBy(f => f.Name)
            .Select(f => new FeeDto(f.Id, f.Name, f.Description, f.Amount, f.SchoolYear, f.GradeLevel, f.IsActive))
            .ToListAsync(cancellationToken);
    }
}
