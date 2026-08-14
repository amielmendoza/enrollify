using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.PaymentMethod).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
        builder.Property(p => p.Remarks).HasMaxLength(500);
        builder.Property(p => p.ReceiptFileName).HasMaxLength(260);
        builder.Property(p => p.ReceiptFileUrl).HasMaxLength(500);
        builder.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(p => p.ReviewedBy).HasMaxLength(200);
        builder.Property(p => p.ReviewNotes).HasMaxLength(500);

        builder.HasOne(p => p.Enrollment)
            .WithMany(e => e.Payments)
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.EnrollmentId);
    }
}
