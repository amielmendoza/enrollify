using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ApplicationFormFields;

/// <summary>
/// The single source of truth for the default field set every tenant should have.
/// Used by the boot-time seeder, the SuperAdmin tenant-creation flow, and the
/// admin-facing "Restore Defaults" command. Idempotent — only fields whose key is
/// missing for the target tenant get inserted.
/// </summary>
public static class DefaultApplicationFormFields
{
    public const string LrnHelpText = "12 digits. Leave blank if none yet (e.g. incoming Kindergarten).";

    internal sealed record FieldDefault(
        string Key, string Label, string Type, string Section, string AppliesTo,
        bool Required, bool Core, int Order, string? Options, string? HelpText = null);

    /// <summary>The default built-in definitions (read-only), e.g. for the startup backfill.</summary>
    internal static IReadOnlyList<FieldDefault> All => Defaults;

    private static readonly FieldDefault[] Defaults = new[]
    {
        // Parent section — core (locked) first, then configurable
        new FieldDefault("parentFirstName",       "Parent First Name",         "Text",     "Parent",     "ParentMode", true,  true,  1,  null),
        new FieldDefault("parentLastName",        "Parent Last Name",          "Text",     "Parent",     "ParentMode", true,  true,  2,  null),
        new FieldDefault("parentEmail",           "Parent Email",              "Text",     "Parent",     "ParentMode", true,  true,  3,  null),
        new FieldDefault("parentContactNumber",   "Parent Contact",            "Text",     "Parent",     "ParentMode", false, false, 10, null),
        new FieldDefault("parentRelationship",    "Relationship to children",  "Dropdown", "Parent",     "ParentMode", true,  false, 20, "[\"Mother\",\"Father\",\"Guardian\",\"Other\"]"),
        // Student section
        new FieldDefault("firstName",             "First Name",                "Text",     "Student",    "Both",       true,  true,  1,  null),
        new FieldDefault("lastName",              "Last Name",                 "Text",     "Student",    "Both",       true,  true,  2,  null),
        new FieldDefault("dateOfBirth",           "Date of Birth",             "Date",     "Student",    "Both",       true,  true,  3,  null),
        new FieldDefault("gender",                "Gender",                    "Dropdown", "Student",    "Both",       true,  true,  4,  "[\"Male\",\"Female\"]"),
        // Email is core (always visible, not deletable) yet OPTIONAL by design: parent-mode
        // children don't need a login of their own (the account comes from the parent's email),
        // and student-mode requiredness is hardcoded in SubmitApplicationCommandValidator
        // because that email becomes the student's login. Don't flip Required here.
        new FieldDefault("email",                 "Email",                     "Text",     "Student",    "Both",       false, true,  5,  null),
        new FieldDefault("middleName",            "Middle Name",               "Text",     "Student",    "Both",       false, false, 10, null),
        new FieldDefault("contactNumber",         "Contact Number",            "Text",     "Student",    "Both",       false, false, 20, null),
        new FieldDefault("address",               "Address",                   "Text",     "Student",    "Both",       false, false, 30, null),
        // Enrollment section
        new FieldDefault("gradeLevel",            "Grade Level",               "Text",     "Enrollment", "Both",       true,  true,  1,  null),
        new FieldDefault("schoolYear",            "School Year",               "Text",     "Enrollment", "Both",       true,  true,  2,  null),
        new FieldDefault("previousSchool",        "Last School Attended",        "Text",     "Enrollment", "Both",       false, false, 10, null),
        new FieldDefault("previousSchoolAddress", "Last School Address",          "Text",     "Enrollment", "Both",       false, false, 20, null),
        // Guardian section
        new FieldDefault("guardianName",          "Guardian Name",             "Text",     "Guardian",   "StudentMode", false, false, 10, null),
        new FieldDefault("guardianContact",       "Guardian Contact",          "Text",     "Guardian",   "StudentMode", false, false, 20, null),
        new FieldDefault("guardianRelationship",  "Relationship",              "Dropdown", "Guardian",   "StudentMode", false, false, 30, "[\"Mother\",\"Father\",\"Guardian\",\"Other\"]"),
        // DepEd learner record — Student section
        new FieldDefault("lrn",                       "LRN (Learner Reference Number)",                 "Text",     "Student",    "Both", false, false, 6,  null, LrnHelpText),
        new FieldDefault("extensionName",             "Name Extension (Jr., III)",                      "Text",     "Student",    "Both", false, false, 11, null),
        new FieldDefault("psaBirthCertificateNumber", "PSA Birth Certificate No.",                      "Text",     "Student",    "Both", false, false, 40, null),
        new FieldDefault("placeOfBirth",              "Place of Birth (City/Municipality)",             "Text",     "Student",    "Both", false, false, 41, null),
        new FieldDefault("motherTongue",              "Mother Tongue",                                  "Text",     "Student",    "Both", false, false, 42, null),
        new FieldDefault("religion",                  "Religion",                                       "Text",     "Student",    "Both", false, false, 43, null),
        new FieldDefault("indigenousPeople",          "Belongs to an Indigenous Peoples (IP) community", "Checkbox", "Student",    "Both", false, false, 50, null),
        new FieldDefault("fourPs",                    "4Ps beneficiary household",                      "Checkbox", "Student",    "Both", false, false, 51, null),
        new FieldDefault("disability",                "Learner with disability",                        "Checkbox", "Student",    "Both", false, false, 52, null),
        // Family section (per child — siblings can have different parents)
        new FieldDefault("fatherName",                "Father's Name",                                  "Text",     "Family",     "Both", false, false, 10, null),
        new FieldDefault("motherMaidenName",          "Mother's Maiden Name",                           "Text",     "Family",     "Both", false, false, 20, null),
        // Enrollment section
        new FieldDefault("strand",                    "Track / Strand",                                 "Dropdown", "Enrollment", "Both", false, true,  3,  null,
            "Shown only for Grade 11–12 when the school offers strands; always required there."),
        new FieldDefault("learnerType",               "Learner Type",                                   "Dropdown", "Enrollment", "Both", false, false, 5,  "[\"New\",\"Continuing\",\"Transferee\",\"Returning\"]"),
        new FieldDefault("lastGradeLevelCompleted",   "Last Grade Level Completed",                     "Text",     "Enrollment", "Both", false, false, 30, null),
        new FieldDefault("lastSchoolYearCompleted",   "Last School Year Completed",                     "Text",     "Enrollment", "Both", false, false, 31, null),
        new FieldDefault("lastSchoolId",              "Last School's DepEd School ID",                  "Text",     "Enrollment", "Both", false, false, 32, null),
    };

    /// <summary>
    /// Adds any missing default fields for the given tenant. Returns the number of fields inserted.
    /// Keys compare case-insensitively, like SQL Server's (TenantId, FieldKey) unique index: a custom
    /// "mothertongue" blocks the built-in (LearnerRecordBackfill converts it in place instead).
    /// Caller is responsible for SaveChangesAsync.
    /// </summary>
    public static async Task<int> EnsureFieldsForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var existingKeys = new HashSet<string>(
            await context.ApplicationFormFields.IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .Select(f => f.FieldKey)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var d in Defaults)
        {
            if (existingKeys.Contains(d.Key)) continue;
            context.ApplicationFormFields.Add(new ApplicationFormField
            {
                TenantId = tenantId,
                FieldKey = d.Key,
                Label = d.Label,
                FieldType = d.Type,
                Section = d.Section,
                AppliesTo = d.AppliesTo,
                IsRequired = d.Required,
                IsVisible = true,
                IsBuiltIn = true,
                IsCore = d.Core,
                DisplayOrder = d.Order,
                Options = d.Options,
                HelpText = d.HelpText
            });
            added++;
        }
        return added;
    }
}
