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

        return new ApplicationDetailDto(
            a.Id, a.ApplicationNumber, a.FirstName, a.MiddleName, a.LastName,
            a.Email, a.ContactNumber, a.Gender, a.DateOfBirth, a.Address,
            a.GradeLevel, a.SchoolYear, a.PreviousSchool, a.PreviousSchoolAddress,
            a.GuardianName, a.GuardianContact, a.GuardianRelationship,
            a.Status, a.CreatedAt, a.ReviewedAt, a.ReviewNotes, a.StudentId);
    }
}
