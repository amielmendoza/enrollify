using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetMyProfileQuery(Guid UserId) : IRequest<StudentDto>;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyProfileQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var s = await _context.Students.FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student profile not found.");

        return StudentMapping.ToDto(s);
    }
}
