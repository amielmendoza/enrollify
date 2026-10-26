using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class WithdrawalTests
{
    private const string Year = "2025-2026";

    // School year started 60 days ago (Manila), so there is room on both sides of "today".
    private static DateTime Today => DepEdRules.ManilaDate(DateTime.UtcNow);
    private static DateTime YearStart => Today.AddDays(-60);

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status = EnrollmentStatus.Enrolled, DateTime? firstAttendance = null)
    {
        var ctx = TestDb.Create();
        ctx.SchoolYears.Add(new SchoolYear { Name = Year, StartDate = YearStart, EndDate = YearStart.AddDays(300), IsActive = true });
        var student = new Student { LRN = "LRN-1801", FirstName = "Wita", LastName = "Draw", Address = "Cebu" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = Year,
            GradeLevel = "Grade 7",
            Status = status,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow,
            FirstAttendanceDate = firstAttendance
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = 4000m, PaymentMethod = "Cash", Status = "Approved" });
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    private static MarkEnrollmentWithdrawnCommand Dropped(Guid id, DateTime date, string? reason = "Family moved abroad") =>
        new(id, "Dropped", date, reason, null, "Registrar Rita");

    private static MarkEnrollmentWithdrawnCommand TransferredOut(Guid id, DateTime date, string? school = "Sample National HS", string? reason = null) =>
        new(id, "TransferredOut", date, reason, school, "Registrar Rita");

    private static Task<EnrollmentDto> Mark(ApplicationDbContext ctx, MarkEnrollmentWithdrawnCommand cmd) =>
        new MarkEnrollmentWithdrawnCommandHandler(ctx).Handle(cmd, default);

    private static Task<EnrollmentDto> Undo(ApplicationDbContext ctx, Guid id) =>
        new UndoEnrollmentWithdrawalCommandHandler(ctx).Handle(new UndoEnrollmentWithdrawalCommand(id, "Admin Ana"), default);

    // ----- status guard -----

    [Theory]
    [InlineData(EnrollmentStatus.Approved)]
    [InlineData(EnrollmentStatus.Paid)]
    [InlineData(EnrollmentStatus.Enrolled)]
    public async Task Mark_AllowedStatuses_Succeed(EnrollmentStatus status)
    {
        var (ctx, e) = await SeedAsync(status);

        var dto = await Mark(ctx, Dropped(e.Id, Today));

        Assert.Equal("Dropped", dto.WithdrawalType);
        Assert.Equal(status, dto.Status);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft)]
    [InlineData(EnrollmentStatus.Submitted)]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Cancelled)]
    public async Task Mark_OtherStatuses_Rejected(EnrollmentStatus status)
    {
        var (ctx, e) = await SeedAsync(status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Mark(ctx, Dropped(e.Id, Today)));

        Assert.Null(e.WithdrawalType);
        Assert.Empty(await ctx.EnrollmentStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task Mark_UnknownEnrollment_NotFound()
    {
        var (ctx, _) = await SeedAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Mark(ctx, Dropped(Guid.NewGuid(), Today)));
    }

    [Fact]
    public async Task Mark_AlreadyWithdrawn_Rejected()
    {
        var (ctx, e) = await SeedAsync();
        await Mark(ctx, Dropped(e.Id, Today.AddDays(-5)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Mark(ctx, TransferredOut(e.Id, Today)));

        Assert.Equal("Dropped", e.WithdrawalType); // original mark untouched
        Assert.Equal(Today.AddDays(-5), e.WithdrawalDate);
        Assert.Equal(1, await ctx.EnrollmentStatusHistories.CountAsync());
    }

    // ----- date bounds -----

    [Fact]
    public async Task Mark_DateBeforeSchoolYearStart_Rejected_WhenNoFirstAttendance()
    {
        var (ctx, e) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Mark(ctx, Dropped(e.Id, YearStart.AddDays(-1))));
        Assert.Null(e.WithdrawalType);
    }

    [Fact]
    public async Task Mark_DateBeforeFirstAttendance_Rejected()
    {
        var firstAttendance = Today.AddDays(-20);
        var (ctx, e) = await SeedAsync(firstAttendance: firstAttendance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Mark(ctx, Dropped(e.Id, firstAttendance.AddDays(-1))));
        Assert.Null(e.WithdrawalType);
    }

    [Fact]
    public async Task Mark_DateEqualToFirstAttendance_Allowed()
    {
        var firstAttendance = Today.AddDays(-20);
        var (ctx, e) = await SeedAsync(firstAttendance: firstAttendance);

        var dto = await Mark(ctx, Dropped(e.Id, firstAttendance));

        Assert.Equal(firstAttendance, dto.WithdrawalDate);
    }

    [Fact]
    public async Task Mark_DateAfterManilaToday_Rejected()
    {
        var (ctx, e) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Mark(ctx, Dropped(e.Id, Today.AddDays(1))));
        Assert.Null(e.WithdrawalType);
    }

    [Fact]
    public async Task Mark_DateEqualToManilaToday_Allowed()
    {
        var (ctx, e) = await SeedAsync();

        var dto = await Mark(ctx, Dropped(e.Id, Today));

        Assert.Equal(Today, dto.WithdrawalDate);
    }

    // ----- marking: fields, history, status/balance untouched -----

    [Fact]
    public async Task Mark_Dropped_SetsFields_WritesHistory_LeavesStatusAndPaymentsAlone()
    {
        var (ctx, e) = await SeedAsync(EnrollmentStatus.Paid);
        var date = Today.AddDays(-3);

        var dto = await Mark(ctx, Dropped(e.Id, date, "  Family moved abroad "));

        Assert.Equal("Dropped", e.WithdrawalType);
        Assert.Equal(date, e.WithdrawalDate);
        Assert.Equal("Family moved abroad", e.WithdrawalReason);
        Assert.Null(e.TransferredToSchool);
        Assert.Equal("Dropped", dto.WithdrawalType);
        Assert.Equal("Family moved abroad", dto.WithdrawalReason);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == e.Id);
        Assert.Equal(EnrollmentStatus.Paid, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Paid, history.ToStatus);
        Assert.Equal($"Marked dropped as of {date:MM/dd/yyyy}: Family moved abroad", history.Remarks);
        Assert.Equal("Registrar Rita", history.CreatedBy);

        Assert.Equal(EnrollmentStatus.Paid, e.Status);
        Assert.Equal(10000m, e.AssessedTotal);
        var payment = await ctx.Payments.SingleAsync(p => p.EnrollmentId == e.Id);
        Assert.Equal(4000m, payment.Amount);
        Assert.Equal("Approved", payment.Status);
    }

    [Fact]
    public async Task Mark_TransferredOut_SetsFields_WritesHistory()
    {
        var (ctx, e) = await SeedAsync(EnrollmentStatus.Approved);
        var date = Today.AddDays(-7);

        await Mark(ctx, TransferredOut(e.Id, date, "Sample National HS"));

        Assert.Equal("TransferredOut", e.WithdrawalType);
        Assert.Equal(date, e.WithdrawalDate);
        Assert.Equal("Sample National HS", e.TransferredToSchool);
        Assert.Null(e.WithdrawalReason);
        Assert.Equal(EnrollmentStatus.Approved, e.Status);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == e.Id);
        Assert.Equal(EnrollmentStatus.Approved, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Approved, history.ToStatus);
        Assert.Equal($"Marked transferred out as of {date:MM/dd/yyyy} to Sample National HS", history.Remarks);
        Assert.Equal("Registrar Rita", history.CreatedBy);
    }

    [Fact]
    public async Task Mark_TransferredOut_KeepsOptionalReason()
    {
        var (ctx, e) = await SeedAsync();

        await Mark(ctx, TransferredOut(e.Id, Today, "Sample National HS", "Relocation"));

        Assert.Equal("Relocation", e.WithdrawalReason);
    }

    // ----- validator (static shape) -----

    private static bool IsValid(MarkEnrollmentWithdrawnCommand cmd) =>
        new MarkEnrollmentWithdrawnCommandValidator().Validate(cmd).IsValid;

    [Fact]
    public void Validator_AcceptsWellFormedCommands()
    {
        Assert.True(IsValid(Dropped(Guid.NewGuid(), Today)));
        Assert.True(IsValid(TransferredOut(Guid.NewGuid(), Today)));
        Assert.True(IsValid(TransferredOut(Guid.NewGuid(), Today, "Sample National HS", "Relocation")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("dropped")]
    [InlineData("Transferred Out")]
    [InlineData("Graduated")]
    public void Validator_RejectsUnknownType(string type) =>
        Assert.False(IsValid(new MarkEnrollmentWithdrawnCommand(Guid.NewGuid(), type, Today, "reason", null, "x")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_Dropped_RequiresReason(string? reason) =>
        Assert.False(IsValid(Dropped(Guid.NewGuid(), Today, reason)));

    [Fact]
    public void Validator_Dropped_ReasonMax500()
    {
        Assert.True(IsValid(Dropped(Guid.NewGuid(), Today, new string('r', 500))));
        Assert.False(IsValid(Dropped(Guid.NewGuid(), Today, new string('r', 501))));
    }

    [Fact]
    public void Validator_Dropped_RejectsDestination() =>
        Assert.False(IsValid(new MarkEnrollmentWithdrawnCommand(Guid.NewGuid(), "Dropped", Today, "reason", "Some School", "x")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_TransferredOut_RequiresDestination(string? school) =>
        Assert.False(IsValid(TransferredOut(Guid.NewGuid(), Today, school)));

    [Fact]
    public void Validator_TransferredOut_DestinationMax200_ReasonOptionalMax500()
    {
        Assert.True(IsValid(TransferredOut(Guid.NewGuid(), Today, new string('s', 200))));
        Assert.False(IsValid(TransferredOut(Guid.NewGuid(), Today, new string('s', 201))));
        Assert.True(IsValid(TransferredOut(Guid.NewGuid(), Today, "Sample National HS", null)));
        Assert.False(IsValid(TransferredOut(Guid.NewGuid(), Today, "Sample National HS", new string('r', 501))));
    }

    // ----- undo -----

    [Fact]
    public async Task Undo_NotWithdrawn_Rejected()
    {
        var (ctx, e) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Undo(ctx, e.Id));

        Assert.Empty(await ctx.EnrollmentStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task Undo_UnknownEnrollment_NotFound()
    {
        var (ctx, _) = await SeedAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Undo(ctx, Guid.NewGuid()));
    }

    [Fact]
    public async Task Undo_Dropped_ClearsAllFields_WritesHistory()
    {
        var (ctx, e) = await SeedAsync(EnrollmentStatus.Enrolled);
        var date = Today.AddDays(-4);
        await Mark(ctx, Dropped(e.Id, date));

        var dto = await Undo(ctx, e.Id);

        Assert.Null(e.WithdrawalType);
        Assert.Null(e.WithdrawalDate);
        Assert.Null(e.WithdrawalReason);
        Assert.Null(e.TransferredToSchool);
        Assert.Null(dto.WithdrawalType);
        Assert.Equal(EnrollmentStatus.Enrolled, dto.Status);

        var undo = (await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == e.Id).ToListAsync())
            .Single(h => h.CreatedBy == "Admin Ana");
        Assert.Equal(EnrollmentStatus.Enrolled, undo.FromStatus);
        Assert.Equal(EnrollmentStatus.Enrolled, undo.ToStatus);
        Assert.Equal($"Withdrawal mark removed (was dropped as of {date:MM/dd/yyyy})", undo.Remarks);
    }

    [Fact]
    public async Task Undo_TransferredOut_ClearsAllFields_WritesHistory()
    {
        var (ctx, e) = await SeedAsync(EnrollmentStatus.Paid);
        var date = Today.AddDays(-9);
        await Mark(ctx, TransferredOut(e.Id, date, "Sample National HS", "Relocation"));

        await Undo(ctx, e.Id);

        Assert.Null(e.WithdrawalType);
        Assert.Null(e.WithdrawalDate);
        Assert.Null(e.WithdrawalReason);
        Assert.Null(e.TransferredToSchool);

        var undo = (await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == e.Id).ToListAsync())
            .Single(h => h.CreatedBy == "Admin Ana");
        Assert.Equal($"Withdrawal mark removed (was transferred out as of {date:MM/dd/yyyy})", undo.Remarks);
    }

    [Fact]
    public async Task Undo_ThenMarkAgain_Works()
    {
        var (ctx, e) = await SeedAsync();
        await Mark(ctx, Dropped(e.Id, Today.AddDays(-2)));
        await Undo(ctx, e.Id);

        var dto = await Mark(ctx, TransferredOut(e.Id, Today));

        Assert.Equal("TransferredOut", dto.WithdrawalType);
    }
}
