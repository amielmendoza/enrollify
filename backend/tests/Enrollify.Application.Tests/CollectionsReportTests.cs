using Enrollify.Application.Features.Reports;
using Enrollify.Application.Features.Reports.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class CollectionsReportTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedEnrollmentAsync()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0600", FirstName = "Juan", MiddleName = "A.", LastName = "Dela Cruz", Address = "Manila" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Approved,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    private static Payment Pay(Guid enrollmentId, decimal amount, string method, DateTime date,
        string status = "Approved", string? reference = null, string? reviewedBy = null) => new()
    {
        EnrollmentId = enrollmentId,
        Amount = amount,
        PaymentMethod = method,
        PaymentDate = date,
        Status = status,
        ReferenceNumber = reference,
        ReviewedBy = reviewedBy
    };

    [Fact]
    public async Task DateRange_IsInclusive_OnBothEnds()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        ctx.Payments.Add(Pay(e.Id, 100m, "Cash", new DateTime(2026, 7, 31, 23, 0, 0, DateTimeKind.Utc)));  // day before → out
        ctx.Payments.Add(Pay(e.Id, 200m, "Cash", new DateTime(2026, 8, 1, 0, 30, 0, DateTimeKind.Utc)));   // from-date early morning → in
        ctx.Payments.Add(Pay(e.Id, 300m, "Cash", new DateTime(2026, 8, 5, 23, 59, 0, DateTimeKind.Utc)));  // to-date late night → in
        ctx.Payments.Add(Pay(e.Id, 400m, "Cash", new DateTime(2026, 8, 6, 0, 1, 0, DateTimeKind.Utc)));    // day after → out
        await ctx.SaveChangesAsync();

        // From carries a mid-day time component to prove both bounds normalize to whole days.
        var result = await new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 1, 15, 0, 0), new DateTime(2026, 8, 5), null), default);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { 200m, 300m }, result.Rows.Select(r => r.Amount).ToArray());
        Assert.Equal(500m, result.Summary.TotalAmount);
    }

    [Fact]
    public async Task OnlyApprovedPayments_AreIncluded()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        var day = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        ctx.Payments.Add(Pay(e.Id, 100m, "Cash", day));
        ctx.Payments.Add(Pay(e.Id, 200m, "Cash", day, status: "Pending"));
        ctx.Payments.Add(Pay(e.Id, 300m, "Cash", day, status: "Rejected"));
        await ctx.SaveChangesAsync();

        var result = await new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), null), default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(100m, result.Summary.TotalAmount);
    }

    [Fact]
    public async Task MethodFilter_MatchesExactly()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        var day = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        ctx.Payments.Add(Pay(e.Id, 100m, "Cash", day));
        ctx.Payments.Add(Pay(e.Id, 200m, "GCash", day));
        await ctx.SaveChangesAsync();

        var result = await new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), "Cash"), default);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Cash", row.PaymentMethod);
        Assert.Equal(100m, result.Summary.TotalAmount);
    }

    [Fact]
    public async Task Summary_MatchesRows_WithOrderedBreakdowns()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        ctx.Payments.Add(Pay(e.Id, 500m, "Cash", new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc), reviewedBy: "Registrar Rita", reference: "OR-1"));
        ctx.Payments.Add(Pay(e.Id, 2000m, "GCash", new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));
        ctx.Payments.Add(Pay(e.Id, 300m, "Cash", new DateTime(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var result = await new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), null), default);

        // Rows in journal (PaymentDate ascending) order.
        Assert.Equal(new[] { 2000m, 300m, 500m }, result.Rows.Select(r => r.Amount).ToArray());
        Assert.Equal("Dela Cruz, Juan A.", result.Rows[0].StudentName);
        Assert.Equal("Grade 7", result.Rows[0].GradeLevel);
        Assert.Equal("2025-2026", result.Rows[0].SchoolYear);
        Assert.Equal("Registrar Rita", result.Rows[2].ReceivedBy);

        Assert.Equal(2800m, result.Summary.TotalAmount);
        Assert.Equal(3, result.Summary.TotalCount);

        // byMethod ordered by amount descending.
        Assert.Equal(new[] { "GCash", "Cash" }, result.Summary.ByMethod.Select(m => m.Method).ToArray());
        Assert.Equal(2000m, result.Summary.ByMethod[0].Amount);
        Assert.Equal(800m, result.Summary.ByMethod[1].Amount);
        Assert.Equal(2, result.Summary.ByMethod[1].Count);

        // byDay ordered by date ascending, date-only keys.
        Assert.Equal(2, result.Summary.ByDay.Count);
        Assert.Equal(new DateTime(2026, 8, 1), result.Summary.ByDay[0].Date);
        Assert.Equal(2300m, result.Summary.ByDay[0].Amount);
        Assert.Equal(2, result.Summary.ByDay[0].Count);
        Assert.Equal(new DateTime(2026, 8, 2), result.Summary.ByDay[1].Date);
        Assert.Equal(500m, result.Summary.ByDay[1].Amount);
    }

    [Fact]
    public async Task Paging_SlicesRows_ButSummaryCoversFullSet()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        for (var i = 1; i <= 5; i++)
            ctx.Payments.Add(Pay(e.Id, i * 100m, "Cash", new DateTime(2026, 8, i, 9, 0, 0, DateTimeKind.Utc)));
        await ctx.SaveChangesAsync();

        var result = await new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), null, Page: 2, PageSize: 2), default);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(new[] { 300m, 400m }, result.Rows.Select(r => r.Amount).ToArray());

        // Summary ignores paging.
        Assert.Equal(1500m, result.Summary.TotalAmount);
        Assert.Equal(5, result.Summary.TotalCount);
    }

    [Fact]
    public async Task FromAfterTo_Throws()
    {
        var (ctx, _) = await SeedEnrollmentAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new GetCollectionsQueryHandler(ctx).Handle(
            new GetCollectionsQuery(new DateTime(2026, 8, 10), new DateTime(2026, 8, 1), null), default));
    }

    [Fact]
    public async Task ExportQuery_ReturnsAllRowsUnpaged_InJournalOrder()
    {
        var (ctx, e) = await SeedEnrollmentAsync();
        for (var i = 1; i <= 60; i++)
            ctx.Payments.Add(Pay(e.Id, 100m, "Cash", new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(i)));
        await ctx.SaveChangesAsync();

        var rows = await new GetCollectionsExportQueryHandler(ctx).Handle(
            new GetCollectionsExportQuery(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31), null), default);

        Assert.Equal(60, rows.Count);
        Assert.Equal(rows.OrderBy(r => r.PaymentDate).Select(r => r.PaymentId), rows.Select(r => r.PaymentId));
    }

    [Fact]
    public void Csv_EscapesCommasAndQuotes()
    {
        var rows = new List<CollectionRowDto>
        {
            new(Guid.NewGuid(), new DateTime(2026, 8, 1), "Dela Cruz, Juan A.", "Grade 7", "2025-2026",
                1234.5m, "Cash", "OR\"7", "Registrar Rita", Guid.NewGuid()),
            new(Guid.NewGuid(), new DateTime(2026, 8, 2), "Santos Maria", "Grade 8", "2025-2026",
                500m, "GCash", null, null, Guid.NewGuid()),
        };

        var csv = CollectionsCsvBuilder.Build(rows);
        var lines = csv.TrimEnd('\r', '\n').Split(Environment.NewLine);

        Assert.Equal("Date,Reference No,Student,Grade Level,School Year,Method,Received By,Amount", lines[0]);
        // Comma in the student name → quoted; embedded quote in the reference → doubled and quoted.
        Assert.Equal("2026-08-01,\"OR\"\"7\",\"Dela Cruz, Juan A.\",Grade 7,2025-2026,Cash,Registrar Rita,1234.50", lines[1]);
        // Nulls render as empty fields; plain fields stay unquoted.
        Assert.Equal("2026-08-02,,Santos Maria,Grade 8,2025-2026,GCash,,500.00", lines[2]);
    }
}
