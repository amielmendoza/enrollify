using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Fees;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Enrollify.Application.Features.Fees.Commands;

public record CreateFeeCommand(
    string Name, string? Description, decimal Amount, string SchoolYear, string GradeLevel
) : IRequest<FeeDto>;

public class CreateFeeCommandValidator : AbstractValidator<CreateFeeCommand>
{
    public CreateFeeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.SchoolYear).NotEmpty();
        RuleFor(x => x.GradeLevel).NotEmpty();
    }
}

public class CreateFeeCommandHandler : IRequestHandler<CreateFeeCommand, FeeDto>
{
    private readonly IApplicationDbContext _context;

    public CreateFeeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FeeDto> Handle(CreateFeeCommand request, CancellationToken cancellationToken)
    {
        var fee = new Fee
        {
            Name = request.Name,
            Description = request.Description,
            Amount = request.Amount,
            SchoolYear = request.SchoolYear,
            GradeLevel = request.GradeLevel
        };

        _context.Fees.Add(fee);
        await _context.SaveChangesAsync(cancellationToken);

        return new FeeDto(fee.Id, fee.Name, fee.Description, fee.Amount, fee.SchoolYear, fee.GradeLevel, fee.IsActive);
    }
}
