using System.Text.Json;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ApplicationFormFields;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Idempotent per-tenant conversion for schools that collected the LRN through a CUSTOM form
/// field keyed "lrn" before it was built in: the field becomes the built-in one (label,
/// visibility and requiredness kept) and saved answers move into AdmissionApplications.Lrn.
/// A second run changes nothing. It never touches Students.LrnNeedsReview — flagging is the
/// AddLearnerRecord migration's one-time job, so restarts can't re-flag confirmed LRNs.
/// Caller saves.
/// </summary>
public static class LearnerRecordBackfill
{
    public static async Task<int> RunForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var changes = 0;

        var field = await context.ApplicationFormFields.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.FieldKey == "lrn", ct);
        if (field != null && !field.IsBuiltIn)
        {
            field.IsBuiltIn = true;
            field.IsCore = false;
            field.FieldType = "Text";
            field.Section = "Student";
            field.AppliesTo = "Both";
            field.Options = null;
            field.HelpText ??= DefaultApplicationFormFields.LrnHelpText;
            changes++;
        }

        var candidates = await context.AdmissionApplications.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Lrn == null && a.CustomFieldValues != null)
            .ToListAsync(ct);
        foreach (var app in candidates)
        {
            var lrn = ExtractLrn(app.CustomFieldValues);
            if (lrn == null || lrn.Length > 20) continue; // column width; shape is checked on later writes
            app.Lrn = lrn;
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
