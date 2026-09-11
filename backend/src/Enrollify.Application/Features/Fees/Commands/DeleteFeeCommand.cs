using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Fees.Commands;

public record DeleteFeeCommand(Guid Id) : IRequest;

public class DeleteFeeCommandHandler : IRequestHandler<DeleteFeeCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteFeeCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task Handle(DeleteFeeCommand request, CancellationToken cancellationToken)
    {
        // FirstOrDefaultAsync (not FindAsync) so the tenant query filter applies.
        var fee = await _context.Fees.FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Fee not found.");

        _context.Fees.Remove(fee);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
