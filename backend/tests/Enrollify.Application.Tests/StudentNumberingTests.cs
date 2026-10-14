using Enrollify.Application.Features.Students;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Interfaces;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class StudentNumberingTests
{
    private static readonly DateTime Mid2026Utc = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Student StudentWith(string number) => new()
    {
        StudentNumber = number, FirstName = "Test", LastName = "Student", Address = "Manila"
    };

    [Fact]
    public async Task Next_StartsAt00001_ForTheYear()
    {
        var ctx = TestDb.Create();
        Assert.Equal("2026-00001", await StudentNumbering.NextStudentNumberAsync(ctx, Mid2026Utc, default));
    }

    [Fact]
    public async Task Next_IncrementsPastMax_IgnoringOtherYears()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(StudentWith("2026-00002"));
        ctx.Students.Add(StudentWith("2026-00007"));
        ctx.Students.Add(StudentWith("2025-00099"));
        await ctx.SaveChangesAsync();

        Assert.Equal("2026-00008", await StudentNumbering.NextStudentNumberAsync(ctx, Mid2026Utc, default));
    }

    [Fact]
    public void ManilaYear_RollsOverEightHoursBeforeUtc()
    {
        // 2025-12-31 16:30 UTC is already 2026-01-01 00:30 in Manila.
        Assert.Equal(2026, StudentNumbering.ManilaYear(new DateTime(2025, 12, 31, 16, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(2025, StudentNumbering.ManilaYear(new DateTime(2025, 12, 31, 15, 59, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task CreateStudent_BlankLrn_StaysNull_AndGetsStudentNumber()
    {
        var ctx = TestDb.Create();
        var dto = await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "Manila", null, null, null, null), default);

        Assert.Null(dto.LRN);
        var year = StudentNumbering.ManilaYear(DateTime.UtcNow);
        Assert.Equal($"{year}-00001", (await ctx.Students.SingleAsync()).StudentNumber);
    }

    [Fact]
    public async Task CreateStudent_KeepsProvidedLrn()
    {
        var ctx = TestDb.Create();
        var dto = await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            " 136512260001 ", "Ben", "", "Reyes", new DateTime(2013, 2, 2), null, "Manila", null, null, null, null), default);

        Assert.Equal("136512260001", dto.LRN);
    }

    [Fact]
    public void CreateStudentValidator_RejectsNonTwelveDigitLrn()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            "100100", "Ben", "", "Reyes", new DateTime(2013, 2, 2), null, "Manila", null, null, null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "LRN");
    }

    /// <summary>
    /// A rival request lands the same freshly-minted student number between our generation
    /// and save; the save fails the way the (TenantId, StudentNumber) unique index would.
    /// </summary>
    private class RacingContext : ApplicationDbContext
    {
        private readonly string _dbName;
        private bool _raced;

        public RacingContext(string dbName)
            : base(TestDb.Options(dbName), new FixedTenantProvider(TestDb.TenantId)) => _dbName = dbName;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_raced)
            {
                _raced = true;
                using var rival = TestDb.Create(_dbName);
                rival.Students.Add(StudentWith($"{StudentNumbering.ManilaYear(DateTime.UtcNow)}-00001"));
                await rival.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated unique index violation on (TenantId, StudentNumber).");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task CreateStudent_RetriesWithFreshNumber_OnUniqueCollision()
    {
        var dbName = $"sn-race-{Guid.NewGuid()}";
        using var ctx = new RacingContext(dbName);

        await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            null, "Cara", "", "Reyes", new DateTime(2012, 5, 5), null, "Manila", null, null, null, null), default);

        using var verify = TestDb.Create(dbName);
        var numbers = await verify.Students.Select(s => s.StudentNumber).OrderBy(n => n).ToListAsync();
        var year = StudentNumbering.ManilaYear(DateTime.UtcNow);
        Assert.Equal(new[] { $"{year}-00001", $"{year}-00002" }, numbers);
    }
}
