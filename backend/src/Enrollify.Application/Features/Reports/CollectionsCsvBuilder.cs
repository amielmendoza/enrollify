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
                Escape(row.ReferenceNumber),
                Escape(row.StudentName),
                Escape(row.GradeLevel),
                Escape(row.SchoolYear),
                Escape(row.PaymentMethod),
                Escape(row.ReceivedBy),
                Escape(row.Amount.ToString("0.00", CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

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
