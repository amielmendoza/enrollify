namespace Enrollify.Application.Common;

/// <summary>
/// Canonical ordered grade-level list. Grade levels are free-text strings on the backend,
/// so every surface must agree on the exact spelling — this list must match the frontend's
/// GRADE_LEVELS in enrollify.client/src/app/core/constants.ts.
/// </summary>
public static class GradeLevels
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Kindergarten",
        "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5", "Grade 6",
        "Grade 7", "Grade 8", "Grade 9", "Grade 10", "Grade 11", "Grade 12",
    };

    /// <summary>
    /// The next grade up (Kindergarten → Grade 1, ..., Grade 11 → Grade 12).
    /// Grade 12 stays Grade 12; unrecognized input is returned unchanged.
    /// </summary>
    public static string Promote(string gradeLevel)
    {
        if (string.IsNullOrWhiteSpace(gradeLevel))
            return gradeLevel;

        var trimmed = gradeLevel.Trim();
        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i], trimmed, StringComparison.OrdinalIgnoreCase))
                return i == All.Count - 1 ? All[i] : All[i + 1];
        }

        return gradeLevel;
    }
}
