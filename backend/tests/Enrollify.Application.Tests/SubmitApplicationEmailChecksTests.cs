using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Duplicate-email rejection at application SUBMISSION time (not approval):
/// anonymous applications must not reuse an email that already has an account.
/// </summary>
public class SubmitApplicationEmailChecksTests
{
    private static SubmitApplicationCommand.Applicant Applicant(string email = "") => new(
        "Test", null, "Child", email, null, "Male",
        new DateTime(2015, 1, 1), null, "Grade 6", "2025-2026",
        null, null, null, null, null, null);

    private static User ExistingUser(string email, UserRole role) => new()
    {
        Email = email,
        PasswordHash = "hash",
        FirstName = "Existing",
        LastName = "User",
        Role = role
    };

    [Fact]
    public async Task ParentMode_WithUsedParentEmail_Throws()
    {
        using var db = TestDb.Create();
        db.Users.Add(ExistingUser("taken@example.com", UserRole.Parent));
        await db.SaveChangesAsync();

        var handler = new SubmitApplicationCommandHandler(db);
        var cmd = new SubmitApplicationCommand(null, "Parent", "Ann", "Reyes",
            "Taken@Example.com", null, new List<SubmitApplicationCommand.Applicant> { Applicant() }, TestDb.TenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd, default));
        Assert.Contains("already exists", ex.Message);
        Assert.Empty(db.AdmissionApplications);
    }

    [Fact]
    public async Task StudentMode_WithUsedEmail_Throws()
    {
        using var db = TestDb.Create();
        db.Users.Add(ExistingUser("student@example.com", UserRole.Student));
        await db.SaveChangesAsync();

        var handler = new SubmitApplicationCommandHandler(db);
        var cmd = new SubmitApplicationCommand(null, "Student", null, null, null, null,
            new List<SubmitApplicationCommand.Applicant> { Applicant("STUDENT@example.com") }, TestDb.TenantId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(cmd, default));
        Assert.Contains("already exists", ex.Message);
        Assert.Empty(db.AdmissionApplications);
    }

    [Fact]
    public async Task ParentMode_WithFreshEmail_Succeeds()
    {
        using var db = TestDb.Create();
        db.Users.Add(ExistingUser("someoneelse@example.com", UserRole.Parent));
        await db.SaveChangesAsync();

        var handler = new SubmitApplicationCommandHandler(db);
        var cmd = new SubmitApplicationCommand(null, "Parent", "Ann", "Reyes",
            "fresh@example.com", null, new List<SubmitApplicationCommand.Applicant> { Applicant() }, TestDb.TenantId);

        var result = await handler.Handle(cmd, default);

        Assert.Single(result);
        Assert.Single(db.AdmissionApplications);
    }

    [Fact]
    public async Task AuthenticatedParent_ReApply_SkipsEmailCheck()
    {
        using var db = TestDb.Create();
        var parent = ExistingUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new SubmitApplicationCommandHandler(db);
        // Authenticated parents re-apply through their own account; their email obviously
        // exists and must not be rejected.
        var cmd = new SubmitApplicationCommand(parent.Id, "Parent", null, null,
            "parent@example.com", null, new List<SubmitApplicationCommand.Applicant> { Applicant() }, TestDb.TenantId);

        var result = await handler.Handle(cmd, default);

        Assert.Single(result);
    }
}
