using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Commands;

public record CreateShsStrandCommand(string Track, string Strand, int DisplayOrder) : IRequest<ShsStrandDto>;

public class CreateShsStrandCommandValidator : AbstractValidator<CreateShsStrandCommand>
{
    public CreateShsStrandCommandValidator()
    {
        RuleFor(x => x.Track).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Strand).NotEmpty().MaximumLength(100);
    }
}

public class CreateShsStrandCommandHandler : IRequestHandler<CreateShsStrandCommand, ShsStrandDto>
{
    private readonly IApplicationDbContext _context;

    public CreateShsStrandCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShsStrandDto> Handle(CreateShsStrandCommand request, CancellationToken cancellationToken)
    {
        var track = request.Track.Trim();
        var strand = request.Strand.Trim();
        var tl = track.ToLower();
        var sl = strand.ToLower();
        if (await _context.ShsStrands.AnyAsync(s => s.Track.ToLower() == tl && s.Strand.ToLower() == sl, cancellationToken))
            throw new InvalidOperationException($"'{track} – {strand}' already exists. Reactivate it instead of adding it again.");

        var row = new ShsStrand { Track = track, Strand = strand, IsActive = true, DisplayOrder = request.DisplayOrder };
        _context.ShsStrands.Add(row);
        await _context.SaveChangesAsync(cancellationToken);
        return new ShsStrandDto(row.Id, row.Track, row.Strand, row.IsActive, row.DisplayOrder);
    }
}
