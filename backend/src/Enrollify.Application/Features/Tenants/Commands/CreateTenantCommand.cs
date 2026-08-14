using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Application.Features.Workflows;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Commands;

public record CreateTenantCommand(
    string Name, string Subdomain,
    string? ContactEmail, string? ContactPhone, string? Address
) : IRequest<TenantDto>;

public class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subdomain).NotEmpty().MaximumLength(60)
            .Matches("^[a-z0-9][a-z0-9-]{0,58}[a-z0-9]$|^[a-z0-9]$")
            .WithMessage("Subdomain must be lowercase letters, digits, and hyphens (no leading/trailing hyphens).");
        RuleFor(x => x.Subdomain)
            .Must(s => !ReservedSubdomains.IsReserved(s))
            .WithMessage(s => $"'{s.Subdomain?.Trim().ToLowerInvariant()}' is a reserved word and can't be used as a subdomain.");
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrEmpty(x.ContactEmail));
    }
}

/// <summary>
/// Centralized list of subdomain values that conflict with the SPA's top-level routes.
/// Schools can't use these because /:slug/apply would shadow (or be shadowed by) the
/// matching /login, /apply, /tenants, etc. route.
/// </summary>
internal static class ReservedSubdomains
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "apply", "tenants", "schools", "dashboard",
        "students", "enrollments", "admissions", "settings",
        "my-enrollment", "my-payments", "my-profile",
        "parent", "super", "api", "auth", "registrars", "print"
    };

    public static bool IsReserved(string? subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return false;
        return Reserved.Contains(subdomain.Trim().ToLowerInvariant());
    }
}

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _context;
    public CreateTenantCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var sub = request.Subdomain.Trim().ToLowerInvariant();
        var conflict = await _context.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Subdomain == sub, cancellationToken);
        if (conflict)
            throw new InvalidOperationException($"A school with subdomain '{sub}' already exists.");

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Subdomain = sub,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = true
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        // The startup seeder seeds default school years / payment terms / requirement templates /
        // application form fields for any tenant missing them on the next boot, so nothing extra
        // is needed here — but we also seed inline so the new tenant is usable immediately.
        await SeedDefaultsForTenantAsync(_context, tenant, cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.Subdomain, tenant.ContactEmail,
            tenant.ContactPhone, tenant.Address, tenant.IsActive, tenant.CreatedAt);
    }

    /// <summary>
    /// Seeds the per-tenant defaults a brand-new school needs to be operational:
    /// active school year placeholder, payment terms, requirement templates, and the
    /// configurable built-in application-form fields. Idempotent.
    /// </summary>
    private static async Task SeedDefaultsForTenantAsync(IApplicationDbContext ctx, Tenant tenant, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var nextSyName = $"{year}-{year + 1}";
        ctx.SchoolYears.Add(new SchoolYear
        {
            TenantId = tenant.Id,
            Name = nextSyName,
            StartDate = new DateTime(year, 6, 1),
            EndDate = new DateTime(year + 1, 3, 31),
            IsActive = true
        });

        foreach (var plan in new[] { ("Full", 0m, 0m, 5m, 1), ("Monthly", 20m, 5m, 0m, 9), ("Quarterly", 30m, 3m, 0m, 3) })
        {
            ctx.PaymentTerms.Add(new PaymentTerm
            {
                TenantId = tenant.Id,
                SchoolYear = nextSyName,
                PlanType = plan.Item1,
                DownPaymentPercent = plan.Item2,
                InterestRatePercent = plan.Item3,
                DiscountPercent = plan.Item4,
                InstallmentCount = plan.Item5
            });
        }

        var defaultRequirements = new[] { "PSA Birth Certificate", "Form 138 (Report Card)", "Good Moral Certificate", "2x2 ID Photo" };
        for (var i = 0; i < defaultRequirements.Length; i++)
        {
            ctx.RequirementTemplates.Add(new RequirementTemplate
            {
                TenantId = tenant.Id,
                DocumentName = defaultRequirements[i],
                IsActive = true,
                DisplayOrder = i + 1
            });
        }

        ctx.WorkflowDefinitions.Add(DefaultWorkflow.Build(tenant.Id));

        await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(ctx, tenant.Id, ct);
        await ctx.SaveChangesAsync(ct);
    }
}
