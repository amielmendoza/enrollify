using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolProfile;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolProfile.Commands;

/// <summary>
/// Updates the DepEd identity fields printed in the SF1 / LIS report headers. The tenant
/// Name is deliberately not editable here (SuperAdmin owns it). Blank or whitespace values
/// are stored as null; the validation rules apply to the trimmed value. Tenant is not a
/// TenantEntity, so the caller passes the current tenant id (from the JWT) and the handler
/// loads that row by primary key.
/// </summary>
public record UpdateSchoolProfileCommand(
    Guid TenantId,
    string? DepEdSchoolId,
    string? DepEdRegionCode,
    string? DepEdRegionName,
    string? DepEdDivision,
    string? DepEdDistrict,
    string? SchoolHeadName,
    string UpdatedBy) : IRequest<SchoolProfileDto>;

public class UpdateSchoolProfileCommandValidator : AbstractValidator<UpdateSchoolProfileCommand>
{
    private const int MaxText = 150;

    public UpdateSchoolProfileCommandValidator()
    {
        RuleFor(x => Clean(x.DepEdSchoolId))
            .Matches(@"^\d{6}$").When(x => Clean(x.DepEdSchoolId) != null)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdSchoolId))
            .WithMessage("DepEd School ID must be exactly 6 digits.");

        RuleFor(x => Clean(x.DepEdRegionCode))
            .Matches(@"^\d{2}0{8}$").When(x => Clean(x.DepEdRegionCode) != null)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdRegionCode))
            .WithMessage("Region code must be 10 digits: a 2-digit region followed by eight zeros.");

        RuleFor(x => Clean(x.DepEdRegionName))
            .NotNull().When(x => Clean(x.DepEdRegionCode) != null)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdRegionName))
            .WithMessage("Region name is required when a region code is set.");
        RuleFor(x => Clean(x.DepEdRegionName))
            .MaximumLength(MaxText)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdRegionName));

        RuleFor(x => Clean(x.DepEdDivision)).MaximumLength(MaxText)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdDivision));
        RuleFor(x => Clean(x.DepEdDistrict)).MaximumLength(MaxText)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.DepEdDistrict));
        RuleFor(x => Clean(x.SchoolHeadName)).MaximumLength(MaxText)
            .OverridePropertyName(nameof(UpdateSchoolProfileCommand.SchoolHeadName));
    }

    /// <summary>Trims; blank or whitespace becomes null.</summary>
    internal static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class UpdateSchoolProfileCommandHandler : IRequestHandler<UpdateSchoolProfileCommand, SchoolProfileDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateSchoolProfileCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<SchoolProfileDto> Handle(UpdateSchoolProfileCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("School not found.");

        tenant.DepEdSchoolId = UpdateSchoolProfileCommandValidator.Clean(request.DepEdSchoolId);
        tenant.DepEdRegionCode = UpdateSchoolProfileCommandValidator.Clean(request.DepEdRegionCode);
        tenant.DepEdRegionName = UpdateSchoolProfileCommandValidator.Clean(request.DepEdRegionName);
        tenant.DepEdDivision = UpdateSchoolProfileCommandValidator.Clean(request.DepEdDivision);
        tenant.DepEdDistrict = UpdateSchoolProfileCommandValidator.Clean(request.DepEdDistrict);
        tenant.SchoolHeadName = UpdateSchoolProfileCommandValidator.Clean(request.SchoolHeadName);
        tenant.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);

        return new SchoolProfileDto(tenant.Name, tenant.DepEdSchoolId, tenant.DepEdRegionCode, tenant.DepEdRegionName,
            tenant.DepEdDivision, tenant.DepEdDistrict, tenant.SchoolHeadName);
    }
}
