using System.Globalization;
using System.Text;
using Enrollify.Application.Features.Reports.Queries;

namespace Enrollify.Application.Features.Reports;

/// <summary>
/// Renders collections journal rows as CSV. Lives in Application (not the controller)
/// so escaping is unit-testable.
/// </summary>
public static class CollectionsCsvBuilder
{
    public const string Header = "Date,Reference No,Student,Grade Level,School Year,Method,Received By,Amount";

    public static string Build(IEnumerable<CollectionRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(row.PaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                EscapeText(row.ReferenceNumber),
                EscapeText(row.StudentName),
                EscapeText(row.GradeLevel),
                EscapeText(row.SchoolYear),
                EscapeText(row.PaymentMethod),
                EscapeText(row.ReceivedBy),
                Escape(row.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escape for free-text cells (names, references, labels): also neutralizes spreadsheet
    /// formula injection — a value starting with = + - @ (or tab/CR) gets a leading apostrophe
    /// so Excel/Sheets display it as text instead of evaluating it. Never use for money or
    /// number cells: negative amounts are legitimate.
    /// </summary>
    public static string EscapeText(string? value) =>
        Escape(!string.IsNullOrEmpty(value) && "=+-@\t\r".Contains(value[0]) ? "'" + value : value);

    /// <summary>RFC 4180 escaping: quote fields containing commas, quotes, or newlines; double embedded quotes.</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
