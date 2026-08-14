using Enrollify.Application.Common.Interfaces;
using MediatR;

namespace Enrollify.Application.Features.Reports.Queries;

/// <summary>
/// Unpaged collections rows for the CSV export — same filter and projection as
/// GetCollectionsQuery via CollectionsReport, in journal (PaymentDate ascending) order.
/// </summary>
public record GetCollectionsExportQuery(DateTime From, DateTime To, string? Method) : IRequest<List<CollectionRowDto>>;

public class GetCollectionsExportQueryHandler : IRequestHandler<GetCollectionsExportQuery, List<CollectionRowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCollectionsExportQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<CollectionRowDto>> Handle(GetCollectionsExportQuery request, CancellationToken cancellationToken)
    {
        var filtered = CollectionsReport.Filter(_context, request.From, request.To, request.Method);
        // Same deterministic tie-break as the paged query so CSV order matches the pages.
        return await CollectionsReport.ProjectAsync(
            filtered.OrderBy(p => p.PaymentDate).ThenBy(p => p.Id), cancellationToken);
    }
}
