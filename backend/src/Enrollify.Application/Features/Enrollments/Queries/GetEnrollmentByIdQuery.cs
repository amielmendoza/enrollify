using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

public record GetEnrollmentByIdQuery(Guid Id) : IRequest<EnrollmentDto>;

public class GetEnrollmentByIdQueryHandler : IRequestHandler<GetEnrollmentByIdQuery, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto> Handle(GetEnrollmentByIdQuery request, CancellationToken cancellationToken)
    {
        var e = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        return new EnrollmentDto(e.Id, e.StudentId, e.Student.FullName,
            e.SchoolYear, e.GradeLevel, e.SectionId, e.Section?.Name,
            e.Status, e.Remarks, e.PaymentPlan, e.CreatedAt,
            e.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }
}
