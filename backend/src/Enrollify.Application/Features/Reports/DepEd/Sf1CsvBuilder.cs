using System.Globalization;
using System.Text;

namespace Enrollify.Application.Features.Reports.DepEd;

/// <summary>
/// Renders the SF1 working copy as CSV: the same columns, in the same order and the learner order of the
/// on-screen report. Text goes through the formula-guarding EscapeText; the age number through Escape.
/// </summary>
public static class Sf1CsvBuilder
{
    public const string Header =
        "LRN,Name,Sex,Birth Date,Age,Mother Tongue,IP Group,Religion,House No./Street,Barangay,City/Municipality,Province," +
        "Father's Name,Mother's Maiden Name,Guardian,Relationship,Contact Number,Remarks";

    public static string Build(Sf1Dto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var row in report.Learners)
            sb.AppendLine(Line(row));

        return sb.ToString();
    }

    private static string Line(Sf1RowDto r) => string.Join(",",
        Text(r.Lrn), Text(r.Name), Text(r.Sex),
        Text(r.BirthDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)),
        CollectionsCsvBuilder.Escape(r.Age?.ToString(CultureInfo.InvariantCulture)),
        Text(r.MotherTongue), Text(r.IpGroup), Text(r.Religion),
        Text(r.HouseStreet), Text(r.Barangay), Text(r.CityMunicipality), Text(r.Province),
        Text(r.FatherName), Text(r.MotherMaidenName), Text(r.GuardianName), Text(r.GuardianRelationship),
        Text(r.Contact), Text(r.Remarks));

    private static string Text(string? value) => CollectionsCsvBuilder.EscapeText(value);
}
