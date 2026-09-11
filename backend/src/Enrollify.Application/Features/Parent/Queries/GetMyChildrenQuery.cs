using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Parent.Queries;

public record GetMyChildrenQuery(Guid ParentUserId) : IRequest<List<ParentChildDto>>;

public record ParentChildDto(
    Guid? StudentId,
    Guid? ApplicationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string FullName,
    string? GradeLevel,
    string? SchoolYear,
    string? Status,
    string Source, // "Application" (still pending) or "Student" (admitted)
    bool HasActiveYearEnrollment = false);

public class GetMyChildrenQueryHandler : IRequestHandler<GetMyChildrenQuery, List<ParentChildDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyChildrenQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ParentChildDto>> Handle(GetMyChildrenQuery request, CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .Where(s => s.ParentUserId == request.ParentUserId)
            .ToListAsync(cancellationToken);

        var studentIds = students.Select(s => s.Id).ToList();

        var enrollmentsByStudent = await _context.Enrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .ToListAsync(cancellationToken);

        // Pending or rejected applications that haven't yet produced a Student record
        var pendingApps = await _context.AdmissionApplications
            .Where(a => a.ParentUserId == request.ParentUserId && a.StudentId == null)
            .ToListAsync(cancellationToken);

        var activeYear = await _context.SchoolYears
            .Where(sy => sy.IsActive)
            .Select(sy => sy.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var children = new List<ParentChildDto>();

        foreach (var s in students)
        {
            // Prefer the ACTIVE school year's enrollment (so after rollover the card shows the
            // current year's status, e.g. a Draft awaiting requirements) and fall back to the
            // latest enrollment overall for students not yet re-enrolled.
            var activeYearEnrollment = activeYear == null
                ? null
                : enrollmentsByStudent
                    .Where(e => e.StudentId == s.Id && e.SchoolYear == activeYear && e.Status != EnrollmentStatus.Cancelled)
                    .OrderByDescending(e => e.CreatedAt)
                    .FirstOrDefault();

            var shown = activeYearEnrollment ?? enrollmentsByStudent
                .Where(e => e.StudentId == s.Id)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();

            children.Add(new ParentChildDto(
                StudentId: s.Id,
                ApplicationId: null,
                FirstName: s.FirstName,
                MiddleName: s.MiddleName,
                LastName: s.LastName,
                FullName: s.FullName,
                GradeLevel: shown?.GradeLevel,
                SchoolYear: shown?.SchoolYear,
                Status: shown?.Status.ToString() ?? "Admitted",
                Source: "Student",
                HasActiveYearEnrollment: activeYearEnrollment != null));
        }

        foreach (var a in pendingApps)
        {
            children.Add(new ParentChildDto(
                StudentId: null,
                ApplicationId: a.Id,
                FirstName: a.FirstName,
                MiddleName: a.MiddleName,
                LastName: a.LastName,
                FullName: $"{a.LastName}, {a.FirstName} {a.MiddleName}".TrimEnd(),
                GradeLevel: a.GradeLevel,
                SchoolYear: a.SchoolYear,
                Status: a.Status,
                Source: "Application"));
        }

        return children
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToList();
    }
}
