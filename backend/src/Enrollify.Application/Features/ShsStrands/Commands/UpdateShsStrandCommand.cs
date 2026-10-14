using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Commands;

/// <summary>
/// Renames, reorders, or (de)activates a strand. Existing enrollments keep the Track/Strand
/// strings they were saved with — this never rewrites history.
/// </summary>
public record UpdateShsStrandCommand(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder) : IRequest<ShsStrandDto>;

public class UpdateShsStrandCommandValidator : AbstractValidator<UpdateShsStrandCommand>
{
    public UpdateShsStrandCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Track).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Strand).NotEmpty().MaximumLength(100);
    }
}

public class UpdateShsStrandCommandHandler : IRequestHandler<UpdateShsStrandCommand, ShsStrandDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateShsStrandCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShsStrandDto> Handle(UpdateShsStrandCommand request, CancellationToken cancellationToken)
    {
        var row = await _context.ShsStrands.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Strand not found.");

        var track = request.Track.Trim();
        var strand = request.Strand.Trim();
        var tl = track.ToLower();
        var sl = strand.ToLower();
        if (await _context.ShsStrands.AnyAsync(s => s.Id != row.Id && s.Track.ToLower() == tl && s.Strand.ToLower() == sl, cancellationToken))
            throw new InvalidOperationException($"'{track} – {strand}' already exists.");

        row.Track = track;
        row.Strand = strand;
        row.IsActive = request.IsActive;
        row.DisplayOrder = request.DisplayOrder;
        await _context.SaveChangesAsync(cancellationToken);
        return new ShsStrandDto(row.Id, row.Track, row.Strand, row.IsActive, row.DisplayOrder);
    }
}
