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
    string Source); // "Application" (still pending) or "Student" (admitted)

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

        var children = new List<ParentChildDto>();

        foreach (var s in students)
        {
            var latest = enrollmentsByStudent
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
                GradeLevel: latest?.GradeLevel,
                SchoolYear: latest?.SchoolYear,
                Status: latest?.Status.ToString() ?? "Admitted",
                Source: "Student"));
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
