namespace Enrollify.Application.Common;

/// <summary>
/// DepEd vocabularies for the learner record. Mirrored in the frontend's
/// enrollify.client/src/app/core/constants.ts — keep spellings identical.
/// </summary>
public static class DepEdLists
{
    public static readonly IReadOnlyList<string> LearnerTypes = new[] { "New", "Continuing", "Transferee", "Returning" };

    /// <summary>BEEF "learner with disability" categories. None contain commas (stored comma-joined).</summary>
    public static readonly IReadOnlyList<string> DisabilityTypes = new[]
    {
        "Visual Impairment", "Hearing Impairment", "Learning Disability", "Intellectual Disability",
        "Autism Spectrum Disorder", "Emotional-Behavioral Disorder", "Orthopedic/Physical Handicap",
        "Speech/Language Disorder", "Cerebral Palsy", "Special Health Problem/Chronic Disease",
        "Multiple Disorder", "Cancer",
    };

    public static readonly IReadOnlyList<string> ExtensionNameSuggestions = new[] { "Jr.", "Sr.", "II", "III", "IV" };

    public static bool IsShsGrade(string? gradeLevel)
    {
        var g = gradeLevel?.Trim();
        return string.Equals(g, "Grade 11", StringComparison.OrdinalIgnoreCase)
            || string.Equals(g, "Grade 12", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTwelveDigitLrn(string? value) =>
        value != null && value.Length == 12 && value.All(char.IsAsciiDigit);
}
