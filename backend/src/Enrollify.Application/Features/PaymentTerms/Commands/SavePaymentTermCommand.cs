using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.PaymentTerms;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.PaymentTerms.Commands;

public record SavePaymentTermCommand(
    string SchoolYear, string PlanType,
    decimal DownPaymentPercent, decimal InterestRatePercent,
    decimal DiscountPercent, int InstallmentCount) : IRequest<PaymentTermDto>;

public class SavePaymentTermCommandHandler : IRequestHandler<SavePaymentTermCommand, PaymentTermDto>
{
    private readonly IApplicationDbContext _context;

    public SavePaymentTermCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaymentTermDto> Handle(SavePaymentTermCommand request, CancellationToken cancellationToken)
    {
        var validTypes = new[] { "Full", "Monthly", "Quarterly" };
        if (!validTypes.Contains(request.PlanType))
            throw new ArgumentException("PlanType must be Full, Monthly, or Quarterly.");

        var existing = await _context.PaymentTerms
            .FirstOrDefaultAsync(p => p.SchoolYear == request.SchoolYear && p.PlanType == request.PlanType, cancellationToken);

        if (existing != null)
        {
            existing.DownPaymentPercent = request.DownPaymentPercent;
            existing.InterestRatePercent = request.InterestRatePercent;
            existing.DiscountPercent = request.DiscountPercent;
            existing.InstallmentCount = request.InstallmentCount;
        }
        else
        {
            existing = new PaymentTerm
            {
                SchoolYear = request.SchoolYear,
                PlanType = request.PlanType,
                DownPaymentPercent = request.DownPaymentPercent,
                InterestRatePercent = request.InterestRatePercent,
                DiscountPercent = request.DiscountPercent,
                InstallmentCount = request.InstallmentCount
            };
            _context.PaymentTerms.Add(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentTermDto(existing.Id, existing.SchoolYear, existing.PlanType,
            existing.DownPaymentPercent, existing.InterestRatePercent,
            existing.DiscountPercent, existing.InstallmentCount, existing.IsActive);
    }
}
