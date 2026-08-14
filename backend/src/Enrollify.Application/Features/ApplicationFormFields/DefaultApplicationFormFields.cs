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
    private record FieldDefault(
        string Key, string Label, string Type, string Section, string AppliesTo,
        bool Required, bool Core, int Order, string? Options);

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
        new FieldDefault("email",                 "Email",                     "Text",     "Student",    "Both",       false, true,  5,  null),
        new FieldDefault("middleName",            "Middle Name",               "Text",     "Student",    "Both",       false, false, 10, null),
        new FieldDefault("contactNumber",         "Contact Number",            "Text",     "Student",    "Both",       false, false, 20, null),
        new FieldDefault("address",               "Address",                   "Text",     "Student",    "Both",       false, false, 30, null),
        // Enrollment section
        new FieldDefault("gradeLevel",            "Grade Level",               "Text",     "Enrollment", "Both",       true,  true,  1,  null),
        new FieldDefault("schoolYear",            "School Year",               "Text",     "Enrollment", "Both",       true,  true,  2,  null),
        new FieldDefault("previousSchool",        "Previous School",           "Text",     "Enrollment", "Both",       false, false, 10, null),
        new FieldDefault("previousSchoolAddress", "Previous School Address",   "Text",     "Enrollment", "Both",       false, false, 20, null),
        // Guardian section
        new FieldDefault("guardianName",          "Guardian Name",             "Text",     "Guardian",   "StudentMode", false, false, 10, null),
        new FieldDefault("guardianContact",       "Guardian Contact",          "Text",     "Guardian",   "StudentMode", false, false, 20, null),
        new FieldDefault("guardianRelationship",  "Relationship",              "Dropdown", "Guardian",   "StudentMode", false, false, 30, "[\"Mother\",\"Father\",\"Guardian\",\"Other\"]"),
    };

    /// <summary>
    /// Adds any missing default fields for the given tenant. Returns the number of fields inserted.
    /// Caller is responsible for SaveChangesAsync.
    /// </summary>
    public static async Task<int> EnsureFieldsForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var existingKeys = await context.ApplicationFormFields.IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.FieldKey)
            .ToListAsync(ct);

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
                Options = d.Options
            });
            added++;
        }
        return added;
    }
}
