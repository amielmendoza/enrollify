using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class SubmitApplicationGuardTests
{
    private static SubmitApplicationCommand.Applicant Applicant(
        string? contactNumber = null, string? guardianRelationship = null) => new(
        "Mika", null, "Velasco",
        "mika@example.com", contactNumber, "Female",
        new DateTime(2012, 3, 10), null,
        "Grade 7", "2026-2027",
        null, null,
        null, null, guardianRelationship,
        null);

    private static SubmitApplicationCommand AnonymousParentCommand(SubmitApplicationCommand.Applicant applicant) => new(
        AuthenticatedParentUserId: null,
        ApplicationType: "Parent",
        ParentFirstName: "Rosa", ParentLastName: "Velasco",
        ParentEmail: "rosa@example.com", ParentContactNumber: "0917-555-0000",
        Applicants: new List<SubmitApplicationCommand.Applicant> { applicant },
        TenantId: TestDb.TenantId);

    // M5: over-length input must fail FluentValidation (→ 400 via the pipeline), never
    // reach SaveChanges as a SqlException.
    [Fact]
    public void Validator_RejectsOverlengthContactNumber()
    {
        var validator = new SubmitApplicationCommandValidator();
        var command = AnonymousParentCommand(Applicant(contactNumber: new string('9', 51)));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("ContactNumber"));
    }

    [Fact]
    public void Validator_AcceptsMaxLengthContactNumber()
    {
        var validator = new SubmitApplicationCommandValidator();
        var command = AnonymousParentCommand(Applicant(contactNumber: new string('9', 50), guardianRelationship: "Mother"));

        Assert.True(validator.Validate(command).IsValid);
    }

    private static async Task<ApplicationDbContext> SeedDefaultFormFieldsAsync()
    {
        var ctx = TestDb.Create();
        await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    // M7: parentRelationship is a built-in, non-core field that defaults to required — the
    // handler must enforce it server-side for anonymous parent submissions (the UI maps it
    // onto each applicant's GuardianRelationship).
    [Fact]
    public async Task AnonymousParent_MissingRequiredRelationship_Throws()
    {
        var ctx = await SeedDefaultFormFieldsAsync();
        var handler = new SubmitApplicationCommandHandler(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(AnonymousParentCommand(Applicant(guardianRelationship: null)), default));

        Assert.Equal("'Relationship to children' is required.", ex.Message);
        Assert.Empty(await ctx.AdmissionApplications.ToListAsync());
    }

    [Fact]
    public async Task AnonymousParent_WithRelationship_Succeeds()
    {
        var ctx = await SeedDefaultFormFieldsAsync();
        var handler = new SubmitApplicationCommandHandler(ctx);

        var result = await handler.Handle(AnonymousParentCommand(Applicant(guardianRelationship: "Mother")), default);

        var dto = Assert.Single(result);
        Assert.Equal("Submitted", dto.Status);
        Assert.Single(await ctx.AdmissionApplications.ToListAsync());
    }
}
