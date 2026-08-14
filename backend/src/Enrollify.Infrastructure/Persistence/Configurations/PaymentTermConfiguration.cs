using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> builder)
    {
        builder.ToTable("PaymentTerms");

        builder.Property(p => p.SchoolYear).HasMaxLength(20).IsRequired();
        builder.Property(p => p.PlanType).HasMaxLength(20).IsRequired();
        builder.Property(p => p.DownPaymentPercent).HasPrecision(5, 2);
        builder.Property(p => p.InterestRatePercent).HasPrecision(5, 2);
        builder.Property(p => p.DiscountPercent).HasPrecision(5, 2);

        builder.HasIndex(p => new { p.TenantId, p.SchoolYear, p.PlanType }).IsUnique();
    }
}
