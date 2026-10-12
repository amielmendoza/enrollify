using Enrollify.Application.Features.Admissions.Queries;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// The student page lists one learner's enrollments and applications — both list queries
/// must be filterable by student so the page never pulls (or shows) anyone else's records.
/// </summary>
public class StudentCrossLinkTests
{
    private static Student S(string lrn, string first) => new() { LRN = lrn, FirstName = first, LastName = "Cruz", Address = "" };

    [Fact]
    public async Task Enrollments_FilteredByStudent_ReturnOnlyThatStudentsRows_AllYearsAndStatuses()
    {
        var ctx = TestDb.Create();
        var ana = S("XL-0001", "Ana");
        var ben = S("XL-0002", "Ben");
        ctx.Students.AddRange(ana, ben);
        ctx.Enrollments.AddRange(
            new Enrollment { StudentId = ana.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Enrolled },
            new Enrollment { StudentId = ana.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 8", Status = EnrollmentStatus.Cancelled },
            new Enrollment { StudentId = ben.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 8", Status = EnrollmentStatus.Draft });
        await ctx.SaveChangesAsync();

        var page = await new GetEnrollmentsQueryHandler(ctx).Handle(
            new GetEnrollmentsQuery(null, null, null, null, StudentId: ana.Id), default);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, e => Assert.Equal(ana.Id, e.StudentId));
    }

    [Fact]
    public async Task Applications_FilteredByStudent_ReturnOnlyThatStudentsApplications()
    {
        var ctx = TestDb.Create();
        var anaId = Guid.NewGuid();
        AdmissionApplication App(Guid? studentId, string first) => new()
        {
            ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8]}", FirstName = first, LastName = "Cruz", Email = "",
            Gender = "Female", GradeLevel = "Grade 7", SchoolYear = "2026-2027", Status = "Approved", StudentId = studentId
        };
        ctx.AdmissionApplications.AddRange(App(anaId, "Ana"), App(Guid.NewGuid(), "Ben"), App(null, "Cy"));
        await ctx.SaveChangesAsync();

        var page = await new GetApplicationsQueryHandler(ctx).Handle(
            new GetApplicationsQuery(null, null, StudentId: anaId), default);

        var only = Assert.Single(page.Items);
        Assert.StartsWith("Cruz, Ana", only.FullName);
    }
}
