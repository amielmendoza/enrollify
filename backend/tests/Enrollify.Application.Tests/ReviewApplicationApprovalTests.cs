using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Domain.Interfaces;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ReviewApplicationApprovalTests
{
    private static AdmissionApplication StudentApplication() => new()
    {
        ApplicationNumber = "APP-20260801-TEST01",
        FirstName = "Mika",
        LastName = "Velasco",
        Email = "mika@example.com",
        Gender = "Female",
        DateOfBirth = new DateTime(2012, 3, 10),
        GradeLevel = "Grade 7",
        SchoolYear = "2026-2027",
        Status = "Submitted",
        ApplicationType = "Student",
        // The live bug: these are optional on the application form and may be null.
        MiddleName = null,
        ContactNumber = null,
        Address = null
    };

    [Fact]
    public async Task Approve_WithNullOptionalFields_Succeeds()
    {
        var ctx = TestDb.Create();
        var app = StudentApplication();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        var email = new RecordingEmailSender();
        var handler = new ReviewApplicationCommandHandler(ctx, email);
        var dto = await handler.Handle(new ReviewApplicationCommand(app.Id, IsApproved: true, "Welcome!"), default);

        Assert.Equal("Approved", dto.Status);
        Assert.NotNull(dto.StudentId);

        var student = await ctx.Students.SingleAsync();
        Assert.Equal(string.Empty, student.Address);       // transcribed, never re-validated
        Assert.Equal(string.Empty, student.MiddleName);
        Assert.Null(student.ContactNumber);
        Assert.Equal(12, student.LRN.Length);              // auto-generated LRN
        Assert.NotNull(student.UserId);                    // student-mode login account

        var user = await ctx.Users.SingleAsync();
        Assert.Equal(student.UserId, user.Id);
        Assert.Equal(UserRole.Student, user.Role);

        var enrollment = await ctx.Enrollments.SingleAsync();
        Assert.Equal(student.Id, enrollment.StudentId);
        Assert.Equal(EnrollmentStatus.Draft, enrollment.Status);

        var sent = Assert.Single(email.Sent);
        Assert.Equal("mika@example.com", sent.To);
        Assert.Contains("approved", sent.Subject);
    }

    /// <summary>Throws on demand at SaveChangesAsync — simulates a save failing AFTER all in-memory work.</summary>
    private class FailingSaveContext : ApplicationDbContext
    {
        public bool Fail { get; set; }

        public FailingSaveContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider)
            : base(options, tenantProvider) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Fail
                ? throw new InvalidOperationException("Simulated save failure.")
                : base.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task FailedApproval_LeavesApplicationSubmitted_WithNoOrphanUserOrStudent()
    {
        var dbName = $"approval-orphans-{Guid.NewGuid()}";

        Guid appId;
        using (var seed = TestDb.Create(dbName))
        {
            var app = StudentApplication();
            seed.AdmissionApplications.Add(app);
            await seed.SaveChangesAsync();
            appId = app.Id;
        }

        using (var failing = new FailingSaveContext(TestDb.Options(dbName), new FixedTenantProvider(TestDb.TenantId)) { Fail = true })
        {
            var handler = new ReviewApplicationCommandHandler(failing, new RecordingEmailSender());
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.Handle(new ReviewApplicationCommand(appId, IsApproved: true, null), default));
        }

        // Verify persisted state through a fresh context: with the single-save structure,
        // nothing from the failed approval leaked — the registrar can retry cleanly.
        using var verify = TestDb.Create(dbName);
        var persisted = await verify.AdmissionApplications.SingleAsync(a => a.Id == appId);
        Assert.Equal("Submitted", persisted.Status);
        Assert.Null(persisted.ReviewedAt);
        Assert.Null(persisted.StudentId);
        Assert.Empty(await verify.Users.ToListAsync());
        Assert.Empty(await verify.Students.ToListAsync());
        Assert.Empty(await verify.Enrollments.ToListAsync());
    }
}
