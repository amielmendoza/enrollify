using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Queries;

public record GetApplicationByIdQuery(Guid Id) : IRequest<ApplicationDetailDto>;

public class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, ApplicationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetApplicationByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationDetailDto> Handle(GetApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        var a = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        return ApplicationMapping.ToDetailDto(a);
    }
}
