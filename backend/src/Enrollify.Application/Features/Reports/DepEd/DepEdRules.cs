using System.Globalization;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;

namespace Enrollify.Application.Features.Reports.DepEd;

/// <summary>
/// Every rule the three DepEd reports (LIS worklist, quick count, SF1) share, written once.
/// All helpers are pure: "now" and every date arrive as parameters, never from the clock.
/// "Manila day" = the Asia/Manila calendar date, a fixed UTC+8 (the Philippines has no DST),
/// mirroring <c>StudentNumbering</c>.
/// </summary>
public static class DepEdRules
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8); // PH has no DST

    private const string DateFormat = "MM/dd/yyyy";

    // ----- Manila day -----

    /// <summary>The Manila calendar date (midnight, Kind unspecified) for a UTC instant.</summary>
    public static DateTime ManilaDate(DateTime utcNow) => (utcNow + ManilaOffset).Date;

    /// <summary>
    /// The UTC instant at which Manila day <paramref name="manilaDate"/> ends (exclusive):
    /// the next midnight in Manila, i.e. 16:00 UTC of that date.
    /// </summary>
    public static DateTime ManilaEndOfDayUtc(DateTime manilaDate) =>
        DateTime.SpecifyKind(manilaDate.Date.AddDays(1) - ManilaOffset, DateTimeKind.Utc);

    // ----- Status -----

    private static bool IsEnrolledSet(EnrollmentStatus status) =>
        status is EnrollmentStatus.Approved or EnrollmentStatus.Paid or EnrollmentStatus.Enrolled;

    /// <summary>
    /// The enrollment's status on Manila day <paramref name="manilaDay"/>: the ToStatus of the last
    /// history row (by TransitionDate) strictly before the end of that day. With history rows but
    /// none that early, null (no status yet). With no history rows at all, the current Status
    /// applies from CreatedAt onward (null before it).
    /// </summary>
    public static EnrollmentStatus? StatusOn(Enrollment e, IReadOnlyList<EnrollmentStatusHistory> history, DateTime manilaDay)
    {
        var endUtc = ManilaEndOfDayUtc(manilaDay);

        if (history.Count == 0)
            return e.CreatedAt < endUtc ? e.Status : null;

        var last = history
            .Where(h => h.TransitionDate < endUtc)
            .OrderBy(h => h.TransitionDate) // stable: ties keep input order
            .LastOrDefault();

        return last?.ToStatus;
    }

    /// <summary>
    /// Counted on a day: status that day is Approved/Paid/Enrolled and the learner is not withdrawn
    /// that day (WithdrawalDate null or later than the day). Withdrawn on X counts on X-1, not X.
    /// </summary>
    public static bool CountedOn(Enrollment e, IReadOnlyList<EnrollmentStatusHistory> history, DateTime manilaDay)
    {
        var status = StatusOn(e, history, manilaDay);
        if (status is not { } s || !IsEnrolledSet(s))
            return false;

        return e.WithdrawalDate is not { } withdrawn || withdrawn.Date > manilaDay.Date;
    }

    /// <summary>Listed on the worklist and SF1: current Status is Approved/Paid/Enrolled.</summary>
    public static bool Listed(Enrollment e) => IsEnrolledSet(e.Status);

    // ----- Learner facts -----

    /// <summary>"Male", "Female" or "NotSet" from free-text gender (trimmed, case-insensitive).</summary>
    public static string Sex(string? gender)
    {
        var g = gender?.Trim();
        if (string.Equals(g, "male", StringComparison.OrdinalIgnoreCase) || string.Equals(g, "m", StringComparison.OrdinalIgnoreCase))
            return "Male";
        if (string.Equals(g, "female", StringComparison.OrdinalIgnoreCase) || string.Equals(g, "f", StringComparison.OrdinalIgnoreCase))
            return "Female";
        return "NotSet";
    }

    /// <summary>Start year of the school year: the row's StartDate.Year, else the first 4 chars of the name.</summary>
    public static int StartYear(string schoolYearName, SchoolYear? row)
    {
        if (row != null)
            return row.StartDate.Year;

        var name = schoolYearName?.Trim() ?? string.Empty;
        if (name.Length >= 4 && int.TryParse(name.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year))
            return year;

        throw new FormatException($"School year name '{schoolYearName}' does not start with a 4-digit year.");
    }

    /// <summary>
    /// Whole years from the birth date to the first Friday of June of <paramref name="startYear"/>.
    /// A birthday counts on its own day. No birth date (null, or an unset default) gives null.
    /// </summary>
    public static int? AgeOn(DateTime? birthDate, int startYear)
    {
        if (birthDate is not { } b || b == default)
            return null;

        var reference = new DateTime(startYear, 6, 1);
        while (reference.DayOfWeek != DayOfWeek.Friday)
            reference = reference.AddDays(1);

        var age = reference.Year - b.Year;
        if (b.Month > reference.Month || (b.Month == reference.Month && b.Day > reference.Day))
            age--;
        return age;
    }

    // ----- Names and contact -----

    private static string Join(params string?[] parts) =>
        string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>SF1 learner name: "Last, First, Ext, Middle", skipping blank parts.</summary>
    public static string LearnerName(string last, string first, string? ext, string? middle) =>
        Join(last, first, ext, middle);

    /// <summary>Parent name: "Last, First, Middle", skipping blank parts.</summary>
    public static string ParentName(string? last, string? first, string? middle) =>
        Join(last, first, middle);

    public static string FatherName(Student s) =>
        ParentName(s.FatherLastName, s.FatherFirstName, s.FatherMiddleName);

    /// <summary>The mother's name uses her maiden-name fields.</summary>
    public static string MotherMaidenName(Student s) =>
        ParentName(s.MotherMaidenLastName, s.MotherMaidenFirstName, s.MotherMaidenMiddleName);

    /// <summary>First non-blank of guardian, mother, father contact; otherwise null.</summary>
    public static string? Contact(string? guardian, string? mother, string? father)
    {
        foreach (var c in new[] { guardian, mother, father })
            if (!string.IsNullOrWhiteSpace(c))
                return c.Trim();
        return null;
    }

    // ----- Attendance and remarks -----

    /// <summary>
    /// The school year's start date: the row's StartDate, or (no row) 1 June of the start year taken
    /// from the name.
    /// </summary>
    private static DateTime SchoolYearStart(SchoolYear? row, string schoolYearName) =>
        row != null ? row.StartDate.Date : new DateTime(StartYear(schoolYearName, null), 6, 1);

    /// <summary>FirstAttendanceDate if recorded, else the school year's start date (date part).</summary>
    public static DateTime EffectiveFirstAttendance(Enrollment e, SchoolYear? row, string schoolYearName) =>
        (e.FirstAttendanceDate ?? SchoolYearStart(row, schoolYearName)).Date;

    private static string Fmt(DateTime d) => d.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string WithDate(string code, DateTime? date) =>
        date is { } d ? $"{code} {Fmt(d)}" : code;

    /// <summary>
    /// SF1 remark codes in spec order, joined with "; ": T/I, B/A, LE, CCT, LWD, DRP, T/O.
    /// LE is raised only when first attendance is set and later than the school year start.
    /// </summary>
    public static string Remarks(Enrollment e, Student s, SchoolYear? row, string schoolYearName)
    {
        var codes = new List<string>();

        var learnerType = e.LearnerType?.Trim();
        if (string.Equals(learnerType, "Transferee", StringComparison.OrdinalIgnoreCase))
            codes.Add("T/I");
        else if (string.Equals(learnerType, "Returning", StringComparison.OrdinalIgnoreCase))
            codes.Add("B/A");

        if (e.FirstAttendanceDate is { } first && first.Date > SchoolYearStart(row, schoolYearName))
            codes.Add($"LE {Fmt(first)}");

        if (s.Is4PsBeneficiary)
            codes.Add("CCT");

        if (s.HasDisability)
            codes.Add("LWD");

        var withdrawal = e.WithdrawalType?.Trim();
        if (string.Equals(withdrawal, "Dropped", StringComparison.OrdinalIgnoreCase))
        {
            codes.Add(WithDate("DRP", e.WithdrawalDate));
        }
        else if (string.Equals(withdrawal, "TransferredOut", StringComparison.OrdinalIgnoreCase))
        {
            var code = WithDate("T/O", e.WithdrawalDate);
            var school = e.TransferredToSchool?.Trim();
            codes.Add(string.IsNullOrEmpty(school) ? code : $"{code} {school}");
        }

        return string.Join("; ", codes);
    }
}
