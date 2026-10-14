using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Sections.Queries;

public record RosterSectionDto(
    Guid Id, string Name, string GradeLevel, string SchoolYear, int Capacity, int CurrentCount, string? Adviser = null);

public record RosterStudentDto(
    Guid StudentId, Guid EnrollmentId, string? Lrn, string Name, string? Gender, string Status, string StudentNumber = "");

public record SectionRosterDto(RosterSectionDto Section, List<RosterStudentDto> Students);

public record GetSectionRosterQuery(Guid SectionId) : IRequest<SectionRosterDto>;

/// <summary>
/// Class list for one section: non-cancelled enrollments assigned to it, ordered by student
/// name. CurrentCount is the roster size — the same "cancelled don't hold seats" semantics
/// as Section.CurrentCount.
/// </summary>
public class GetSectionRosterQueryHandler : IRequestHandler<GetSectionRosterQuery, SectionRosterDto>
{
    private readonly IApplicationDbContext _context;

    public GetSectionRosterQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<SectionRosterDto> Handle(GetSectionRosterQuery request, CancellationToken cancellationToken)
    {
        var section = await _context.Sections
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var raw = await _context.Enrollments
            .Where(e => e.SectionId == section.Id && e.Status != EnrollmentStatus.Cancelled)
            .OrderBy(e => e.Student.LastName)
            .ThenBy(e => e.Student.FirstName)
            .ThenBy(e => e.Student.MiddleName)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.StudentId,
                EnrollmentId = e.Id,
                e.Student.LRN,
                e.Student.StudentNumber,
                e.Student.LastName,
                e.Student.FirstName,
                e.Student.MiddleName,
                e.Student.Gender,
                e.Status
            })
            .ToListAsync(cancellationToken);

        var students = raw
            .Select(r => new RosterStudentDto(
                r.StudentId, r.EnrollmentId, r.LRN,
                $"{r.LastName}, {r.FirstName} {r.MiddleName}".TrimEnd(),
                r.Gender, r.Status.ToString(), StudentNumber: r.StudentNumber))
            .ToList();

        return new SectionRosterDto(
            new RosterSectionDto(section.Id, section.Name, section.GradeLevel, section.SchoolYear,
                section.Capacity, students.Count, section.Adviser),
            students);
    }
}
