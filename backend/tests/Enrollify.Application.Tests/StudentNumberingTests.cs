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
    private static Student StudentWith(string lrn) => new()
    {
        LRN = lrn,
        FirstName = "Test",
        LastName = "Student",
        Address = "Manila"
    };

    [Fact]
    public async Task NextLrn_StartsAtSeed_WhenNoStudentsExist()
    {
        var ctx = TestDb.Create();

        Assert.Equal("100100100001", await StudentNumbering.NextLrnAsync(ctx, default));
    }

    [Fact]
    public async Task NextLrn_IncrementsPastMax_IgnoringNonNumericLrns()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(StudentWith("100100100002"));
        ctx.Students.Add(StudentWith("100100100007"));
        ctx.Students.Add(StudentWith("LRN-MANUAL-1")); // 12 chars but not numeric — ignored
        ctx.Students.Add(StudentWith("55"));           // numeric but not the generated shape — ignored
        await ctx.SaveChangesAsync();

        Assert.Equal("100100100008", await StudentNumbering.NextLrnAsync(ctx, default));
    }

    [Fact]
    public async Task CreateStudent_GeneratesSequentialLrns()
    {
        var ctx = TestDb.Create();
        var handler = new CreateStudentCommandHandler(ctx);

        var first = await handler.Handle(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "Manila", null, null, null, null), default);
        var second = await handler.Handle(new CreateStudentCommand(
            null, "Ben", "", "Reyes", new DateTime(2013, 2, 2), null, "Manila", null, null, null, null), default);

        Assert.Equal("100100100001", first.LRN);
        Assert.Equal("100100100002", second.LRN);
    }

    /// <summary>
    /// Simulates the concurrency race: a rival request lands the same freshly-minted LRN
    /// between our generation and save, and the save fails the way the (TenantId, LRN)
    /// unique index would. The handler must regenerate and retry.
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
                rival.Students.Add(StudentWith("100100100001"));
                await rival.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated unique index violation on (TenantId, LRN).");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task CreateStudent_RetriesWithFreshLrn_OnUniqueCollision()
    {
        var dbName = $"lrn-race-{Guid.NewGuid()}";
        using var ctx = new RacingContext(dbName);

        var handler = new CreateStudentCommandHandler(ctx);
        var dto = await handler.Handle(new CreateStudentCommand(
            null, "Cara", "", "Reyes", new DateTime(2012, 5, 5), null, "Manila", null, null, null, null), default);

        // The rival took 100100100001; the retry regenerated past it.
        Assert.Equal("100100100002", dto.LRN);

        using var verify = TestDb.Create(dbName);
        Assert.Equal(2, await verify.Students.CountAsync());
    }
}
