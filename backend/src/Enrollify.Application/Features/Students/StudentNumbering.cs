using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Auto-generated student numbers (LRNs): 12-digit values starting at 100100100001,
/// shared by CreateStudentCommandHandler and application approval so both mint from the
/// same sequence. Two concurrent requests can compute the same next number — the
/// (TenantId, LRN) unique index rejects the loser, so callers inserting an auto-generated
/// LRN must retry a couple of times, regenerating between attempts.
/// </summary>
public static class StudentNumbering
{
    public const long Seed = 100100100001L;
    private const int Digits = 12;

    public static async Task<string> NextLrnAsync(IApplicationDbContext context, CancellationToken ct)
    {
        // Only LRNs in the auto-generated shape (exactly 12 chars) can affect the sequence,
        // so instead of loading every LRN we scan from the string-max of that shape downward:
        // for equal-length digit strings lexicographic order equals numeric order, making the
        // first PARSEABLE value the numeric max (12-char manual alphabetic LRNs sort above
        // the digits and get skipped). Deliberately avoids SQL-Server-only LIKE [0-9] ranges —
        // this must run on any provider, including InMemory in tests.
        var topOfShape = await context.Students
            .Where(s => s.LRN.Length == Digits)
            .OrderByDescending(s => s.LRN)
            .Select(s => s.LRN)
            .Take(50)
            .ToListAsync(ct);

        var next = Seed;
        foreach (var lrn in topOfShape)
        {
            if (long.TryParse(lrn, out var value))
            {
                if (value >= next) next = value + 1;
                break;
            }
        }

        return next.ToString("D12");
    }
}
