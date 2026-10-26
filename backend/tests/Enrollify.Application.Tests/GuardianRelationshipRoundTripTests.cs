using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>GuardianRelationship must round-trip through the student CRUD and self-profile commands into StudentDto.</summary>
public class GuardianRelationshipRoundTripTests
{
    [Fact]
    public async Task UpdateStudent_SetsRelationship_AndDtoReturnsIt()
    {
        var ctx = TestDb.Create();
        var s = new Student { StudentNumber = "2026-00001", FirstName = "Ana", LastName = "Cruz", Address = "x", GuardianName = "Lola" };
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var dto = await new UpdateStudentCommandHandler(ctx).Handle(new UpdateStudentCommand(
            s.Id, null, "Ana", "", "Cruz", new DateTime(2012, 1, 1), null, "x",
            null, null, "Lola", null, null, "  Grandparent  "), default);

        Assert.Equal("Grandparent", dto.GuardianRelationship);
        Assert.Equal("Grandparent", (await ctx.Students.SingleAsync()).GuardianRelationship);

        // Blank clears it (staff form is the full record).
        var cleared = await new UpdateStudentCommandHandler(ctx).Handle(new UpdateStudentCommand(
            s.Id, null, "Ana", "", "Cruz", new DateTime(2012, 1, 1), null, "x",
            null, null, "Lola", null, null, " "), default);
        Assert.Null(cleared.GuardianRelationship);
    }

    [Fact]
    public async Task UpdateMyProfile_SetsRelationshipDirectly_OverwritingExisting_AndOmittedLeavesIt()
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2026-00002", FirstName = "Ben", LastName = "Uy", Address = "x", UserId = userId, GuardianRelationship = "Mother" };
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var handler = new UpdateMyProfileCommandHandler(ctx);
        var dto = await handler.Handle(new UpdateMyProfileCommand(userId, null, null, null, null, null, GuardianRelationship: "Aunt/Uncle"), default);
        Assert.Equal("Aunt/Uncle", dto.GuardianRelationship);
        Assert.Equal("Aunt/Uncle", (await ctx.Students.SingleAsync()).GuardianRelationship);

        var untouched = await handler.Handle(new UpdateMyProfileCommand(userId, "0917-000-0000", null, null, null, null), default);
        Assert.Equal("Aunt/Uncle", untouched.GuardianRelationship);
    }

    [Fact]
    public async Task UpdateChildProfile_SetsRelationship()
    {
        var ctx = TestDb.Create();
        var parentId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2026-00003", FirstName = "Cy", LastName = "Lim", Address = "x", ParentUserId = parentId };
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var dto = await new UpdateChildProfileCommandHandler(ctx).Handle(new UpdateChildProfileCommand(
            s.Id, parentId, null, null, null, null, null, GuardianRelationship: "Father"), default);

        Assert.Equal("Father", dto.GuardianRelationship);
        Assert.Equal("Father", (await ctx.Students.SingleAsync()).GuardianRelationship);
    }

    [Fact]
    public void Validators_RejectRelationshipOver50Chars()
    {
        var tooLong = new string('x', 51);
        Assert.False(new UpdateMyProfileCommandValidator().Validate(
            new UpdateMyProfileCommand(Guid.NewGuid(), null, null, null, null, null, GuardianRelationship: tooLong)).IsValid);
        Assert.False(new UpdateChildProfileCommandValidator().Validate(
            new UpdateChildProfileCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null, GuardianRelationship: tooLong)).IsValid);
        var staff = new UpdateStudentCommandValidator().Validate(
            new UpdateStudentCommand(Guid.NewGuid(), null, "A", "", "B", new DateTime(2012, 1, 1), null, "x",
                null, null, null, null, null, tooLong));
        Assert.Contains(staff.Errors, e => e.PropertyName == "GuardianRelationship");
    }
}
