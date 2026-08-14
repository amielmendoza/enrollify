using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

public record GetChildEnrollmentsQuery(Guid StudentId, Guid ParentUserId) : IRequest<List<EnrollmentDto>>;

public class GetChildEnrollmentsQueryHandler : IRequestHandler<GetChildEnrollmentsQuery, List<EnrollmentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetChildEnrollmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<EnrollmentDto>> Handle(GetChildEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(
            s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        var enrollments = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .Where(e => e.StudentId == student.Id)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return enrollments.Select(e => new EnrollmentDto(
            e.Id, e.StudentId,
            e.Student.LastName + ", " + e.Student.FirstName,
            e.SchoolYear, e.GradeLevel, e.SectionId, e.Section?.Name,
            e.Status, e.Remarks, e.PaymentPlan, e.CreatedAt,
            e.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList()))
            .ToList();
    }
}
