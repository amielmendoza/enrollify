using Enrollify.Application.Common.Interfaces;
using MediatR;

namespace Enrollify.Application.Features.Reports.Queries;

/// <summary>
/// Unpaged enrollment register rows for the CSV export — same filter, ordering, and
/// projection as GetEnrollmentRegisterQuery via EnrollmentRegisterReport.
/// </summary>
public record GetEnrollmentRegisterExportQuery(EnrollmentRegisterFilter Filter) : IRequest<List<EnrollmentRegisterRowDto>>;

public class GetEnrollmentRegisterExportQueryHandler : IRequestHandler<GetEnrollmentRegisterExportQuery, List<EnrollmentRegisterRowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentRegisterExportQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<EnrollmentRegisterRowDto>> Handle(GetEnrollmentRegisterExportQuery request, CancellationToken cancellationToken)
    {
        var schoolYear = await EnrollmentRegisterReport.ResolveSchoolYearAsync(_context, request.Filter.SchoolYear, cancellationToken);
        var filtered = EnrollmentRegisterReport.Filter(_context, request.Filter, schoolYear);

        return await EnrollmentRegisterReport.ProjectAsync(
            _context, EnrollmentRegisterReport.Order(filtered), schoolYear, cancellationToken);
    }
}
