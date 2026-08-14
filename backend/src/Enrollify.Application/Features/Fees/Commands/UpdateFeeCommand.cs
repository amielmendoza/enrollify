using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Fees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Fees.Commands;

public record UpdateFeeCommand(
    Guid Id, string Name, string? Description, decimal Amount, string SchoolYear, string GradeLevel, bool IsActive
) : IRequest<FeeDto>;

public class UpdateFeeCommandValidator : AbstractValidator<UpdateFeeCommand>
{
    public UpdateFeeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.SchoolYear).NotEmpty();
        RuleFor(x => x.GradeLevel).NotEmpty();
    }
}

public class UpdateFeeCommandHandler : IRequestHandler<UpdateFeeCommand, FeeDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateFeeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FeeDto> Handle(UpdateFeeCommand request, CancellationToken cancellationToken)
    {
        var fee = await _context.Fees
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Fee not found.");

        fee.Name = request.Name;
        fee.Description = request.Description;
        fee.Amount = request.Amount;
        fee.SchoolYear = request.SchoolYear;
        fee.GradeLevel = request.GradeLevel;
        fee.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new FeeDto(fee.Id, fee.Name, fee.Description, fee.Amount, fee.SchoolYear, fee.GradeLevel, fee.IsActive);
    }
}
