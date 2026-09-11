using Enrollify.Application.Features.Dashboard;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Dashboard stats are scoped to one school year: the active year by default, or an
/// explicitly requested year. TotalStudents stays year-independent.
/// </summary>
public class DashboardStatsScopeTests
{
    private static async Task<ApplicationDbContext> SeedTwoYearsAsync()
    {
        var ctx = TestDb.Create();

        ctx.SchoolYears.AddRange(
            new SchoolYear { Name = "2024-2025", StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 3, 31), IsActive = false },
            new SchoolYear { Name = "2025-2026", StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });

        var s1 = new Student { LRN = "LRN-DASH-1", FirstName = "Ana", LastName = "One", Address = "Manila" };
        var s2 = new Student { LRN = "LRN-DASH-2", FirstName = "Ben", LastName = "Two", Address = "Manila" };
        ctx.Students.AddRange(s1, s2);

        // Active year 2025-2026: one Enrolled + one Draft enrollment, one active section,
        // payments: 1000 approved + 200 pending.
        var enrolledNow = new Enrollment { StudentId = s1.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Enrolled };
        var draftNow = new Enrollment { StudentId = s2.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 8", Status = EnrollmentStatus.Draft };
        // Prior year 2024-2025: one Enrolled enrollment, one active section, 500 approved.
        var enrolledOld = new Enrollment { StudentId = s1.Id, SchoolYear = "2024-2025", GradeLevel = "Grade 6", Status = EnrollmentStatus.Enrolled };
        ctx.Enrollments.AddRange(enrolledNow, draftNow, enrolledOld);

        ctx.Sections.AddRange(
            new Section { Name = "New-A", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 },
            new Section { Name = "Old-A", GradeLevel = "Grade 6", SchoolYear = "2024-2025", Capacity = 40 });

        ctx.Payments.AddRange(
            new Payment { EnrollmentId = enrolledNow.Id, Amount = 1000m, PaymentMethod = "Cash", Status = "Approved" },
            new Payment { EnrollmentId = enrolledNow.Id, Amount = 200m, PaymentMethod = "Cash", Status = "Pending" },
            new Payment { EnrollmentId = enrolledOld.Id, Amount = 500m, PaymentMethod = "Cash", Status = "Approved" });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task NullSchoolYear_ResolvesActiveYear_AndScopesCounts()
    {
        var ctx = await SeedTwoYearsAsync();
        var handler = new GetDashboardStatsQueryHandler(ctx);

        var dto = await handler.Handle(new GetDashboardStatsQuery(), default);

        Assert.Equal("2025-2026", dto.SchoolYear);   // resolved active year echoed back
        Assert.Equal(2, dto.TotalStudents);          // year-independent
        Assert.Equal(2, dto.TotalEnrollments);       // 2024-2025 enrollment excluded
        Assert.Equal(1, dto.DraftEnrollments);
        Assert.Equal(0, dto.ApprovedEnrollments);
        Assert.Equal(1, dto.EnrolledCount);
        Assert.Equal(1, dto.TotalSections);          // only the 2025-2026 section
        Assert.Equal(1000m, dto.TotalRevenue);       // prior-year payment excluded
        Assert.Equal(1, dto.PendingPayments);
    }

    [Fact]
    public async Task ExplicitSchoolYear_ScopesToThatYear()
    {
        var ctx = await SeedTwoYearsAsync();
        var handler = new GetDashboardStatsQueryHandler(ctx);

        var dto = await handler.Handle(new GetDashboardStatsQuery("2024-2025"), default);

        Assert.Equal("2024-2025", dto.SchoolYear);
        Assert.Equal(2, dto.TotalStudents);          // still year-independent
        Assert.Equal(1, dto.TotalEnrollments);
        Assert.Equal(0, dto.DraftEnrollments);
        Assert.Equal(1, dto.EnrolledCount);
        Assert.Equal(1, dto.TotalSections);
        Assert.Equal(500m, dto.TotalRevenue);
        Assert.Equal(0, dto.PendingPayments);
    }

    [Fact]
    public async Task NoActiveYearAndNoneGiven_FallsBackToAllYears()
    {
        var ctx = await SeedTwoYearsAsync();
        var active = ctx.SchoolYears.Single(sy => sy.IsActive);
        active.IsActive = false;
        await ctx.SaveChangesAsync();

        var handler = new GetDashboardStatsQueryHandler(ctx);
        var dto = await handler.Handle(new GetDashboardStatsQuery(), default);

        Assert.Null(dto.SchoolYear);
        Assert.Equal(3, dto.TotalEnrollments);
        Assert.Equal(2, dto.TotalSections);
        Assert.Equal(1500m, dto.TotalRevenue);
    }
}
