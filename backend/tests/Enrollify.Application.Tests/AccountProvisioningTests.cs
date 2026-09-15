using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Auth.Commands;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>Every admin-issued temporary password must set MustChangePassword.</summary>
public class AccountProvisioningTests
{
    private class FakeJwt : IJwtTokenGenerator
    {
        public string GenerateToken(User user) => $"jwt-{user.Id}";
    }

    [Fact]
    public async Task Register_SetsMustChangePassword()
    {
        var ctx = TestDb.Create();

        var response = await new RegisterCommandHandler(ctx, new FakeJwt()).Handle(
            new RegisterCommand("new.parent@example.com", "Temp123!", "New", "Parent", "Parent", TestDb.TenantId), default);

        Assert.True(response.MustChangePassword);
        var user = await ctx.Users.IgnoreQueryFilters().SingleAsync();
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public async Task Approval_SetsMustChangePassword_OnCreatedAccount()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(new AdmissionApplication
        {
            ApplicationNumber = "APP-20260915-ACCT01",
            FirstName = "Migo", LastName = "Torres",
            Email = "migo@example.com", Gender = "Male",
            DateOfBirth = new DateTime(2011, 5, 5),
            GradeLevel = "Grade 8", SchoolYear = "2026-2027",
            Status = "Submitted", ApplicationType = "Student"
        });
        await ctx.SaveChangesAsync();
        var app = await ctx.AdmissionApplications.SingleAsync();

        await new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(app.Id, IsApproved: true, null), default);

        var user = await ctx.Users.IgnoreQueryFilters().SingleAsync();
        Assert.True(user.MustChangePassword);
    }

    private static async Task<(Enrollify.Infrastructure.Persistence.ApplicationDbContext Ctx, Student Student)> SeedStudentAsync(
        Guid? parentUserId = null)
    {
        var ctx = TestDb.Create();
        var student = new Student
        {
            LRN = "LRN-1601", FirstName = "Sol", LastName = "Acct", Address = "Manila",
            ParentUserId = parentUserId
        };
        ctx.Students.Add(student);
        await ctx.SaveChangesAsync();
        return (ctx, student);
    }

    [Fact]
    public async Task CreateStudentAccount_GeneratesTempPassword_SetsFlag_AndRefusesASecondAccount()
    {
        var (ctx, student) = await SeedStudentAsync();
        var handler = new CreateStudentAccountCommandHandler(ctx);

        // No password supplied → the server generates the temporary one and returns it once.
        var result = await handler.Handle(new CreateStudentAccountCommand(student.Id, "sol@example.com"), default);

        Assert.Equal("sol@example.com", result.Email);
        Assert.True(result.TemporaryPassword.Length >= 6);

        var user = await ctx.Users.IgnoreQueryFilters().SingleAsync();
        Assert.True(user.MustChangePassword);
        Assert.True(BCrypt.Net.BCrypt.Verify(result.TemporaryPassword, user.PasswordHash));
        Assert.Equal(user.Id, student.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateStudentAccountCommand(student.Id, "sol2@example.com"), default));
        Assert.Contains("already has", ex.Message);
    }

    [Fact]
    public async Task CreateStudentAccount_UsesProvidedPassword_WhenGiven()
    {
        var (ctx, student) = await SeedStudentAsync();

        var result = await new CreateStudentAccountCommandHandler(ctx)
            .Handle(new CreateStudentAccountCommand(student.Id, "sol@example.com", "Chosen123"), default);

        Assert.Equal("Chosen123", result.TemporaryPassword);
    }

    [Fact]
    public async Task CreateStudentAccount_RejectsParentManagedStudent()
    {
        // PRESERVED behavior the UI relies on: a student with ParentUserId set (even with no
        // UserId) is managed through the parent account and cannot get a separate login.
        var (ctx, student) = await SeedStudentAsync(parentUserId: Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CreateStudentAccountCommandHandler(ctx)
            .Handle(new CreateStudentAccountCommand(student.Id, "sol@example.com"), default));

        Assert.Contains("managed by a parent account", ex.Message);
        Assert.Empty(await ctx.Users.IgnoreQueryFilters().ToListAsync());
    }
}
