using Enrollify.Application.Features.Parent.Queries;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class RolloverVisibilityTests
{
    private const string OldYear = "2024-2025";
    private const string ActiveYear = "2025-2026";

    [Fact]
    public async Task GetMyChildren_PrefersActiveYearEnrollment_AndFlagsIt()
    {
        var ctx = TestDb.Create();
        var parentId = Guid.NewGuid();
        var reEnrolled = new Student { LRN = "LRN-1101", FirstName = "Amy", LastName = "Alpha", Address = "Manila", ParentUserId = parentId };
        var notReEnrolled = new Student { LRN = "LRN-1102", FirstName = "Ben", LastName = "Beta", Address = "Manila", ParentUserId = parentId };
        ctx.Students.AddRange(reEnrolled, notReEnrolled);
        ctx.SchoolYears.Add(new SchoolYear { Name = ActiveYear, StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });

        ctx.Enrollments.Add(new Enrollment { StudentId = reEnrolled.Id, SchoolYear = OldYear, GradeLevel = "Grade 7", Status = EnrollmentStatus.Enrolled });
        ctx.Enrollments.Add(new Enrollment { StudentId = reEnrolled.Id, SchoolYear = ActiveYear, GradeLevel = "Grade 8", Status = EnrollmentStatus.Draft });
        ctx.Enrollments.Add(new Enrollment { StudentId = notReEnrolled.Id, SchoolYear = OldYear, GradeLevel = "Grade 5", Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var children = await new GetMyChildrenQueryHandler(ctx).Handle(new GetMyChildrenQuery(parentId), default);

        var amy = children.Single(c => c.StudentId == reEnrolled.Id);
        Assert.Equal(ActiveYear, amy.SchoolYear);
        Assert.Equal("Grade 8", amy.GradeLevel);
        Assert.Equal("Draft", amy.Status);
        Assert.True(amy.HasActiveYearEnrollment);

        var ben = children.Single(c => c.StudentId == notReEnrolled.Id);
        Assert.Equal(OldYear, ben.SchoolYear);
        Assert.Equal("Grade 5", ben.GradeLevel);
        Assert.Equal("Enrolled", ben.Status);
        Assert.False(ben.HasActiveYearEnrollment);
    }

    // ----- Two-year money seed shared by the payments/ledger tests -----

    private static async Task<(ApplicationDbContext Ctx, Guid UserId, Guid ParentId, Student Student)> SeedTwoYearStudentAsync()
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var student = new Student
        {
            LRN = "LRN-1103", FirstName = "Cara", LastName = "Gamma", Address = "Manila",
            UserId = userId, ParentUserId = parentId
        };
        ctx.Students.Add(student);
        ctx.SchoolYears.Add(new SchoolYear { Name = ActiveYear, StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });

        var oldEnrollment = new Enrollment
        {
            StudentId = student.Id, SchoolYear = OldYear, GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Enrolled, AssessedTotal = 5000m, AssessedAt = new DateTime(2024, 6, 15)
        };
        var currentEnrollment = new Enrollment
        {
            StudentId = student.Id, SchoolYear = ActiveYear, GradeLevel = "Grade 8",
            Status = EnrollmentStatus.Approved, AssessedTotal = 8000m, AssessedAt = new DateTime(2025, 6, 15)
        };
        ctx.Enrollments.AddRange(oldEnrollment, currentEnrollment);

        // Snapshots so the ledger has charge lines.
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = oldEnrollment.Id, Name = "Tuition", Amount = 5000m });
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = currentEnrollment.Id, Name = "Tuition", Amount = 8000m });

        // Old year fully paid; current year 2000 of 8000.
        ctx.Payments.Add(new Payment { EnrollmentId = oldEnrollment.Id, Amount = 5000m, PaymentMethod = "Cash", Status = "Approved", PaymentDate = new DateTime(2024, 7, 1) });
        ctx.Payments.Add(new Payment { EnrollmentId = currentEnrollment.Id, Amount = 2000m, PaymentMethod = "Cash", Status = "Approved", PaymentDate = new DateTime(2025, 7, 1) });

        await ctx.SaveChangesAsync();
        return (ctx, userId, parentId, student);
    }

    [Fact]
    public async Task MyPayments_DefaultsToActiveYear_AndListsAllOtherYearsWithBalances()
    {
        var (ctx, userId, _, _) = await SeedTwoYearStudentAsync();

        var result = await new GetMyPaymentsQueryHandler(ctx).Handle(new GetMyPaymentsQuery(userId), default);

        Assert.Equal(ActiveYear, result.SchoolYear);
        Assert.Equal(6000m, result.Balance.Balance); // 8000 - 2000

        var other = Assert.Single(result.OtherYears!);
        Assert.Equal(OldYear, other.SchoolYear);
        Assert.Equal(0m, other.Balance); // fully paid — still listed (contract: all years)
    }

    [Fact]
    public async Task MyPayments_SchoolYearParam_ShowsThatYear()
    {
        var (ctx, userId, _, _) = await SeedTwoYearStudentAsync();

        var result = await new GetMyPaymentsQueryHandler(ctx).Handle(new GetMyPaymentsQuery(userId, OldYear), default);

        Assert.Equal(OldYear, result.SchoolYear);
        Assert.Equal(0m, result.Balance.Balance);

        var other = Assert.Single(result.OtherYears!);
        Assert.Equal(ActiveYear, other.SchoolYear);
        Assert.Equal(6000m, other.Balance);
    }

    [Fact]
    public async Task MyLedger_SchoolYearParam_ShowsThatYearsLedger()
    {
        var (ctx, userId, _, _) = await SeedTwoYearStudentAsync();

        var oldLedger = await new GetMyLedgerQueryHandler(ctx).Handle(new GetMyLedgerQuery(userId, OldYear), default);
        Assert.Equal(0m, oldLedger.Balance);           // 5000 charged - 5000 paid
        Assert.Equal(5000m, oldLedger.TotalDebits);

        var defaultLedger = await new GetMyLedgerQueryHandler(ctx).Handle(new GetMyLedgerQuery(userId), default);
        Assert.Equal(6000m, defaultLedger.Balance);    // active year: 8000 - 2000
    }

    [Fact]
    public async Task ChildPayments_SchoolYearParam_ShowsThatYear()
    {
        var (ctx, _, parentId, student) = await SeedTwoYearStudentAsync();

        var oldView = await new GetChildPaymentsQueryHandler(ctx)
            .Handle(new GetChildPaymentsQuery(student.Id, parentId, OldYear), default);
        Assert.Equal(0m, oldView.Balance.Balance);

        var defaultView = await new GetChildPaymentsQueryHandler(ctx)
            .Handle(new GetChildPaymentsQuery(student.Id, parentId), default);
        Assert.Equal(6000m, defaultView.Balance.Balance);
    }

    [Fact]
    public async Task ChildPayments_CarriesSchoolYearAndOtherYears_LikeMyPayments()
    {
        // Mirror of MyPayments_DefaultsToActiveYear...: the parent twin must expose the
        // exact same multi-year fields the student twin does.
        var (ctx, _, parentId, student) = await SeedTwoYearStudentAsync();

        var result = await new GetChildPaymentsQueryHandler(ctx)
            .Handle(new GetChildPaymentsQuery(student.Id, parentId), default);

        Assert.Equal(ActiveYear, result.SchoolYear);
        Assert.Equal(6000m, result.Balance.Balance);

        var other = Assert.Single(result.OtherYears!);
        Assert.Equal(OldYear, other.SchoolYear);
        Assert.Equal(0m, other.Balance); // fully paid — still listed (contract: all years)
    }
}
