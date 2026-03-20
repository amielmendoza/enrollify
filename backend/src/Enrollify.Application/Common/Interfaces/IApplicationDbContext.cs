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
    DbSet<Fee> Fees { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Section> Sections { get; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<AdmissionApplication> AdmissionApplications { get; }
    DbSet<EnrollmentRequirement> EnrollmentRequirements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
