using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Application.Features.Workflows;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Infrastructure.Persistence;

public static class ApplicationDbContextSeed
{
    // The two known demo tenants. Demo-only seeders (e.g. invented fee amounts) are
    // hard-scoped to these IDs so real tenants never receive fabricated data.
    private static readonly Guid[] DemoTenantIds =
    {
        Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), // Manila Science High School (mshs)
        Guid.Parse("b2c3d4e5-f6a7-8901-cdef-234567890123"), // Quezon City Science High School (qcshs)
    };

    // Must exactly match the frontend's canonical list in enrollify.client/src/app/core/constants.ts
    // (grade levels are free-text strings on the backend, so every surface must agree on spelling).
    // Local copy: Application/Common/GradeLevels.cs did not exist when this was written — switch to
    // the shared helper once it lands.
    private static readonly string[] GradeLevels =
    {
        "Kindergarten",
        "Grade 1", "Grade 2", "Grade 3", "Grade 4", "Grade 5", "Grade 6",
        "Grade 7", "Grade 8", "Grade 9", "Grade 10", "Grade 11", "Grade 12",
    };

    // Standard demo fee structure applied to every grade level x school year of the demo tenants.
    private static readonly (string Name, string Description, decimal Amount)[] StandardDemoFees =
    {
        ("Tuition Fee", "Annual tuition fee", 15000m),
        ("Miscellaneous Fee", "Lab, library, and other fees", 3000m),
        ("Registration Fee", "One-time registration fee", 500m),
    };

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Order matters: tenants are created first, then every per-tenant seeder below backfills
        // defaults for any tenant missing them, so all of these are idempotent per tenant.
        // SeedDemoFeesAsync must run after SeedSchoolYearsAsync — it seeds per school year.
        await SeedSuperAdminAsync(context);
        await SeedSecondTenantAsync(context);
        await SeedSchoolYearsAsync(context);
        await SeedPaymentTermsAsync(context);
        await SeedDemoFeesAsync(context);
        await SeedRequirementTemplatesAsync(context);
        await SeedDefaultWorkflowsAsync(context);
        await SeedApplicationFormFieldsAsync(context);

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

        // Seed school years
        var schoolYears = new List<SchoolYear>
        {
            new() { TenantId = tenantId, Name = "2024-2025", StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 3, 31), IsActive = true },
            new() { TenantId = tenantId, Name = "2025-2026", StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = false },
        };
        context.SchoolYears.AddRange(schoolYears);

        // Seed fees: standard demo fee structure for every grade level x school year, so demo
        // students in any grade can be assessed (previously Grade 7 only, which tripped the
        // zero-fee assessment guard for everyone else).
        foreach (var syName in new[] { "2024-2025", "2025-2026" })
        {
            foreach (var grade in GradeLevels)
            {
                foreach (var (name, description, amount) in StandardDemoFees)
                {
                    context.Fees.Add(new Fee
                    {
                        TenantId = tenantId,
                        Name = name,
                        Description = description,
                        Amount = amount,
                        SchoolYear = syName,
                        GradeLevel = grade
                    });
                }
            }
        }

        // Seed payment terms
        foreach (var syName in new[] { "2024-2025", "2025-2026" })
        {
            context.PaymentTerms.AddRange(
                new PaymentTerm { TenantId = tenantId, SchoolYear = syName, PlanType = "Full", DownPaymentPercent = 0, InterestRatePercent = 0, DiscountPercent = 5, InstallmentCount = 1 },
                new PaymentTerm { TenantId = tenantId, SchoolYear = syName, PlanType = "Monthly", DownPaymentPercent = 20, InterestRatePercent = 5, DiscountPercent = 0, InstallmentCount = 9 },
                new PaymentTerm { TenantId = tenantId, SchoolYear = syName, PlanType = "Quarterly", DownPaymentPercent = 30, InterestRatePercent = 3, DiscountPercent = 0, InstallmentCount = 3 }
            );
        }

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

        // Sample logins demonstrating both flows:
        // - Pedro (Parent) owns Juan + Jose (siblings, demonstrates multi-child parent flow)
        // - Maria (Student) self-registered (demonstrates student self-enrollment flow)
        var parent1 = new User
        {
            TenantId = tenantId,
            Email = "pedro.delacruz@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Parent123!"),
            FirstName = "Pedro",
            LastName = "Dela Cruz",
            Role = UserRole.Parent
        };
        var studentUser = new User
        {
            TenantId = tenantId,
            Email = "maria.garcia@mshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student123!"),
            FirstName = "Maria",
            LastName = "Garcia",
            Role = UserRole.Student
        };

        context.Users.AddRange(parent1, studentUser);

        students[0].ParentUserId = parent1.Id;   // Juan -> Pedro
        students[1].UserId = studentUser.Id;     // Maria self-registered
        students[2].ParentUserId = parent1.Id;   // Jose -> Pedro

        context.Students.AddRange(students);

        await context.SaveChangesAsync();
    }

    private static async Task SeedSchoolYearsAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        if (tenantIds.Count == 0) return;

        var tenantsWithYears = await context.SchoolYears.IgnoreQueryFilters()
            .Select(sy => sy.TenantId).Distinct().ToListAsync();

        var added = false;
        foreach (var tenantId in tenantIds.Except(tenantsWithYears))
        {
            context.SchoolYears.AddRange(
                new SchoolYear { TenantId = tenantId, Name = "2024-2025", StartDate = new DateTime(2024, 6, 1), EndDate = new DateTime(2025, 3, 31), IsActive = true },
                new SchoolYear { TenantId = tenantId, Name = "2025-2026", StartDate = new DateTime(2025, 6, 1), EndDate = new DateTime(2026, 3, 31), IsActive = false });
            added = true;
        }

        if (added) await context.SaveChangesAsync();
    }

    private static async Task SeedRequirementTemplatesAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        if (tenantIds.Count == 0) return;

        var tenantsWithTemplates = await context.RequirementTemplates.IgnoreQueryFilters()
            .Select(rt => rt.TenantId).Distinct().ToListAsync();

        var defaults = new[]
        {
            "PSA Birth Certificate",
            "Form 138 (Report Card)",
            "Good Moral Certificate",
            "2x2 ID Photo"
        };

        var added = false;
        foreach (var tenantId in tenantIds.Except(tenantsWithTemplates))
        {
            for (var i = 0; i < defaults.Length; i++)
            {
                context.RequirementTemplates.Add(new RequirementTemplate
                {
                    TenantId = tenantId,
                    DocumentName = defaults[i],
                    GradeLevel = null,
                    IsActive = true,
                    DisplayOrder = i + 1
                });
            }
            added = true;
        }

        if (added) await context.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent. Every tenant needs an active WorkflowDefinition for status transitions to be
    /// configurable (MoveEnrollmentStepCommand also has a hardcoded fallback, but seeding one
    /// makes it visible and editable in Settings > Workflows).
    /// </summary>
    private static async Task SeedDefaultWorkflowsAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        if (tenantIds.Count == 0) return;

        var tenantsWithActiveWorkflow = await context.WorkflowDefinitions.IgnoreQueryFilters()
            .Where(w => w.IsActive)
            .Select(w => w.TenantId).Distinct().ToListAsync();

        var added = false;
        foreach (var tenantId in tenantIds.Except(tenantsWithActiveWorkflow))
        {
            context.WorkflowDefinitions.Add(DefaultWorkflow.Build(tenantId));
            added = true;
        }

        if (added) await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seed configurable built-in fields per tenant. Idempotent: any tenant that already has
    /// a row for a given (TenantId, FieldKey) is skipped, so this can re-run safely on each boot
    /// to backfill new built-ins added in future releases.
    /// </summary>
    private static async Task SeedApplicationFormFieldsAsync(ApplicationDbContext context)
    {
        var tenants = await context.Tenants.IgnoreQueryFilters().ToListAsync();
        if (tenants.Count == 0) return;

        // Delegated to the shared DefaultApplicationFormFields helper so the boot seed,
        // the per-tenant create flow, and the admin "Restore Defaults" action all stay in sync.
        var anyAdded = false;
        foreach (var tenant in tenants)
        {
            var added = await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(context, tenant.Id);
            if (added > 0) anyAdded = true;
        }

        if (anyAdded) await context.SaveChangesAsync();
    }

    // ----- Below: the legacy in-file defaults are no longer used; kept commented for reference. -----
    /*
    private static async Task LegacySeedApplicationFormFieldsAsync(ApplicationDbContext context)
    {
        var tenants = await context.Tenants.IgnoreQueryFilters().ToListAsync();
        if (tenants.Count == 0) return;

        var defaults = new (string Key, string Label, string Type, string Section, string AppliesTo, bool Required, bool Visible, bool Core, int Order, string? Options)[]
        {
            // Parent section — core fields (locked) shown first, then configurable
            ("parentFirstName",     "Parent First Name",         "Text",     "Parent",     "ParentMode", true,  true, true,  1,  null),
            ("parentLastName",      "Parent Last Name",          "Text",     "Parent",     "ParentMode", true,  true, true,  2,  null),
            ("parentEmail",         "Parent Email",              "Text",     "Parent",     "ParentMode", true,  true, true,  3,  null),
            ("parentContactNumber", "Parent Contact",            "Text",     "Parent",     "ParentMode", false, true, false, 10, null),
            ("parentRelationship",  "Relationship to children",  "Dropdown", "Parent",     "ParentMode", true,  true, false, 20, "[\"Mother\",\"Father\",\"Guardian\",\"Other\"]"),
            // Student / child section
            ("firstName",           "First Name",                "Text",     "Student",    "Both",       true,  true, true,  1,  null),
            ("lastName",            "Last Name",                 "Text",     "Student",    "Both",       true,  true, true,  2,  null),
            ("dateOfBirth",         "Date of Birth",             "Date",     "Student",    "Both",       true,  true, true,  3,  null),
            ("gender",              "Gender",                    "Dropdown", "Student",    "Both",       true,  true, true,  4,  "[\"Male\",\"Female\"]"),
            ("email",               "Email",                     "Text",     "Student",    "Both",       false, true, true,  5,  null),
            ("middleName",          "Middle Name",               "Text",     "Student",    "Both",       false, true, false, 10, null),
            ("contactNumber",       "Contact Number",            "Text",     "Student",    "Both",       false, true, false, 20, null),
            ("address",             "Address",                   "Text",     "Student",    "Both",       false, true, false, 30, null),
            // Enrollment section
            ("gradeLevel",            "Grade Level",             "Text",     "Enrollment", "Both",       true,  true, true,  1,  null),
            ("schoolYear",            "School Year",             "Text",     "Enrollment", "Both",       true,  true, true,  2,  null),
            ("previousSchool",        "Previous School",         "Text",     "Enrollment", "Both",       false, true, false, 10, null),
            ("previousSchoolAddress", "Previous School Address", "Text",     "Enrollment", "Both",       false, true, false, 20, null),
            // Guardian section (only relevant in Student mode; Parent mode auto-populates from parent)
            ("guardianName",         "Guardian Name",            "Text",     "Guardian",   "StudentMode", false, true, false, 10, null),
            ("guardianContact",      "Guardian Contact",         "Text",     "Guardian",   "StudentMode", false, true, false, 20, null),
            ("guardianRelationship", "Relationship",             "Dropdown", "Guardian",   "StudentMode", false, true, false, 30, "[\"Mother\",\"Father\",\"Guardian\",\"Other\"]"),
        };

        foreach (var tenant in tenants)
        {
            var existingKeys = await context.ApplicationFormFields.IgnoreQueryFilters()
                .Where(f => f.TenantId == tenant.Id)
                .Select(f => f.FieldKey)
                .ToListAsync();

            foreach (var d in defaults)
            {
                if (existingKeys.Contains(d.Key)) continue;
                context.ApplicationFormFields.Add(new ApplicationFormField
                {
                    TenantId = tenant.Id,
                    FieldKey = d.Key,
                    Label = d.Label,
                    FieldType = d.Type,
                    Section = d.Section,
                    AppliesTo = d.AppliesTo,
                    IsRequired = d.Required,
                    IsVisible = d.Visible,
                    IsBuiltIn = true,
                    IsCore = d.Core,
                    DisplayOrder = d.Order,
                    Options = d.Options
                });
            }
        }

        await context.SaveChangesAsync();
    }
    */

    /// <summary>
    /// Idempotent. Creates a global SuperAdmin login if one doesn't exist yet.
    /// The SuperAdmin's TenantId is technically tied to the first tenant for FK purposes,
    /// but their cross-tenant power comes from the role check on /api/tenants endpoints.
    /// </summary>
    private static async Task SeedSuperAdminAsync(ApplicationDbContext context)
    {
        const string email = "super@enrollify.app";
        var exists = await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        if (exists) return;

        var anyTenant = await context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync();
        if (anyTenant == null) return;  // First tenant hasn't been seeded yet — handled in main seed below

        context.Users.Add(new User
        {
            TenantId = anyTenant.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin123!"),
            FirstName = "Platform",
            LastName = "Operator",
            Role = UserRole.SuperAdmin,
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent. Adds a second tenant + a sample admin so the multi-school flow is testable.
    /// Per-tenant defaults (school years, payment terms, requirements, application form fields)
    /// are seeded by the existing seeders below on the next boot.
    /// </summary>
    private static async Task SeedSecondTenantAsync(ApplicationDbContext context)
    {
        var secondId = Guid.Parse("b2c3d4e5-f6a7-8901-cdef-234567890123");
        var exists = await context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == secondId);
        if (exists) return;

        // Don't seed the second tenant until the first one exists.
        var firstExists = await context.Tenants.IgnoreQueryFilters().AnyAsync();
        if (!firstExists) return;

        var tenant = new Tenant
        {
            Id = secondId,
            Name = "Quezon City Science High School",
            Subdomain = "qcshs",
            ContactEmail = "admin@qcshs.edu.ph",
            ContactPhone = "+63-2-9876-5432",
            Address = "Quezon City, Philippines"
        };
        context.Tenants.Add(tenant);

        context.Users.Add(new User
        {
            TenantId = secondId,
            Email = "admin@qcshs.edu.ph",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            FirstName = "QCSHS",
            LastName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    // Idempotent per (tenant, school year, plan type) — not per tenant. A tenant can end up
    // with a partial set (e.g. Monthly saved via Settings but Full never created; there is no
    // delete for payment terms, so a gap is always an accident, not intent) and the parent
    // payment-plan picker only offers plans that have a term row. Existing rows, including
    // admin-edited percentages, are never touched.
    private static async Task SeedPaymentTermsAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .Select(t => t.Id).ToListAsync();
        if (tenantIds.Count == 0) return;

        var existing = await context.PaymentTerms.IgnoreQueryFilters()
            .Select(pt => new { pt.TenantId, pt.SchoolYear, pt.PlanType })
            .ToListAsync();
        var existingKeys = existing
            .Select(e => (e.TenantId, e.SchoolYear, e.PlanType))
            .ToHashSet();

        var defaults = new[]
        {
            (PlanType: "Full", Down: 0m, Interest: 0m, Disc: 5m, Count: 1),
            (PlanType: "Monthly", Down: 20m, Interest: 5m, Disc: 0m, Count: 9),
            (PlanType: "Quarterly", Down: 30m, Interest: 3m, Disc: 0m, Count: 3),
        };

        var added = false;
        foreach (var tenantId in tenantIds)
        {
            var schoolYearNames = await context.SchoolYears.IgnoreQueryFilters()
                .Where(sy => sy.TenantId == tenantId)
                .Select(sy => sy.Name).ToListAsync();

            foreach (var syName in schoolYearNames)
            {
                foreach (var d in defaults)
                {
                    if (existingKeys.Contains((tenantId, syName, d.PlanType))) continue;
                    context.PaymentTerms.Add(new PaymentTerm
                    {
                        TenantId = tenantId,
                        SchoolYear = syName,
                        PlanType = d.PlanType,
                        DownPaymentPercent = d.Down,
                        InterestRatePercent = d.Interest,
                        DiscountPercent = d.Disc,
                        InstallmentCount = d.Count
                    });
                    added = true;
                }
            }
        }

        if (added) await context.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent per-boot backfill: gives the DEMO tenants the standard fee structure for
    /// every grade level x every school year, so demo students in any grade can be assessed
    /// (fees used to exist for Grade 7 only). Hard-scoped to <see cref="DemoTenantIds"/> —
    /// real tenants must never receive invented fee amounts. Per tenant + school year + grade,
    /// a fee is skipped when one with the same Name already exists, so re-boots don't duplicate
    /// and pre-existing rows (e.g. the original Grade 7 fees, or admin-edited amounts) are
    /// preserved untouched.
    /// </summary>
    private static async Task SeedDemoFeesAsync(ApplicationDbContext context)
    {
        var demoTenantIds = await context.Tenants.IgnoreQueryFilters()
            .Where(t => DemoTenantIds.Contains(t.Id))
            .Select(t => t.Id).ToListAsync();
        if (demoTenantIds.Count == 0) return;

        var added = false;
        foreach (var tenantId in demoTenantIds)
        {
            var schoolYearNames = await context.SchoolYears.IgnoreQueryFilters()
                .Where(sy => sy.TenantId == tenantId)
                .Select(sy => sy.Name).ToListAsync();
            if (schoolYearNames.Count == 0) continue;

            var existingRows = await context.Fees.IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId)
                .Select(f => new { f.SchoolYear, f.GradeLevel, f.Name })
                .ToListAsync();
            var existingKeys = existingRows
                .Select(f => (f.SchoolYear, f.GradeLevel, f.Name))
                .ToHashSet();

            foreach (var syName in schoolYearNames)
            {
                foreach (var grade in GradeLevels)
                {
                    foreach (var (name, description, amount) in StandardDemoFees)
                    {
                        if (existingKeys.Contains((syName, grade, name))) continue;

                        context.Fees.Add(new Fee
                        {
                            TenantId = tenantId,
                            Name = name,
                            Description = description,
                            Amount = amount,
                            SchoolYear = syName,
                            GradeLevel = grade,
                            IsActive = true
                        });
                        added = true;
                    }
                }
            }
        }

        if (added) await context.SaveChangesAsync();
    }
}
