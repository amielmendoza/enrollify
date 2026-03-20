using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Common;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EnrollmentStatusHistory> EnrollmentStatusHistories => Set<EnrollmentStatusHistory>();
    public DbSet<Fee> Fees => Set<Fee>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();
    public DbSet<EnrollmentRequirement> EnrollmentRequirements => Set<EnrollmentRequirement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filter for multi-tenancy
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(CreateTenantFilter(entityType.ClrType));
            }
        }
    }

    private System.Linq.Expressions.LambdaExpression CreateTenantFilter(Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var tenantIdProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(TenantEntity.TenantId));
        var tenantIdValue = System.Linq.Expressions.Expression.Property(
            System.Linq.Expressions.Expression.Constant(this), nameof(CurrentTenantId));
        var body = System.Linq.Expressions.Expression.Equal(tenantIdProperty, tenantIdValue);
        return System.Linq.Expressions.Expression.Lambda(body, parameter);
    }

    public Guid CurrentTenantId => _tenantProvider.GetTenantId();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.TenantId = tenantId;
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
