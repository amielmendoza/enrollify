using Enrollify.Application.Features.Reports;
using Enrollify.Application.Features.Reports.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Enrollment register: shared filter (year/grade/status/section), cancelled-excluded default,
/// full-range summary, batched balance math (incl. unassessed nulls and adjustment fold),
/// paging clamp, and a CSV export that shares the exact filter/projection.
/// </summary>
public class EnrollmentRegisterTests
{
    private static Student NewStudent(string lrn, string first, string last) =>
        new() { LRN = lrn, FirstName = first, LastName = last, Address = "Manila" };

    /// <summary>
    /// Active year 2025-2026 (Full-plan term: 5% discount):
    ///  A: Cruz, Grade 7, Enrolled, in section, assessed 10000 Full plan, paid 4000 (+pending 999 ignored),
    ///     adjustments +200 debit and a voided credit (ignored) => balance 9500 + 200 - 4000 = 5700
    ///  B: Reyes, Grade 7, Submitted, unassessed => assessedTotal/balance null
    ///  C: Santos, Grade 8, Approved, assessed 8000 no plan, paid 8000 => balance 0
    ///  D: Diaz, Grade 7, Cancelled (excluded unless includeCancelled)
    /// Prior year 2024-2025: E: Cruz, Grade 7, Enrolled (excluded by year scope).
    /// </summary>
    private static async Task<(ApplicationDbContext Ctx, Enrollment A, Enrollment B, Enrollment C, Enrollment D, Section Section)> SeedAsync()
    {
        var ctx = TestDb.Create();

        ctx.SchoolYears.AddRange(
            new SchoolYear { Name = "2024-2025", StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 3, 31), IsActive = false },
            new SchoolYear { Name = "2025-2026", StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });

        ctx.PaymentTerms.Add(new PaymentTerm
        {
            SchoolYear = "2025-2026", PlanType = "Full",
            DownPaymentPercent = 0, InterestRatePercent = 0, DiscountPercent = 5, InstallmentCount = 1
        });

        var sa = NewStudent("REG-0001", "Ana", "Cruz");
        var sb = NewStudent("REG-0002", "Ben", "Reyes");
        var sc = NewStudent("REG-0003", "Carla", "Santos");
        var sd = NewStudent("REG-0004", "Dan", "Diaz");
        ctx.Students.AddRange(sa, sb, sc, sd);

        var section = new Section { Name = "Einstein", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 };
        ctx.Sections.Add(section);

        var a = new Enrollment
        {
            StudentId = sa.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Enrolled, SectionId = section.Id,
            AssessedTotal = 10000m, AssessedAt = DateTime.UtcNow, PaymentPlan = "Full"
        };
        var b = new Enrollment
        {
            StudentId = sb.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Submitted
        };
        var c = new Enrollment
        {
            StudentId = sc.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 8",
            Status = EnrollmentStatus.Approved, AssessedTotal = 8000m, AssessedAt = DateTime.UtcNow
        };
        var d = new Enrollment
        {
            StudentId = sd.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Cancelled
        };
        var e = new Enrollment
        {
            StudentId = sa.Id, SchoolYear = "2024-2025", GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Enrolled
        };
        ctx.Enrollments.AddRange(a, b, c, d, e);

        ctx.Payments.AddRange(
            new Payment { EnrollmentId = a.Id, Amount = 4000m, PaymentMethod = "Cash", Status = "Approved" },
            new Payment { EnrollmentId = a.Id, Amount = 999m, PaymentMethod = "Cash", Status = "Pending" },
            new Payment { EnrollmentId = c.Id, Amount = 8000m, PaymentMethod = "Cash", Status = "Approved" });

        ctx.LedgerAdjustments.AddRange(
            new LedgerAdjustment { EnrollmentId = a.Id, Type = "Debit", Description = "Late penalty", Amount = 200m, PostedBy = "Registrar Maria" },
            new LedgerAdjustment { EnrollmentId = a.Id, Type = "Credit", Description = "Voided credit", Amount = 50m, PostedBy = "Registrar Maria", IsVoided = true });

        await ctx.SaveChangesAsync();
        return (ctx, a, b, c, d, section);
    }

    private static EnrollmentRegisterFilter Filter(
        string? schoolYear = null, string? gradeLevel = null, EnrollmentStatus? status = null,
        Guid? sectionId = null, bool includeCancelled = false) =>
        new(schoolYear, gradeLevel, status, sectionId, includeCancelled);

    [Fact]
    public async Task NullSchoolYear_ResolvesActiveYear_ExcludesCancelledAndOtherYears()
    {
        var (ctx, a, b, c, _, _) = await SeedAsync();

        var dto = await new GetEnrollmentRegisterQueryHandler(ctx).Handle(
            new GetEnrollmentRegisterQuery(Filter()), default);

        Assert.Equal(3, dto.TotalCount);
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, dto.Items.Select(i => i.EnrollmentId).ToArray()); // Grade 7 (Cruz, Reyes), then Grade 8
        Assert.DoesNotContain(dto.Items, i => i.Status == "Cancelled");
    }

    [Fact]
    public async Task Filters_Grade_Status_Section_AllNarrow()
    {
        var (ctx, a, b, _, _, section) = await SeedAsync();
        var handler = new GetEnrollmentRegisterQueryHandler(ctx);

        var byGrade = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(gradeLevel: "Grade 7")), default);
        Assert.Equal(new[] { a.Id, b.Id }, byGrade.Items.Select(i => i.EnrollmentId).ToArray());

        var byStatus = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(status: EnrollmentStatus.Enrolled)), default);
        Assert.Equal(a.Id, Assert.Single(byStatus.Items).EnrollmentId);

        var bySection = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(sectionId: section.Id)), default);
        Assert.Equal(a.Id, Assert.Single(bySection.Items).EnrollmentId);

        // Explicit year honored: only the single prior-year enrollment (Cruz, Enrolled) comes back.
        var byYear = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(schoolYear: "2024-2025")), default);
        var priorRow = Assert.Single(byYear.Items);
        Assert.Equal("Cruz, Ana", priorRow.StudentName);
        Assert.Equal("Enrolled", priorRow.Status);
    }

    [Fact]
    public async Task BalanceMath_FoldsDiscountAdjustmentsPayments_AndKeepsUnassessedNull()
    {
        var (ctx, a, b, c, _, _) = await SeedAsync();

        var dto = await new GetEnrollmentRegisterQueryHandler(ctx).Handle(
            new GetEnrollmentRegisterQuery(Filter()), default);

        var rowA = dto.Items.Single(i => i.EnrollmentId == a.Id);
        Assert.Equal(10000m, rowA.AssessedTotal);
        Assert.Equal(4000m, rowA.TotalPaid);           // pending payment ignored
        Assert.Equal(5700m, rowA.Balance);             // 9500 (5% discounted) + 200 adj - 4000 paid
        Assert.Equal("Einstein", rowA.SectionName);
        Assert.Equal("Cruz, Ana", rowA.StudentName);

        var rowB = dto.Items.Single(i => i.EnrollmentId == b.Id);
        Assert.Null(rowB.AssessedTotal);
        Assert.Null(rowB.Balance);                     // unassessed: null, not a fake zero
        Assert.Equal(0m, rowB.TotalPaid);
        Assert.Null(rowB.SectionName);

        var rowC = dto.Items.Single(i => i.EnrollmentId == c.Id);
        Assert.Equal(0m, rowC.Balance);                // fully paid, no plan
    }

    [Fact]
    public async Task IncludeCancelled_ShowsCancelledRows()
    {
        var (ctx, _, _, _, d, _) = await SeedAsync();

        var dto = await new GetEnrollmentRegisterQueryHandler(ctx).Handle(
            new GetEnrollmentRegisterQuery(Filter(includeCancelled: true)), default);

        Assert.Equal(4, dto.TotalCount);
        Assert.Equal("Cancelled", dto.Items.Single(i => i.EnrollmentId == d.Id).Status);
    }

    [Fact]
    public async Task Paging_Clamps_AndSummaryCoversFullRangeNotPage()
    {
        var (ctx, a, _, c, _, _) = await SeedAsync();
        var handler = new GetEnrollmentRegisterQueryHandler(ctx);

        var page1 = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(), Page: 1, PageSize: 2), default);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page1.Summary.TotalCount);     // summary over the FULL filtered range
        Assert.Equal(new[] { ("Submitted", 1), ("Approved", 1), ("Enrolled", 1) },
            page1.Summary.ByStatus.Select(s => (s.Status, s.Count)).ToArray());
        Assert.Equal(new[] { ("Grade 7", 2), ("Grade 8", 1) },
            page1.Summary.ByGrade.Select(g => (g.GradeLevel, g.Count)).ToArray());

        var page2 = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(), Page: 2, PageSize: 2), default);
        Assert.Equal(c.Id, Assert.Single(page2.Items).EnrollmentId); // deterministic order across pages

        // Clamps: page < 1 -> 1; pageSize < 1 -> 1 (observable: exactly one row comes back).
        var clamped = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(), Page: 0, PageSize: 0), default);
        Assert.Equal(a.Id, Assert.Single(clamped.Items).EnrollmentId);
        Assert.Equal(3, clamped.TotalCount);

        // pageSize far above the 500 cap must not throw and still returns everything here.
        var oversized = await handler.Handle(new GetEnrollmentRegisterQuery(Filter(), Page: 1, PageSize: 99999), default);
        Assert.Equal(3, oversized.Items.Count);
    }

    [Fact]
    public async Task CsvExport_SharesFilterProjectionAndOrder()
    {
        var (ctx, _, _, _, _, _) = await SeedAsync();

        var paged = await new GetEnrollmentRegisterQueryHandler(ctx).Handle(
            new GetEnrollmentRegisterQuery(Filter(), Page: 1, PageSize: 500), default);
        var exported = await new GetEnrollmentRegisterExportQueryHandler(ctx).Handle(
            new GetEnrollmentRegisterExportQuery(Filter()), default);

        // Same rows, same order as the paged query.
        Assert.Equal(paged.Items.Select(i => i.EnrollmentId), exported.Select(r => r.EnrollmentId));

        var csv = EnrollmentRegisterCsvBuilder.Build(exported);
        var lines = csv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal(EnrollmentRegisterCsvBuilder.Header, lines[0]);
        Assert.Equal(4, lines.Length); // header + 3 rows

        var lineA = lines.Single(l => l.StartsWith("REG-0001"));
        Assert.Contains("10000.00", lineA);
        Assert.Contains("4000.00", lineA);
        Assert.Contains("5700.00", lineA);
        Assert.Contains("\"Cruz, Ana\"", lineA);       // RFC 4180: comma-bearing name quoted

        // Unassessed row: assessed/balance are EMPTY fields, never "0.00".
        var lineB = lines.Single(l => l.StartsWith("REG-0002"));
        Assert.Contains("Submitted,,,0.00,,", lineB);   // plan,assessed empty; paid 0.00; balance empty
    }

    private static ApplicationDbContext WithActiveYear()
    {
        var ctx = TestDb.Create();
        ctx.SchoolYears.Add(new SchoolYear { Name = "2025-2026", StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = true });
        return ctx;
    }

    // The register is a signed document for a K-12 school: grades must read Kindergarten,
    // Grade 1 … Grade 12 — not text order (Grade 1, Grade 10, …, Grade 9, Kindergarten).
    [Fact]
    public async Task Rows_FollowTheCanonicalGradeOrder_UnknownGradesLast()
    {
        var ctx = WithActiveYear();
        foreach (var (grade, last) in new[] { ("Grade 10", "Alpha"), ("Nursery", "Bravo"), ("Grade 2", "Charlie"), ("Kindergarten", "Delta") })
        {
            var s = NewStudent($"ORD-{last}", "Kid", last);
            ctx.Students.Add(s);
            ctx.Enrollments.Add(new Enrollment { StudentId = s.Id, SchoolYear = "2025-2026", GradeLevel = grade, Status = EnrollmentStatus.Draft });
        }
        await ctx.SaveChangesAsync();

        var dto = await new GetEnrollmentRegisterQueryHandler(ctx).Handle(new GetEnrollmentRegisterQuery(Filter()), default);

        Assert.Equal(new[] { "Kindergarten", "Grade 2", "Grade 10", "Nursery" }, dto.Items.Select(i => i.GradeLevel).ToArray());
    }

    // Enrollments assessed before fee snapshots existed have no AssessedTotal; the balance
    // endpoint falls back to the live fee catalog, and the register must agree with it.
    [Fact]
    public async Task LegacyAssessedEnrollmentWithoutSnapshot_MatchesTheBalanceEndpoint()
    {
        var ctx = WithActiveYear();
        ctx.Fees.AddRange(
            new Fee { Name = "Tuition", Amount = 1000m, SchoolYear = "2025-2026", GradeLevel = "Grade 9", IsActive = true },
            new Fee { Name = "Misc", Amount = 500m, SchoolYear = "2025-2026", GradeLevel = "Grade 9", IsActive = true },
            new Fee { Name = "Retired", Amount = 999m, SchoolYear = "2025-2026", GradeLevel = "Grade 9", IsActive = false });
        var s = NewStudent("LEG-0001", "Lee", "Legacy");
        ctx.Students.Add(s);
        var e = new Enrollment { StudentId = s.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 9", Status = EnrollmentStatus.Approved };
        ctx.Enrollments.Add(e);
        ctx.Payments.Add(new Payment { EnrollmentId = e.Id, Amount = 300m, PaymentMethod = "Cash", Status = "Approved" });
        await ctx.SaveChangesAsync();

        var row = Assert.Single((await new GetEnrollmentRegisterQueryHandler(ctx)
            .Handle(new GetEnrollmentRegisterQuery(Filter()), default)).Items);
        var balance = await new Enrollify.Application.Features.Payments.Queries.GetBalanceQueryHandler(ctx)
            .Handle(new Enrollify.Application.Features.Payments.Queries.GetBalanceQuery(e.Id), default);

        Assert.Equal(1500m, row.AssessedTotal);
        Assert.Equal(1200m, row.Balance);
        Assert.Equal(balance.Balance, row.Balance);
    }

    // Applicant-typed names reach this CSV: a cell starting with = + - @ must not run as a
    // formula when opened in Excel/Sheets. Money stays numeric (negatives are legitimate).
    [Fact]
    public void Csv_NeutralizesFormulaLikeText_ButKeepsNegativeMoney()
    {
        var row = new EnrollmentRegisterRowDto(Guid.NewGuid(), Guid.NewGuid(), "=1+1", "@SUM(A1), Ana", "Grade 7", "+Section",
            "Draft", "-Plan", -50m, 0m, -125.5m, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var line = EnrollmentRegisterCsvBuilder.Build(new[] { row }).TrimEnd().Split(Environment.NewLine)[1];

        Assert.StartsWith("'=1+1,,\"'@SUM(A1), Ana\",Grade 7,'+Section,Draft,'-Plan,-50.00,0.00,-125.50,", line);
    }

    // Created dates are stored in UTC; the register prints the school's (Manila) calendar day.
    [Fact]
    public void Csv_CreatedDate_IsTheManilaCalendarDay()
    {
        var row = new EnrollmentRegisterRowDto(Guid.NewGuid(), Guid.NewGuid(), null, "Cruz, Ana", "Grade 7", null,
            "Draft", null, null, 0m, null, new DateTime(2026, 6, 1, 17, 0, 0, DateTimeKind.Utc)); // 01:00 on Jun 2 in Manila

        var line = EnrollmentRegisterCsvBuilder.Build(new[] { row }).TrimEnd().Split(Environment.NewLine)[1];

        Assert.EndsWith(",2026-06-02", line);
    }

    [Fact]
    public void Csv_IncludesStudentNumber_AndRendersMissingLrnAsEmpty()
    {
        var row = new EnrollmentRegisterRowDto(Guid.NewGuid(), Guid.NewGuid(), null, "Cruz, Ana", "Grade 7", null,
            "Draft", null, null, 0m, null, new DateTime(2026, 6, 1), StudentNumber: "2026-00042");

        var line = EnrollmentRegisterCsvBuilder.Build(new[] { row }).TrimEnd().Split(Environment.NewLine)[1];

        Assert.StartsWith(",2026-00042,\"Cruz, Ana\",", line);
    }
}
