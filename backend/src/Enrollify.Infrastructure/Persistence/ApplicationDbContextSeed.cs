using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync())
            return;

        var tenantId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Manila Science High School",
            Subdomain = "mshs",
            ContactEmail = "admin@mshs.edu.ph",
            ContactPhone = "+63-2-1234-5678",
            Address = "Taft Avenue, Manila, Philippines"
        };

        context.Tenants.Add(tenant);

        var admin = new User
        {
            Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
            TenantId = tenantId,
            Email = "admin@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            FirstName = "System",
            LastName = "Administrator",
            Role = UserRole.Admin
        };

        var registrar = new User
        {
            Id = Guid.Parse("c3d4e5f6-a7b8-9012-cdef-123456789012"),
            TenantId = tenantId,
            Email = "registrar@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Registrar123!"),
            FirstName = "Maria",
            LastName = "Santos",
            Role = UserRole.Registrar
        };

        context.Users.AddRange(admin, registrar);

        // Seed workflow
        var workflow = new WorkflowDefinition
        {
            Id = Guid.Parse("d4e5f6a7-b8c9-0123-defa-234567890123"),
            TenantId = tenantId,
            Name = "Standard Enrollment Workflow",
            Description = "Default enrollment workflow for K-12"
        };

        workflow.Steps.Add(new WorkflowStep
        {
            TenantId = tenantId,
            StepOrder = 1,
            StepName = "Submit Application",
            FromStatus = EnrollmentStatus.Draft,
            ToStatus = EnrollmentStatus.Submitted,
            RequiredRole = "Registrar",
            RequiresApproval = false
        });

        workflow.Steps.Add(new WorkflowStep
        {
            TenantId = tenantId,
            StepOrder = 2,
            StepName = "Assess Requirements",
            FromStatus = EnrollmentStatus.Submitted,
            ToStatus = EnrollmentStatus.Assessed,
            RequiredRole = "Registrar",
            RequiresApproval = true
        });

        workflow.Steps.Add(new WorkflowStep
        {
            TenantId = tenantId,
            StepOrder = 3,
            StepName = "Approve Enrollment",
            FromStatus = EnrollmentStatus.Assessed,
            ToStatus = EnrollmentStatus.Approved,
            RequiredRole = "Admin",
            RequiresApproval = true
        });

        workflow.Steps.Add(new WorkflowStep
        {
            TenantId = tenantId,
            StepOrder = 4,
            StepName = "Confirm Payment",
            FromStatus = EnrollmentStatus.Approved,
            ToStatus = EnrollmentStatus.Paid,
            RequiredRole = "Registrar",
            RequiresApproval = false
        });

        workflow.Steps.Add(new WorkflowStep
        {
            TenantId = tenantId,
            StepOrder = 5,
            StepName = "Finalize Enrollment",
            FromStatus = EnrollmentStatus.Paid,
            ToStatus = EnrollmentStatus.Enrolled,
            RequiredRole = "Registrar",
            RequiresApproval = false
        });

        context.WorkflowDefinitions.Add(workflow);

        // Seed fees
        var fees = new List<Fee>
        {
            new() { TenantId = tenantId, Name = "Tuition Fee", Description = "Annual tuition fee", Amount = 15000m, SchoolYear = "2024-2025", GradeLevel = "Grade 7" },
            new() { TenantId = tenantId, Name = "Miscellaneous Fee", Description = "Lab, library, and other fees", Amount = 3000m, SchoolYear = "2024-2025", GradeLevel = "Grade 7" },
            new() { TenantId = tenantId, Name = "Registration Fee", Description = "One-time registration fee", Amount = 500m, SchoolYear = "2024-2025", GradeLevel = "Grade 7" },
        };

        context.Fees.AddRange(fees);

        // Seed sections
        var sections = new List<Section>
        {
            new() { TenantId = tenantId, Name = "Section A - Einstein", GradeLevel = "Grade 7", SchoolYear = "2024-2025", Capacity = 40, Adviser = "Mr. Juan Dela Cruz" },
            new() { TenantId = tenantId, Name = "Section B - Newton", GradeLevel = "Grade 7", SchoolYear = "2024-2025", Capacity = 40, Adviser = "Ms. Ana Reyes" },
            new() { TenantId = tenantId, Name = "Section C - Curie", GradeLevel = "Grade 7", SchoolYear = "2024-2025", Capacity = 40, Adviser = "Mrs. Liza Cruz" },
        };

        context.Sections.AddRange(sections);

        // Seed sample students
        var students = new List<Student>
        {
            new() { TenantId = tenantId, LRN = "100100100001", FirstName = "Juan", MiddleName = "Reyes", LastName = "Dela Cruz", BirthDate = new DateTime(2012, 5, 15), Gender = "Male", Address = "123 Rizal St, Manila", ContactNumber = "+63-912-345-6789", GuardianName = "Pedro Dela Cruz", GuardianContact = "+63-912-345-6780" },
            new() { TenantId = tenantId, LRN = "100100100002", FirstName = "Maria", MiddleName = "Santos", LastName = "Garcia", BirthDate = new DateTime(2012, 8, 22), Gender = "Female", Address = "456 Mabini St, Manila", ContactNumber = "+63-912-345-6790", GuardianName = "Rosa Garcia", GuardianContact = "+63-912-345-6791" },
            new() { TenantId = tenantId, LRN = "100100100003", FirstName = "Jose", MiddleName = "Luna", LastName = "Reyes", BirthDate = new DateTime(2012, 3, 10), Gender = "Male", Address = "789 Bonifacio Ave, Manila", ContactNumber = "+63-912-345-6792", GuardianName = "Carlos Reyes", GuardianContact = "+63-912-345-6793" },
        };

        // Create student user accounts
        var studentUser1 = new User
        {
            TenantId = tenantId,
            Email = "juan.delacruz@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student123!"),
            FirstName = "Juan",
            LastName = "Dela Cruz",
            Role = UserRole.Student
        };
        var studentUser2 = new User
        {
            TenantId = tenantId,
            Email = "maria.garcia@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student123!"),
            FirstName = "Maria",
            LastName = "Garcia",
            Role = UserRole.Student
        };
        var studentUser3 = new User
        {
            TenantId = tenantId,
            Email = "jose.reyes@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student123!"),
            FirstName = "Jose",
            LastName = "Reyes",
            Role = UserRole.Student
        };

        context.Users.AddRange(studentUser1, studentUser2, studentUser3);

        // Link students to their user accounts
        students[0].UserId = studentUser1.Id;
        students[1].UserId = studentUser2.Id;
        students[2].UserId = studentUser3.Id;

        context.Students.AddRange(students);

        await context.SaveChangesAsync();
    }
}
