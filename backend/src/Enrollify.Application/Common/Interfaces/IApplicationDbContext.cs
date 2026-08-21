using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<EnrollmentStatusHistory> EnrollmentStatusHistories { get; }
    DbSet<EnrollmentFee> EnrollmentFees { get; }
    DbSet<LedgerAdjustment> LedgerAdjustments { get; }
    DbSet<Fee> Fees { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Section> Sections { get; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<AdmissionApplication> AdmissionApplications { get; }
    DbSet<EnrollmentRequirement> EnrollmentRequirements { get; }
    DbSet<SchoolYear> SchoolYears { get; }
    DbSet<FileDocument> FileDocuments { get; }
    DbSet<PaymentTerm> PaymentTerms { get; }
    DbSet<RequirementTemplate> RequirementTemplates { get; }
    DbSet<ApplicationFormField> ApplicationFormFields { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction (via the provider's
    /// execution strategy) and commits on success. Use for multi-entity mutations that must
    /// be all-or-nothing, e.g. application approval.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
