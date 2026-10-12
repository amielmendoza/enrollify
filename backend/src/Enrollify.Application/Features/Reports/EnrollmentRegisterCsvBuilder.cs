using System.Globalization;
using System.Text;
using Enrollify.Application.Features.Reports.Queries;

namespace Enrollify.Application.Features.Reports;

/// <summary>
/// Renders enrollment register rows as CSV (RFC 4180 via CollectionsCsvBuilder.Escape).
/// Lives in Application (not the controller) so escaping and null handling are unit-testable.
/// Null money (pre-assessment AssessedTotal/Balance) renders as an empty field, never "0.00".
/// </summary>
public static class EnrollmentRegisterCsvBuilder
{
    public const string Header = "LRN,Student,Grade Level,Section,Status,Payment Plan,Assessed Total,Total Paid,Balance,Created";

    public static string Build(IEnumerable<EnrollmentRegisterRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                CollectionsCsvBuilder.EscapeText(row.Lrn),
                CollectionsCsvBuilder.EscapeText(row.StudentName),
                CollectionsCsvBuilder.EscapeText(row.GradeLevel),
                CollectionsCsvBuilder.EscapeText(row.SectionName),
                CollectionsCsvBuilder.EscapeText(row.Status),
                CollectionsCsvBuilder.EscapeText(row.PaymentPlan),
                CollectionsCsvBuilder.Escape(Money(row.AssessedTotal)),
                CollectionsCsvBuilder.Escape(Money(row.TotalPaid)),
                CollectionsCsvBuilder.Escape(Money(row.Balance)),
                // Stored in UTC; printed as the school's calendar day (Asia/Manila, UTC+8, no DST).
                CollectionsCsvBuilder.Escape(row.CreatedAt.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    private static string Money(decimal? value) =>
        value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
}
