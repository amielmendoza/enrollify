using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class LearnerRecordBackfillTests
{
    private static AdmissionApplication App(string? customJson, string? lrn = null) => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8]}", FirstName = "A", LastName = "B", Email = "",
        Gender = "Female", GradeLevel = "Grade 7", SchoolYear = "2026-2027", Status = "Submitted",
        CustomFieldValues = customJson, Lrn = lrn
    };

    [Fact]
    public async Task ConvertsCustomLrnField_KeepingLabelVisibilityAndRequiredness()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField
        {
            FieldKey = "lrn", Label = "Learner Ref No.", FieldType = "Number", Section = "Enrollment",
            AppliesTo = "ParentMode", IsRequired = true, IsVisible = true, IsBuiltIn = false, Options = "[]"
        });
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        var f = await ctx.ApplicationFormFields.SingleAsync();
        Assert.True(f.IsBuiltIn);
        Assert.Equal("Text", f.FieldType);
        Assert.Equal("Student", f.Section);
        Assert.Equal("Both", f.AppliesTo);
        Assert.Equal("Learner Ref No.", f.Label);
        Assert.True(f.IsRequired);
        Assert.Null(f.Options);
    }

    [Fact]
    public async Task MovesCustomValuesIntoTheColumn_CaseInsensitively()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(App("{\"LRN\":\" 136512260001 \",\"allergies\":\"none\"}"));
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        Assert.Equal("136512260001", (await ctx.AdmissionApplications.SingleAsync()).Lrn);
    }

    [Fact]
    public async Task SecondRun_IsANoOp()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField { FieldKey = "lrn", Label = "LRN", FieldType = "Text", Section = "Student", AppliesTo = "Both" });
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"136512260001\"}"));
        await ctx.SaveChangesAsync();

        Assert.True(await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId) > 0);
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task ExistingColumnValue_IsLeftAlone_AndMalformedJsonIgnored()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"999999999999\"}", lrn: "136512260001"));
        ctx.AdmissionApplications.Add(App("{not json"));
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task NeverTouchesStudentLrnFlags()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(new Student { StudentNumber = "2024-00001", LRN = "136512260001", LrnNeedsReview = false, FirstName = "A", LastName = "B", Address = "" });
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        Assert.False((await ctx.Students.SingleAsync()).LrnNeedsReview);
    }
}
