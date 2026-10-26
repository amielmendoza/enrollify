using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class LisEncodedTests
{
    private static async Task<Enrollment> AddAsync(ApplicationDbContext ctx, EnrollmentStatus status,
        DateTime? encodedAt = null, string? encodedBy = null)
    {
        var n = Guid.NewGuid().ToString("N")[..6];
        var student = new Student { LRN = $"LRN-{n}", FirstName = "Lis", LastName = n, Address = "Baguio" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status,
            LisEncodedAt = encodedAt,
            LisEncodedBy = encodedBy
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return enrollment;
    }

    private static Task<int> Run(ApplicationDbContext ctx, IEnumerable<Guid> ids, bool encoded, string by = "Registrar Rita") =>
        new SetLisEncodedCommandHandler(ctx).Handle(new SetLisEncodedCommand(ids.ToList(), encoded, by), default);

    private static Task<Enrollment> Fresh(string db, Guid id) =>
        TestDb.Create(db).Enrollments.AsNoTracking().SingleAsync(e => e.Id == id);

    [Fact]
    public async Task Encode_StampsNameAndTime_OnUnmarked()
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var a = await AddAsync(ctx, EnrollmentStatus.Approved);
        var b = await AddAsync(ctx, EnrollmentStatus.Paid);
        var c = await AddAsync(ctx, EnrollmentStatus.Enrolled);
        var before = DateTime.UtcNow;

        var changed = await Run(ctx, new[] { a.Id, b.Id, c.Id }, true);

        Assert.Equal(3, changed);
        foreach (var id in new[] { a.Id, b.Id, c.Id })
        {
            var e = await Fresh(db, id);
            Assert.Equal("Registrar Rita", e.LisEncodedBy);
            Assert.NotNull(e.LisEncodedAt);
            Assert.InRange(e.LisEncodedAt!.Value, before, DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task Encode_AlreadyMarked_KeepsOriginalStamp_AndIsNotCounted()
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var oldAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var marked = await AddAsync(ctx, EnrollmentStatus.Approved, oldAt, "Admin Ana");
        var fresh = await AddAsync(ctx, EnrollmentStatus.Approved);

        var changed = await Run(ctx, new[] { marked.Id, fresh.Id }, true, "Registrar Rita");

        Assert.Equal(1, changed);
        var m = await Fresh(db, marked.Id);
        Assert.Equal(oldAt, m.LisEncodedAt);
        Assert.Equal("Admin Ana", m.LisEncodedBy);
        var f = await Fresh(db, fresh.Id);
        Assert.Equal("Registrar Rita", f.LisEncodedBy);
        Assert.NotNull(f.LisEncodedAt);
        Assert.True(f.LisEncodedAt > oldAt);
    }

    [Fact]
    public async Task Encode_AllAlreadyMarked_ReturnsZero()
    {
        var ctx = TestDb.Create();
        var a = await AddAsync(ctx, EnrollmentStatus.Enrolled, DateTime.UtcNow.AddDays(-1), "Admin Ana");

        Assert.Equal(0, await Run(ctx, new[] { a.Id }, true));
    }

    [Fact]
    public async Task Unmark_ClearsBothFields_AndCountsOnlyMarked()
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var marked1 = await AddAsync(ctx, EnrollmentStatus.Approved, DateTime.UtcNow.AddDays(-2), "Admin Ana");
        var marked2 = await AddAsync(ctx, EnrollmentStatus.Paid, DateTime.UtcNow.AddDays(-1), "Admin Ana");
        var unmarked = await AddAsync(ctx, EnrollmentStatus.Enrolled);

        var changed = await Run(ctx, new[] { marked1.Id, marked2.Id, unmarked.Id }, false);

        Assert.Equal(2, changed);
        foreach (var id in new[] { marked1.Id, marked2.Id, unmarked.Id })
        {
            var e = await Fresh(db, id);
            Assert.Null(e.LisEncodedAt);
            Assert.Null(e.LisEncodedBy);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingId_RejectsWholeRequest_AndChangesNothing(bool encoded)
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        DateTime? oldAt = encoded ? null : new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        string? oldBy = encoded ? null : "Admin Ana";
        var a = await AddAsync(ctx, EnrollmentStatus.Approved, oldAt, oldBy);
        var b = await AddAsync(ctx, EnrollmentStatus.Paid, oldAt, oldBy);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(ctx, new[] { a.Id, Guid.NewGuid(), b.Id }, encoded));

        foreach (var id in new[] { a.Id, b.Id })
        {
            var e = await Fresh(db, id);
            Assert.Equal(oldAt, e.LisEncodedAt);
            Assert.Equal(oldBy, e.LisEncodedBy);
        }
    }

    [Fact]
    public async Task OtherTenantsId_IsTreatedAsMissing_AndRejected()
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var mine = await AddAsync(ctx, EnrollmentStatus.Approved);

        var otherTenant = Guid.NewGuid();
        var otherCtx = new ApplicationDbContext(TestDb.Options(db), new FixedTenantProvider(otherTenant));
        var theirs = await AddAsync(otherCtx, EnrollmentStatus.Approved);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(ctx, new[] { mine.Id, theirs.Id }, true));

        Assert.Null((await Fresh(db, mine.Id)).LisEncodedAt);
        var theirsFresh = await new ApplicationDbContext(TestDb.Options(db), new FixedTenantProvider(otherTenant))
            .Enrollments.AsNoTracking().SingleAsync(e => e.Id == theirs.Id);
        Assert.Null(theirsFresh.LisEncodedAt);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft)]
    [InlineData(EnrollmentStatus.Submitted)]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Cancelled)]
    public async Task NotListed_RejectsWholeRequest_AndChangesNothing(EnrollmentStatus status)
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var listed = await AddAsync(ctx, EnrollmentStatus.Approved);
        var notListed = await AddAsync(ctx, status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Run(ctx, new[] { listed.Id, notListed.Id }, true));

        Assert.Contains(status.ToString(), ex.Message);
        Assert.Null((await Fresh(db, listed.Id)).LisEncodedAt);
        Assert.Null((await Fresh(db, notListed.Id)).LisEncodedAt);
    }

    [Fact]
    public async Task NotListed_RejectedEvenWhenUnmarking()
    {
        var db = $"lis-{Guid.NewGuid()}";
        var ctx = TestDb.Create(db);
        var oldAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var listed = await AddAsync(ctx, EnrollmentStatus.Approved, oldAt, "Admin Ana");
        var cancelled = await AddAsync(ctx, EnrollmentStatus.Cancelled, oldAt, "Admin Ana");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(ctx, new[] { listed.Id, cancelled.Id }, false));

        Assert.Equal(oldAt, (await Fresh(db, listed.Id)).LisEncodedAt);
        Assert.Equal(oldAt, (await Fresh(db, cancelled.Id)).LisEncodedAt);
    }

    // ----- validator -----

    private static bool Valid(IReadOnlyList<Guid> ids) =>
        new SetLisEncodedCommandValidator().Validate(new SetLisEncodedCommand(ids, true, "x")).IsValid;

    [Fact]
    public void Validator_RejectsEmpty() => Assert.False(Valid(new List<Guid>()));

    [Fact]
    public void Validator_Accepts1And500Distinct()
    {
        Assert.True(Valid(new[] { Guid.NewGuid() }));
        Assert.True(Valid(Enumerable.Range(0, 500).Select(_ => Guid.NewGuid()).ToList()));
    }

    [Fact]
    public void Validator_RejectsMoreThan500() =>
        Assert.False(Valid(Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList()));

    [Fact]
    public void Validator_RejectsDuplicateIds()
    {
        var id = Guid.NewGuid();
        Assert.False(Valid(new[] { id, Guid.NewGuid(), id }));
    }
}
