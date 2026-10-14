using Enrollify.Application.Features.ApplicationFormFields;
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

    private static ApplicationFormField Custom(string key, string label, string type = "Text", string section = "Enrollment",
        string appliesTo = "Both", bool required = false, bool visible = true, string? options = null) => new()
    {
        FieldKey = key, Label = label, FieldType = type, Section = section, AppliesTo = appliesTo,
        IsRequired = required, IsVisible = visible, IsBuiltIn = false, Options = options
    };

    [Fact]
    public async Task ConvertsCustomLrnField_KeepingLabelVisibilityRequirednessAndAppliesTo()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(Custom("lrn", "Learner Ref No.", type: "Number", appliesTo: "ParentMode", required: true, options: "[]"));
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        var f = await ctx.ApplicationFormFields.SingleAsync();
        Assert.True(f.IsBuiltIn);
        Assert.Equal("Text", f.FieldType);
        Assert.Equal("Student", f.Section);
        Assert.Equal("ParentMode", f.AppliesTo);
        Assert.Equal("Learner Ref No.", f.Label);
        Assert.True(f.IsRequired);
        Assert.Null(f.Options);
    }

    [Fact]
    public async Task CaseVariantCustomKey_GetsNoDuplicateFromEnsure_AndIsConvertedToTheBuiltIn()
    {
        // SQL Server's (TenantId, FieldKey) unique index is case-insensitive: inserting the built-in
        // "motherTongue" next to a custom "mothertongue" would crash the startup seed.
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(Custom("mothertongue", "Native language", type: "Dropdown", appliesTo: "StudentMode",
            required: true, visible: false, options: "[\"Tagalog\",\"Cebuano\"]"));
        await ctx.SaveChangesAsync();

        await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();
        Assert.Single(await ctx.ApplicationFormFields
            .Where(f => f.FieldKey.ToLower() == "mothertongue").ToListAsync());

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        var f = await ctx.ApplicationFormFields.SingleAsync(x => x.FieldKey.ToLower() == "mothertongue");
        Assert.Equal("motherTongue", f.FieldKey);       // canonical key
        Assert.True(f.IsBuiltIn);
        Assert.Equal("Text", f.FieldType);
        Assert.Equal("Student", f.Section);
        Assert.Null(f.Options);
        Assert.Equal("Native language", f.Label);       // kept
        Assert.Equal("StudentMode", f.AppliesTo);       // kept
        Assert.True(f.IsRequired);                      // kept
        Assert.False(f.IsVisible);                      // kept

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task ExactCaseCustomReligion_IsConvertedInPlace_AndSecondRunIsANoOp()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(Custom("religion", "Faith", type: "Dropdown", options: "[\"Catholic\",\"INC\"]"));
        ctx.AdmissionApplications.Add(App("{\"religion\":\"INC\"}"));
        await ctx.SaveChangesAsync();

        Assert.True(await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId) > 0);
        await ctx.SaveChangesAsync();

        var f = await ctx.ApplicationFormFields.SingleAsync();
        Assert.Equal("religion", f.FieldKey);
        Assert.True(f.IsBuiltIn);
        Assert.Equal("Text", f.FieldType);
        Assert.Equal("Student", f.Section);
        Assert.Null(f.Options);
        Assert.Equal("Faith", f.Label);
        // Only the LRN moves into a typed column; other old answers stay in the custom JSON.
        var app = await ctx.AdmissionApplications.SingleAsync();
        Assert.Null(app.Religion);
        Assert.Equal("{\"religion\":\"INC\"}", app.CustomFieldValues);

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task PreviousSchoolBuiltIns_StillOnTheOldDefaultLabel_AreRelabelled_Once()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField { FieldKey = "previousSchool", Label = "Previous School", Section = "Enrollment", IsBuiltIn = true });
        ctx.ApplicationFormFields.Add(new ApplicationFormField { FieldKey = "previousSchoolAddress", Label = "Previous School Address", Section = "Enrollment", IsBuiltIn = true });
        await ctx.SaveChangesAsync();

        Assert.Equal(2, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();

        Assert.Equal("Last School Attended", (await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "previousSchool")).Label);
        Assert.Equal("Last School Address", (await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "previousSchoolAddress")).Label);
        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task PreviousSchoolBuiltIn_WithASchoolChosenLabel_IsLeftAlone()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField { FieldKey = "previousSchool", Label = "School last year", Section = "Enrollment", IsBuiltIn = true });
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
        Assert.Equal("School last year", (await ctx.ApplicationFormFields.SingleAsync()).Label);
    }

    [Fact]
    public async Task NonTwelveDigitCustomLrnAnswers_StayInTheCustomJson()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"N/A\"}"));
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"123\"}"));
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"1365122600019\"}"));
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();

        var apps = await ctx.AdmissionApplications.ToListAsync();
        Assert.All(apps, a => Assert.Null(a.Lrn));
        Assert.Contains(apps, a => a.CustomFieldValues == "{\"lrn\":\"N/A\"}");
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
