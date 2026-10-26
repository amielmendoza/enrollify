using System.Globalization;
using System.Text;

namespace Enrollify.Application.Features.Reports.DepEd;

/// <summary>
/// Renders the quick count as CSV: one row per grade level, then a Total row. Lives in Application
/// so escaping is unit-testable. Numbers (change columns can be negative) go through Escape, never
/// the apostrophe-prefixing EscapeText.
/// </summary>
public static class QuickCountCsvBuilder
{
    public const string Header = "Grade Level,Male,Female,Sex Not Set,Total,Male Change,Female Change,Sex Not Set Change,Total Change";

    public static string Build(QuickCountDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var row in report.Rows)
            sb.AppendLine(Line(row));
        sb.AppendLine(Line(report.Total));

        return sb.ToString();
    }

    private static string Line(QuickCountRowDto r) => string.Join(",",
        CollectionsCsvBuilder.EscapeText(r.GradeLevel),
        Num(r.Male), Num(r.Female), Num(r.NotSet), Num(r.Total),
        Num(r.MaleChange), Num(r.FemaleChange), Num(r.NotSetChange), Num(r.TotalChange));

    private static string Num(int value) =>
        CollectionsCsvBuilder.Escape(value.ToString(CultureInfo.InvariantCulture));
}
