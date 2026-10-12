using Enrollify.Application.Features.Admissions.Queries;
using Enrollify.Application.Features.Dashboard;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// "Pending" means awaiting a decision: Submitted OR UnderReview. Marking an application under
/// review must not make it vanish from the dashboard tile, and the tile's deep link
/// (/admissions?status=Pending) must list exactly what the tile counts.
/// </summary>
public class PendingApplicationsTests
{
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var ctx = TestDb.Create();
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31), IsActive = true });
        foreach (var status in new[] { "Submitted", "UnderReview", "Approved", "Rejected" })
        {
            ctx.AdmissionApplications.Add(new AdmissionApplication
            {
                ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8]}", FirstName = status, LastName = "Cruz", Email = "",
                Gender = "Female", GradeLevel = "Grade 7", SchoolYear = "2026-2027", Status = status
            });
        }
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task DashboardPendingCount_IncludesUnderReview()
    {
        var ctx = await SeedAsync();
        var dto = await new GetDashboardStatsQueryHandler(ctx).Handle(new GetDashboardStatsQuery(), default);
        Assert.Equal(2, dto.PendingApplications);
    }

    [Fact]
    public async Task PendingStatusFilter_ListsSubmittedAndUnderReview()
    {
        var ctx = await SeedAsync();
        var page = await new GetApplicationsQueryHandler(ctx).Handle(new GetApplicationsQuery("Pending", null), default);

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, a => a.Status == "Submitted");
        Assert.Contains(page.Items, a => a.Status == "UnderReview");
    }
}
