using Enrollify.Application.Features.SchoolYears.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class CreateSchoolYearProvisioningTests
{
    private const string NewYear = "2026-2027";
    private const string SourceYear = "2025-2026";

    private static Task<Application.DTOs.SchoolYears.SchoolYearDto> CreateAsync(ApplicationDbContext ctx, string? copyFrom = null,
        bool includeFees = true, bool includeSections = true) =>
        new CreateSchoolYearCommandHandler(ctx)
            .Handle(new CreateSchoolYearCommand(NewYear, new DateTime(2026, 6, 1), new DateTime(2027, 3, 31),
                copyFrom, includeFees, includeSections), default);

    private static PaymentTerm Term(string year, string plan, decimal down = 0, decimal interest = 0, decimal discount = 0, int count = 1) => new()
    {
        SchoolYear = year, PlanType = plan,
        DownPaymentPercent = down, InterestRatePercent = interest, DiscountPercent = discount,
        InstallmentCount = count, IsActive = true
    };

    [Fact]
    public async Task Create_NoHistory_EnsuresStandardTermTriple()
    {
        var ctx = TestDb.Create();

        var dto = await CreateAsync(ctx);

        Assert.Equal(NewYear, dto.Name);
        var terms = await ctx.PaymentTerms.Where(t => t.SchoolYear == NewYear).ToListAsync();
        Assert.Equal(3, terms.Count);
        Assert.Equal(5m, terms.Single(t => t.PlanType == "Full").DiscountPercent);
        var monthly = terms.Single(t => t.PlanType == "Monthly");
        Assert.Equal(20m, monthly.DownPaymentPercent);
        Assert.Equal(5m, monthly.InterestRatePercent);
        Assert.Equal(9, monthly.InstallmentCount);
        var quarterly = terms.Single(t => t.PlanType == "Quarterly");
        Assert.Equal(30m, quarterly.DownPaymentPercent);
        Assert.Equal(3, quarterly.InstallmentCount);
    }

    [Fact]
    public async Task Create_WithCopyFrom_CopiesTermsFeesAndSections_SkippingDuplicates()
    {
        var ctx = TestDb.Create();
        // Source terms: customized Monthly + Full; Quarterly missing → default fills the gap.
        ctx.PaymentTerms.Add(Term(SourceYear, "Monthly", down: 35m, interest: 4m, count: 10));
        ctx.PaymentTerms.Add(Term(SourceYear, "Full", discount: 8m));
        // Source fees: two active G7, one inactive (skipped), one active G8 (all grades copied).
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 8000m, SchoolYear = SourceYear, GradeLevel = "Grade 7", IsActive = true });
        ctx.Fees.Add(new Fee { Name = "Miscellaneous", Amount = 2000m, SchoolYear = SourceYear, GradeLevel = "Grade 7", IsActive = true });
        ctx.Fees.Add(new Fee { Name = "Old Fee", Amount = 500m, SchoolYear = SourceYear, GradeLevel = "Grade 7", IsActive = false });
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 9000m, SchoolYear = SourceYear, GradeLevel = "Grade 8", IsActive = true });
        // Pre-existing fee in the NEW year with the same (name, grade) → duplicate skipped.
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 9999m, SchoolYear = NewYear, GradeLevel = "Grade 7", IsActive = true });
        // Source sections: one active, one inactive.
        var sourceSection = new Section { Name = "Rose", GradeLevel = "Grade 7", SchoolYear = SourceYear, Capacity = 40, Adviser = "Ms. Cruz", IsActive = true };
        ctx.Sections.Add(sourceSection);
        ctx.Sections.Add(new Section { Name = "Closed", GradeLevel = "Grade 7", SchoolYear = SourceYear, Capacity = 30, IsActive = false });
        await ctx.SaveChangesAsync();

        await CreateAsync(ctx, copyFrom: SourceYear);

        var terms = await ctx.PaymentTerms.Where(t => t.SchoolYear == NewYear).ToListAsync();
        Assert.Equal(3, terms.Count);
        Assert.Equal(35m, terms.Single(t => t.PlanType == "Monthly").DownPaymentPercent);
        Assert.Equal(10, terms.Single(t => t.PlanType == "Monthly").InstallmentCount);
        Assert.Equal(8m, terms.Single(t => t.PlanType == "Full").DiscountPercent);
        Assert.Equal(30m, terms.Single(t => t.PlanType == "Quarterly").DownPaymentPercent); // default fallback

        var fees = await ctx.Fees.Where(f => f.SchoolYear == NewYear).ToListAsync();
        Assert.Equal(3, fees.Count); // pre-existing Tuition/G7 + copied Misc/G7 + Tuition/G8
        Assert.Equal(9999m, fees.Single(f => f.Name == "Tuition" && f.GradeLevel == "Grade 7").Amount); // dupe kept, not overwritten
        Assert.Equal(2000m, fees.Single(f => f.Name == "Miscellaneous").Amount);
        Assert.DoesNotContain(fees, f => f.Name == "Old Fee");

        var sections = await ctx.Sections.Where(s => s.SchoolYear == NewYear).ToListAsync();
        var copied = Assert.Single(sections);
        Assert.Equal("Rose", copied.Name);
        Assert.Equal(40, copied.Capacity);
        Assert.Equal("Ms. Cruz", copied.Adviser);
        Assert.NotEqual(sourceSection.Id, copied.Id); // fresh row
    }

    [Fact]
    public async Task Create_CopyFromWithoutTerms_FallsBackToMostRecentYearWithTerms()
    {
        var ctx = TestDb.Create();
        ctx.PaymentTerms.Add(Term("2023-2024", "Monthly", down: 25m, count: 8));
        await ctx.SaveChangesAsync();

        // CopyFrom names a year that has no terms → most recent year with terms wins.
        await CreateAsync(ctx, copyFrom: SourceYear);

        var monthly = await ctx.PaymentTerms.SingleAsync(t => t.SchoolYear == NewYear && t.PlanType == "Monthly");
        Assert.Equal(25m, monthly.DownPaymentPercent);
        Assert.Equal(8, monthly.InstallmentCount);
    }

    [Fact]
    public async Task Create_WithoutCopyFrom_EnsuresTermsButCopiesNothing()
    {
        var ctx = TestDb.Create();
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 8000m, SchoolYear = SourceYear, GradeLevel = "Grade 7", IsActive = true });
        ctx.Sections.Add(new Section { Name = "Rose", GradeLevel = "Grade 7", SchoolYear = SourceYear, Capacity = 40, IsActive = true });
        await ctx.SaveChangesAsync();

        await CreateAsync(ctx, copyFrom: null);

        Assert.Equal(3, await ctx.PaymentTerms.CountAsync(t => t.SchoolYear == NewYear));
        Assert.Equal(0, await ctx.Fees.CountAsync(f => f.SchoolYear == NewYear));
        Assert.Equal(0, await ctx.Sections.CountAsync(s => s.SchoolYear == NewYear));
    }

    [Fact]
    public async Task Create_CopyFrom_RespectsIncludeFlags()
    {
        var ctx = TestDb.Create();
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 8000m, SchoolYear = SourceYear, GradeLevel = "Grade 7", IsActive = true });
        ctx.Sections.Add(new Section { Name = "Rose", GradeLevel = "Grade 7", SchoolYear = SourceYear, Capacity = 40, IsActive = true });
        await ctx.SaveChangesAsync();

        await CreateAsync(ctx, copyFrom: SourceYear, includeFees: false, includeSections: true);

        Assert.Equal(0, await ctx.Fees.CountAsync(f => f.SchoolYear == NewYear));
        Assert.Equal(1, await ctx.Sections.CountAsync(s => s.SchoolYear == NewYear));
    }
}
