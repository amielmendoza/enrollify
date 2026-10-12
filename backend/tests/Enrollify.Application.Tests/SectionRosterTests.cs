using Enrollify.Application.Features.Sections.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Section roster: non-cancelled enrollments assigned to the section, ordered by student
/// name, with CurrentCount matching Section.CurrentCount semantics (cancelled hold no seats).
/// </summary>
public class SectionRosterTests
{
    [Fact]
    public async Task Roster_ExcludesCancelledAndOtherSections_OrderedByName_CountMatchesSeats()
    {
        var ctx = TestDb.Create();

        var section = new Section { Name = "Einstein", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 };
        var otherSection = new Section { Name = "Newton", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 };
        ctx.Sections.AddRange(section, otherSection);

        var zoe = new Student { LRN = "ROS-0001", FirstName = "Zoe", LastName = "Reyes", Gender = "Female", Address = "Manila" };
        var ana = new Student { LRN = "ROS-0002", FirstName = "Ana", LastName = "Cruz", Gender = "Female", Address = "Manila" };
        var ben = new Student { LRN = "ROS-0003", FirstName = "Ben", LastName = "Diaz", Gender = "Male", Address = "Manila" };
        var eve = new Student { LRN = "ROS-0004", FirstName = "Eve", LastName = "Lopez", Gender = "Female", Address = "Manila" };
        ctx.Students.AddRange(zoe, ana, ben, eve);

        ctx.Enrollments.AddRange(
            new Enrollment { StudentId = zoe.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", SectionId = section.Id, Status = EnrollmentStatus.Enrolled },
            new Enrollment { StudentId = ana.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", SectionId = section.Id, Status = EnrollmentStatus.Approved },
            // Cancelled but still pointing at the section (legacy shape) — must not appear or count.
            new Enrollment { StudentId = ben.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", SectionId = section.Id, Status = EnrollmentStatus.Cancelled },
            // Seated in a different section — must not appear.
            new Enrollment { StudentId = eve.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", SectionId = otherSection.Id, Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var dto = await new GetSectionRosterQueryHandler(ctx).Handle(new GetSectionRosterQuery(section.Id), default);

        Assert.Equal(section.Id, dto.Section.Id);
        Assert.Equal("Einstein", dto.Section.Name);
        Assert.Equal("Grade 7", dto.Section.GradeLevel);
        Assert.Equal("2025-2026", dto.Section.SchoolYear);
        Assert.Equal(40, dto.Section.Capacity);
        Assert.Equal(2, dto.Section.CurrentCount);  // cancelled seat not counted — same as Section.CurrentCount

        Assert.Equal(new[] { "Cruz, Ana", "Reyes, Zoe" }, dto.Students.Select(s => s.Name).ToArray()); // name order
        Assert.Equal(new[] { "Approved", "Enrolled" }, dto.Students.Select(s => s.Status).ToArray());
        Assert.All(dto.Students, s => Assert.NotEqual("Cancelled", s.Status));
        Assert.DoesNotContain(dto.Students, s => s.Lrn == "ROS-0003"); // cancelled
        Assert.DoesNotContain(dto.Students, s => s.Lrn == "ROS-0004"); // other section
        Assert.Equal("Female", dto.Students.First(s => s.Lrn == "ROS-0002").Gender);
    }

    [Fact]
    public async Task Roster_EmptySection_ReturnsZeroStudents()
    {
        var ctx = TestDb.Create();
        var section = new Section { Name = "Curie", GradeLevel = "Grade 8", SchoolYear = "2025-2026", Capacity = 30 };
        ctx.Sections.Add(section);
        await ctx.SaveChangesAsync();

        var dto = await new GetSectionRosterQueryHandler(ctx).Handle(new GetSectionRosterQuery(section.Id), default);

        Assert.Empty(dto.Students);
        Assert.Equal(0, dto.Section.CurrentCount);
    }

    [Fact]
    public async Task Roster_SectionNotFound_Throws()
    {
        var ctx = TestDb.Create();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => new GetSectionRosterQueryHandler(ctx).Handle(new GetSectionRosterQuery(Guid.NewGuid()), default));
    }

    // The printed class list is signed by the adviser — the roster carries their name.
    [Fact]
    public async Task Roster_IncludesTheSectionAdviser()
    {
        var ctx = TestDb.Create();
        var section = new Section { Name = "Rizal", GradeLevel = "Grade 8", SchoolYear = "2025-2026", Capacity = 35, Adviser = "Ms. Liza Cruz" };
        ctx.Sections.Add(section);
        await ctx.SaveChangesAsync();

        var dto = await new GetSectionRosterQueryHandler(ctx).Handle(new GetSectionRosterQuery(section.Id), default);

        Assert.Equal("Ms. Liza Cruz", dto.Section.Adviser);
    }
}
