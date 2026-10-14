using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class StaffStudentEditTests
{
    private static readonly PsgcAddressDto ManilaBarangay = new(
        "1300000000", "NCR", null, null, "1380600000", "City of Manila", "1380601001", "Barangay 1", "1013");

    private static Student Legacy(string? lrn = "L-777", bool flagged = true) => new()
    {
        StudentNumber = "2024-00001", LRN = lrn, LrnNeedsReview = flagged,
        FirstName = "Mika", LastName = "Velasco", BirthDate = new DateTime(2012, 3, 10), Address = "Old line, Manila"
    };

    private static UpdateStudentCommand Update(Student s, string? lrn, string? contact = null, LearnerProfileDto? profile = null) => new(
        s.Id, lrn, s.FirstName, s.MiddleName, s.LastName, s.BirthDate, s.Gender, s.Address,
        contact, s.Email, s.GuardianName, s.GuardianContact, profile);

    [Fact]
    public async Task UnrelatedEdit_KeepsFlag_AndUntouchedLegacyLrn()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "L-777", contact: "0917-000-0000"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("0917-000-0000", reloaded.ContactNumber);
        Assert.Equal("L-777", reloaded.LRN);
        Assert.True(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ChangedLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "136512260001"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("136512260001", reloaded.LRN);
        Assert.False(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ClearingLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "  "), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Null(reloaded.LRN);
        Assert.False(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ChangedLrn_NotTwelveDigits_IsRefused()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateStudentCommandHandler(ctx).Handle(Update(s, "12345"), default));
        Assert.Contains("12 digits", ex.Message);
    }

    [Fact]
    public async Task ChangedLrn_TakenByAnotherStudent_IsRefused()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        ctx.Students.Add(new Student { StudentNumber = "2024-00002", LRN = "136512260001", FirstName = "B", LastName = "C", Address = "" });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateStudentCommandHandler(ctx).Handle(Update(s, "136512260001"), default));
    }

    [Fact]
    public async Task UpdateWithProfile_AppliesLearnerFieldsAndAddress()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "L-777",
            profile: new LearnerProfileDto(Religion: "Catholic", HomeAddress: ManilaBarangay)), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("Catholic", reloaded.Religion);
        Assert.Equal("1380601001", reloaded.HomeAddress.BarangayCode);
    }

    [Fact]
    public async Task ConfirmLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy(lrn: "136512260001");
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var dto = await new ConfirmLrnCommandHandler(ctx).Handle(new ConfirmLrnCommand(s.Id), default);

        Assert.False(dto.LrnNeedsReview);
        Assert.False((await ctx.Students.SingleAsync()).LrnNeedsReview);
    }

    [Theory]
    [InlineData(null, "no LRN")]
    [InlineData("L-777", "not 12 digits")]
    public async Task ConfirmLrn_RefusesMissingOrMalformed(string? lrn, string messagePart)
    {
        var ctx = TestDb.Create();
        var s = Legacy(lrn: lrn);
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConfirmLrnCommandHandler(ctx).Handle(new ConfirmLrnCommand(s.Id), default));
        Assert.Contains(messagePart, ex.Message);
    }

    [Fact]
    public void CreateValidator_RequiresPsgcCityAndBarangay()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "12 Mabini St", null, null, null, null));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Choose the learner's city/municipality and barangay.");
    }

    [Fact]
    public void CreateValidator_AcceptsPsgcAddressWithoutStreetLine()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "", null, null, null, null,
            Profile: new LearnerProfileDto(HomeAddress: ManilaBarangay)));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void UpdateValidator_AllowsLegacyAddressLineWithoutPsgc()
    {
        var s = Legacy();
        Assert.True(new UpdateStudentCommandValidator().Validate(Update(s, "L-777")).IsValid);
    }

    [Fact]
    public void UpdateValidator_RequiresEitherPsgcOrAddressLine()
    {
        var s = Legacy();
        s.Address = "";
        var result = new UpdateStudentCommandValidator().Validate(Update(s, "L-777"));
        Assert.Contains(result.Errors, e => e.PropertyName == "Address");
    }
}
