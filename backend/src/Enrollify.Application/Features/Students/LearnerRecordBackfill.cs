using System.Text.Json;
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Idempotent per-tenant startup backfill for the learner-record release; a second run changes nothing.
/// <list type="bullet">
/// <item>A CUSTOM form field whose key matches a default built-in key (case-insensitively — e.g. a
/// school's own "lrn", "religion" or "mothertongue") becomes that built-in in place: canonical key,
/// type, section, options and help text from the default; the school's label, visibility,
/// requiredness and AppliesTo are kept.</item>
/// <item>Built-in previous-school fields still on their old default label get the new one.</item>
/// <item>Saved custom "lrn" answers move into AdmissionApplications.Lrn — only 12-digit values;
/// anything else stays in the custom JSON. Other converted keys' answers stay there too.</item>
/// </list>
/// It never touches Students.LrnNeedsReview — flagging is the AddLearnerRecord migration's one-time
/// job, so restarts can't re-flag confirmed LRNs. Caller saves.
/// </summary>
public static class LearnerRecordBackfill
{
    private static readonly (string Key, string OldLabel)[] RenamedLabels =
    {
        ("previousSchool", "Previous School"),
        ("previousSchoolAddress", "Previous School Address"),
    };

    public static async Task<int> RunForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var fields = await context.ApplicationFormFields.IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .ToListAsync(ct);

        var changes = ConvertCollidingCustomFields(fields) + RelabelOldDefaults(fields);

        // SQL Server's default collation makes Contains case-insensitive; the InMemory provider is
        // case-sensitive, hence both spellings. ExtractLrn's key match is the real check.
        var candidates = await context.AdmissionApplications.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Lrn == null && a.CustomFieldValues != null
                        && (a.CustomFieldValues.Contains("lrn") || a.CustomFieldValues.Contains("LRN")))
            .ToListAsync(ct);
        foreach (var app in candidates)
        {
            var lrn = ExtractLrn(app.CustomFieldValues);
            if (!DepEdLists.IsTwelveDigitLrn(lrn)) continue; // e.g. "N/A" stays under Additional Information
            app.Lrn = lrn;
            changes++;
        }

        return changes;
    }

    private static int ConvertCollidingCustomFields(List<ApplicationFormField> fields)
    {
        var changes = 0;
        foreach (var d in DefaultApplicationFormFields.All)
        {
            bool Matches(ApplicationFormField f) =>
                string.Equals(f.FieldKey, d.Key, StringComparison.OrdinalIgnoreCase);

            // Where keys compare case-sensitively a built-in can sit beside its custom twin; leave both.
            if (fields.Any(f => f.IsBuiltIn && Matches(f))) continue;
            var custom = fields.FirstOrDefault(f => !f.IsBuiltIn && Matches(f));
            if (custom == null) continue;

            custom.FieldKey = d.Key;
            custom.IsBuiltIn = true;
            custom.IsCore = d.Core;
            custom.FieldType = d.Type;
            custom.Section = d.Section;
            custom.Options = d.Options;
            custom.HelpText ??= d.HelpText;
            changes++;
        }
        return changes;
    }

    /// <summary>Spec §2 renamed these built-ins; a label the school chose is never overwritten.</summary>
    private static int RelabelOldDefaults(List<ApplicationFormField> fields)
    {
        var changes = 0;
        foreach (var (key, oldLabel) in RenamedLabels)
        {
            var field = fields.FirstOrDefault(f => f.IsBuiltIn && f.FieldKey == key && f.Label == oldLabel);
            if (field == null) continue;
            field.Label = DefaultApplicationFormFields.All.Single(d => d.Key == key).Label;
            changes++;
        }
        return changes;
    }

    private static string? ExtractLrn(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
            if (values == null) return null;
            foreach (var kv in values)
            {
                if (string.Equals(kv.Key, "lrn", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }
            return null;
        }
        catch (JsonException)
        {
            return null; // malformed legacy JSON is skipped, as everywhere else
        }
    }
}
