using Enrollify.Application.Features.Reports;
using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Quick count: grade/sex grid counted on a Manila day, change vs the day before, NotSet list,
/// row ordering, CSV, and the asOf default.
/// </summary>
public class QuickCountReportTests
{
    private const string Year = "2025-2026";
    private static readonly DateOnly AsOf = new(2026, 3, 10);

    private static ApplicationDbContext NewDb()
    {
        var ctx = TestDb.Create();
        ctx.Tenants.Add(new Tenant
        {
            Id = TestDb.TenantId, Name = "Metro Science HS", Subdomain = "mshs",
            DepEdSchoolId = "301234", DepEdRegionName = "NCR", DepEdDivision = "Manila", DepEdDistrict = "District 3"
        });
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = Year, StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true
        });
        return ctx;
    }

    /// <summary>UTC instant in the middle of Manila day <paramref name="d"/> (10:00 UTC = 18:00 Manila).</summary>
    private static DateTime During(DateOnly d) => new(d.Year, d.Month, d.Day, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>An enrollment whose history is Approved on <paramref name="approvedOn"/>, optionally Cancelled later.</summary>
    private static Enrollment Add(
        ApplicationDbContext ctx, string grade, string? gender, DateOnly approvedOn,
        string last = "Cruz", string first = "Ana", DateOnly? cancelledOn = null, string year = Year)
    {
        var st = new Student { FirstName = first, LastName = last, Gender = gender, Address = "Manila" };
        ctx.Students.Add(st);
        var e = new Enrollment
        {
            StudentId = st.Id, SchoolYear = year, GradeLevel = grade,
            Status = cancelledOn is null ? EnrollmentStatus.Approved : EnrollmentStatus.Cancelled
        };
        ctx.Enrollments.Add(e);
        ctx.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = e.Id, FromStatus = EnrollmentStatus.Assessed, ToStatus = EnrollmentStatus.Approved,
            TransitionDate = During(approvedOn)
        });
        if (cancelledOn is { } c)
            ctx.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
            {
                EnrollmentId = e.Id, FromStatus = EnrollmentStatus.Approved, ToStatus = EnrollmentStatus.Cancelled,
                TransitionDate = During(c)
            });
        return e;
    }

    private static Task<QuickCountDto> Run(ApplicationDbContext ctx, DateOnly? asOf = null, string? year = Year) =>
        new GetQuickCountQueryHandler(ctx).Handle(new GetQuickCountQuery(TestDb.TenantId, year, asOf), CancellationToken.None);

    [Fact]
    public async Task Header_comes_from_tenant_and_year_and_asOf_is_echoed()
    {
        var ctx = NewDb();
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf, year: null);

        Assert.Equal(new SchoolHeaderDto("Metro Science HS", "301234", "NCR", "Manila", "District 3", Year), result.Header);
        Assert.Equal(AsOf, result.AsOf);
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.Total.Total);
        Assert.Equal("Total", result.Total.GradeLevel);
    }

    [Fact]
    public async Task Counts_by_grade_and_sex_with_sex_normalized()
    {
        var ctx = NewDb();
        var old = AsOf.AddDays(-5);
        Add(ctx, "Grade 7", "Male", old);
        Add(ctx, "Grade 7", "m", old);
        Add(ctx, "Grade 7", "Female", old);
        Add(ctx, "Grade 7", null, old);
        Add(ctx, "Grade 8", "F", old);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        var g7 = Assert.Single(result.Rows, r => r.GradeLevel == "Grade 7");
        Assert.Equal((2, 1, 1, 4), (g7.Male, g7.Female, g7.NotSet, g7.Total));
        Assert.Equal((0, 0, 0, 0), (g7.MaleChange, g7.FemaleChange, g7.NotSetChange, g7.TotalChange));
        var g8 = Assert.Single(result.Rows, r => r.GradeLevel == "Grade 8");
        Assert.Equal((0, 1, 0, 1), (g8.Male, g8.Female, g8.NotSet, g8.Total));
        Assert.Equal((2, 2, 1, 5), (result.Total.Male, result.Total.Female, result.Total.NotSet, result.Total.Total));
    }

    [Fact]
    public async Task Rows_follow_canonical_grade_order_then_unknown_grades_alphabetically()
    {
        var ctx = NewDb();
        var old = AsOf.AddDays(-5);
        Add(ctx, "Grade 10", "Male", old);
        Add(ctx, "Zeta Grade", "Male", old);
        Add(ctx, "Kindergarten", "Male", old);
        Add(ctx, "Alpha Grade", "Male", old);
        Add(ctx, "Grade 2", "Male", old);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        Assert.Equal(new[] { "Kindergarten", "Grade 2", "Grade 10", "Alpha Grade", "Zeta Grade" },
            result.Rows.Select(r => r.GradeLevel).ToArray());
    }

    [Fact]
    public async Task Not_set_column_and_not_set_learner_list()
    {
        var ctx = NewDb();
        var old = AsOf.AddDays(-5);
        var blank = Add(ctx, "Grade 7", "   ", old, last: "Reyes", first: "Jo");
        var unknown = Add(ctx, "Grade 9", "Other", old, last: "Lim", first: "Kai");
        Add(ctx, "Grade 7", "Male", old);
        // Cancelled before asOf: not counted, so must not be listed.
        Add(ctx, "Grade 7", null, AsOf.AddDays(-6), last: "Gone", first: "Al", cancelledOn: AsOf.AddDays(-2));
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        Assert.Equal(2, result.Total.NotSet);
        Assert.Equal(1, result.Rows.Single(r => r.GradeLevel == "Grade 7").NotSet);
        Assert.Equal(2, result.NotSetLearners.Count);
        var jo = Assert.Single(result.NotSetLearners, n => n.EnrollmentId == blank.Id);
        Assert.Equal(("Reyes, Jo", "Grade 7", blank.StudentId), (jo.Name, jo.GradeLevel, jo.StudentId));
        var kai = Assert.Single(result.NotSetLearners, n => n.EnrollmentId == unknown.Id);
        Assert.Equal("Grade 9", kai.GradeLevel);
    }

    [Fact]
    public async Task Change_is_asOf_minus_previous_day()
    {
        var ctx = NewDb();
        // Counted both days.
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-5));
        // Approved on asOf: counts on asOf, not the day before -> +1.
        Add(ctx, "Grade 7", "Female", AsOf);
        Add(ctx, "Grade 7", null, AsOf);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        var g7 = Assert.Single(result.Rows);
        Assert.Equal((1, 1, 1, 3), (g7.Male, g7.Female, g7.NotSet, g7.Total));
        Assert.Equal((0, 1, 1, 2), (g7.MaleChange, g7.FemaleChange, g7.NotSetChange, g7.TotalChange));
        Assert.Equal(2, result.Total.TotalChange);
    }

    [Fact]
    public async Task Learner_cancelled_after_approval_counts_before_but_not_after()
    {
        var ctx = NewDb();
        Add(ctx, "Grade 8", "Male", AsOf.AddDays(-5), cancelledOn: AsOf.AddDays(-2));  // gone before the day before
        Add(ctx, "Grade 8", "Male", AsOf.AddDays(-5), cancelledOn: AsOf);              // counted day before, not on asOf
        Add(ctx, "Grade 8", "Female", AsOf.AddDays(-5), cancelledOn: AsOf.AddDays(1)); // still counted on asOf
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        var g8 = Assert.Single(result.Rows);
        Assert.Equal((0, 1, 0, 1), (g8.Male, g8.Female, g8.NotSet, g8.Total));
        Assert.Equal((-1, 0, 0, -1), (g8.MaleChange, g8.FemaleChange, g8.NotSetChange, g8.TotalChange));
    }

    [Fact]
    public async Task Grade_with_only_previous_day_count_still_gets_a_row_with_negative_change()
    {
        var ctx = NewDb();
        Add(ctx, "Grade 9", "Female", AsOf.AddDays(-5), cancelledOn: AsOf);
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        var g9 = Assert.Single(result.Rows);
        Assert.Equal("Grade 9", g9.GradeLevel);
        Assert.Equal((0, 0, 0, 0), (g9.Male, g9.Female, g9.NotSet, g9.Total));
        Assert.Equal((0, -1, 0, -1), (g9.MaleChange, g9.FemaleChange, g9.NotSetChange, g9.TotalChange));
    }

    [Fact]
    public async Task Each_enrollment_is_evaluated_against_its_own_history_only()
    {
        var ctx = NewDb();
        // A is cancelled on asOf-3; B approved on asOf-5 and never cancelled. If A's cancellation leaked
        // into B (shared history list), B would not be counted.
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-6), cancelledOn: AsOf.AddDays(-3));
        Add(ctx, "Grade 7", "Female", AsOf.AddDays(-5));
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        Assert.Equal((0, 1), (result.Total.Male, result.Total.Female));
    }

    [Fact]
    public async Task Other_school_years_are_excluded()
    {
        var ctx = NewDb();
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-5));
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-5), year: "2024-2025");
        await ctx.SaveChangesAsync();

        var result = await Run(ctx, AsOf);

        Assert.Equal(1, result.Total.Total);
    }

    [Fact]
    public async Task AsOf_defaults_to_manila_today()
    {
        var ctx = NewDb();
        await ctx.SaveChangesAsync();

        var before = DateOnly.FromDateTime(DepEdRules.ManilaDate(DateTime.UtcNow));
        var result = await Run(ctx, null);
        var after = DateOnly.FromDateTime(DepEdRules.ManilaDate(DateTime.UtcNow));

        Assert.True(result.AsOf == before || result.AsOf == after);
    }

    [Fact]
    public async Task Export_query_returns_the_same_report()
    {
        var ctx = NewDb();
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-5));
        Add(ctx, "Grade 7", null, AsOf);
        await ctx.SaveChangesAsync();

        var paged = await Run(ctx, AsOf);
        var export = await new GetQuickCountExportQueryHandler(ctx)
            .Handle(new GetQuickCountExportQuery(TestDb.TenantId, Year, AsOf), CancellationToken.None);

        Assert.Equal(paged.Total, export.Total);
        Assert.Equal(paged.Rows, export.Rows);
        Assert.Equal(paged.NotSetLearners, export.NotSetLearners);
        Assert.Equal(paged.Header, export.Header);
    }

    [Fact]
    public async Task Csv_has_header_one_row_per_grade_and_a_total_row()
    {
        var ctx = NewDb();
        Add(ctx, "Grade 7", "Male", AsOf.AddDays(-5));
        Add(ctx, "Grade 7", "Female", AsOf);
        Add(ctx, "Grade 8", "Female", AsOf.AddDays(-5), cancelledOn: AsOf);
        Add(ctx, "=Evil", "Male", AsOf.AddDays(-5));
        await ctx.SaveChangesAsync();

        var csv = QuickCountCsvBuilder.Build(await Run(ctx, AsOf));
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Grade Level,Male,Female,Sex Not Set,Total,Male Change,Female Change,Sex Not Set Change,Total Change", lines[0]);
        Assert.Equal(QuickCountCsvBuilder.Header, lines[0]);
        Assert.Equal("Grade 7,1,1,0,2,0,1,0,1", lines[1]);
        Assert.Equal("Grade 8,0,0,0,0,0,-1,0,-1", lines[2]);
        Assert.Equal("'=Evil,1,0,0,1,0,0,0,0", lines[3]);
        Assert.Equal("Total,2,1,0,3,0,0,0,0", lines[4]);
        Assert.Equal(5, lines.Length);
    }
}
