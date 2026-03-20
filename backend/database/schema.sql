-- =============================================================================
-- Enrollify Multi-Tenant K-12 Enrollment System
-- SQL Server Database Schema
-- =============================================================================
-- Description : Creates all tables, constraints, indexes, and seed data
--               for the Enrollify multi-tenant enrollment platform.
-- Target      : SQL Server 2019+
-- Generated   : 2026-03-19
-- =============================================================================

USE [Enrollify];
GO

-- =============================================================================
-- 1. TENANTS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Tenants]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [Name]          NVARCHAR(200)    NOT NULL,
        [Subdomain]     NVARCHAR(100)    NOT NULL,
        [ContactEmail]  NVARCHAR(256)    NULL,
        [ContactPhone]  NVARCHAR(50)     NULL,
        [Address]       NVARCHAR(500)    NULL,
        [IsActive]      BIT              NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Tenants] PRIMARY KEY CLUSTERED ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Subdomain]
        ON [dbo].[Tenants] ([Subdomain]);

    CREATE NONCLUSTERED INDEX [IX_Tenants_IsActive]
        ON [dbo].[Tenants] ([IsActive]);

    PRINT 'Created table [Tenants]';
END
GO

-- =============================================================================
-- 2. USERS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]      UNIQUEIDENTIFIER NOT NULL,
        [Email]         NVARCHAR(256)    NOT NULL,
        [PasswordHash]  NVARCHAR(500)    NOT NULL,
        [FirstName]     NVARCHAR(100)    NOT NULL,
        [LastName]      NVARCHAR(100)    NOT NULL,
        [Role]          NVARCHAR(50)     NOT NULL,
        [IsActive]      BIT              NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Users_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_TenantId_Email]
        ON [dbo].[Users] ([TenantId], [Email]);

    CREATE NONCLUSTERED INDEX [IX_Users_TenantId]
        ON [dbo].[Users] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Users_Role]
        ON [dbo].[Users] ([Role]);

    PRINT 'Created table [Users]';
END
GO

-- =============================================================================
-- 3. STUDENTS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Students]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Students]
    (
        [Id]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]         UNIQUEIDENTIFIER NOT NULL,
        [LRN]              NVARCHAR(20)     NOT NULL,
        [FirstName]        NVARCHAR(100)    NOT NULL,
        [MiddleName]       NVARCHAR(100)    NULL,
        [LastName]         NVARCHAR(100)    NOT NULL,
        [BirthDate]        DATE             NOT NULL,
        [Gender]           NVARCHAR(20)     NOT NULL,
        [Address]          NVARCHAR(500)    NULL,
        [ContactNumber]    NVARCHAR(50)     NULL,
        [Email]            NVARCHAR(256)    NULL,
        [GuardianName]     NVARCHAR(200)    NULL,
        [GuardianContact]  NVARCHAR(50)     NULL,
        [IsActive]         BIT              NOT NULL DEFAULT 1,
        [CreatedAt]        DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]        NVARCHAR(256)    NULL,
        [UpdatedAt]        DATETIME2(7)     NULL,
        [UpdatedBy]        NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Students] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Students_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Students_TenantId_LRN]
        ON [dbo].[Students] ([TenantId], [LRN]);

    CREATE NONCLUSTERED INDEX [IX_Students_TenantId]
        ON [dbo].[Students] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Students_LastName_FirstName]
        ON [dbo].[Students] ([LastName], [FirstName]);

    CREATE NONCLUSTERED INDEX [IX_Students_TenantId_IsActive]
        ON [dbo].[Students] ([TenantId], [IsActive]);

    PRINT 'Created table [Students]';
END
GO

-- =============================================================================
-- 4. SECTIONS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Sections]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Sections]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]      UNIQUEIDENTIFIER NOT NULL,
        [Name]          NVARCHAR(100)    NOT NULL,
        [GradeLevel]    NVARCHAR(50)     NOT NULL,
        [SchoolYear]    NVARCHAR(20)     NOT NULL,
        [Capacity]      INT              NOT NULL DEFAULT 40,
        [Adviser]       NVARCHAR(200)    NULL,
        [IsActive]      BIT              NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Sections] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Sections_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_Sections_TenantId]
        ON [dbo].[Sections] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Sections_TenantId_GradeLevel_SchoolYear]
        ON [dbo].[Sections] ([TenantId], [GradeLevel], [SchoolYear]);

    CREATE NONCLUSTERED INDEX [IX_Sections_TenantId_IsActive]
        ON [dbo].[Sections] ([TenantId], [IsActive]);

    PRINT 'Created table [Sections]';
END
GO

-- =============================================================================
-- 5. ENROLLMENTS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Enrollments]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Enrollments]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]      UNIQUEIDENTIFIER NOT NULL,
        [StudentId]     UNIQUEIDENTIFIER NOT NULL,
        [SchoolYear]    NVARCHAR(20)     NOT NULL,
        [GradeLevel]    NVARCHAR(50)     NOT NULL,
        [SectionId]     UNIQUEIDENTIFIER NULL,
        [Status]        NVARCHAR(50)     NOT NULL DEFAULT N'Draft',
        [Remarks]       NVARCHAR(1000)   NULL,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Enrollments] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Enrollments_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_Enrollments_Students] FOREIGN KEY ([StudentId])
            REFERENCES [dbo].[Students] ([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_Enrollments_Sections] FOREIGN KEY ([SectionId])
            REFERENCES [dbo].[Sections] ([Id])
            ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX [IX_Enrollments_TenantId_StudentId_SchoolYear]
        ON [dbo].[Enrollments] ([TenantId], [StudentId], [SchoolYear]);

    CREATE NONCLUSTERED INDEX [IX_Enrollments_TenantId]
        ON [dbo].[Enrollments] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Enrollments_StudentId]
        ON [dbo].[Enrollments] ([StudentId]);

    CREATE NONCLUSTERED INDEX [IX_Enrollments_SectionId]
        ON [dbo].[Enrollments] ([SectionId]);

    CREATE NONCLUSTERED INDEX [IX_Enrollments_TenantId_Status]
        ON [dbo].[Enrollments] ([TenantId], [Status]);

    CREATE NONCLUSTERED INDEX [IX_Enrollments_TenantId_SchoolYear_GradeLevel]
        ON [dbo].[Enrollments] ([TenantId], [SchoolYear], [GradeLevel]);

    PRINT 'Created table [Enrollments]';
END
GO

-- =============================================================================
-- 6. ENROLLMENT STATUS HISTORIES
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EnrollmentStatusHistories]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[EnrollmentStatusHistories]
    (
        [Id]              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]        UNIQUEIDENTIFIER NOT NULL,
        [EnrollmentId]    UNIQUEIDENTIFIER NOT NULL,
        [FromStatus]      NVARCHAR(50)     NULL,
        [ToStatus]        NVARCHAR(50)     NOT NULL,
        [Remarks]         NVARCHAR(1000)   NULL,
        [TransitionDate]  DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedAt]       DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]       NVARCHAR(256)    NULL,
        [UpdatedAt]       DATETIME2(7)     NULL,
        [UpdatedBy]       NVARCHAR(256)    NULL,

        CONSTRAINT [PK_EnrollmentStatusHistories] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_EnrollmentStatusHistories_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_EnrollmentStatusHistories_Enrollments] FOREIGN KEY ([EnrollmentId])
            REFERENCES [dbo].[Enrollments] ([Id])
            ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_EnrollmentStatusHistories_TenantId]
        ON [dbo].[EnrollmentStatusHistories] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_EnrollmentStatusHistories_EnrollmentId]
        ON [dbo].[EnrollmentStatusHistories] ([EnrollmentId]);

    CREATE NONCLUSTERED INDEX [IX_EnrollmentStatusHistories_TransitionDate]
        ON [dbo].[EnrollmentStatusHistories] ([TransitionDate]);

    PRINT 'Created table [EnrollmentStatusHistories]';
END
GO

-- =============================================================================
-- 7. FEES
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Fees]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Fees]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]      UNIQUEIDENTIFIER NOT NULL,
        [Name]          NVARCHAR(200)    NOT NULL,
        [Description]   NVARCHAR(500)    NULL,
        [Amount]        DECIMAL(18, 2)   NOT NULL,
        [SchoolYear]    NVARCHAR(20)     NOT NULL,
        [GradeLevel]    NVARCHAR(50)     NOT NULL,
        [IsActive]      BIT              NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Fees] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Fees_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_Fees_TenantId]
        ON [dbo].[Fees] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Fees_TenantId_SchoolYear_GradeLevel]
        ON [dbo].[Fees] ([TenantId], [SchoolYear], [GradeLevel]);

    CREATE NONCLUSTERED INDEX [IX_Fees_TenantId_IsActive]
        ON [dbo].[Fees] ([TenantId], [IsActive]);

    PRINT 'Created table [Fees]';
END
GO

-- =============================================================================
-- 8. PAYMENTS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Payments]
    (
        [Id]               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]         UNIQUEIDENTIFIER NOT NULL,
        [EnrollmentId]     UNIQUEIDENTIFIER NOT NULL,
        [Amount]           DECIMAL(18, 2)   NOT NULL,
        [PaymentMethod]    NVARCHAR(50)     NOT NULL,
        [ReferenceNumber]  NVARCHAR(100)    NULL,
        [Remarks]          NVARCHAR(1000)   NULL,
        [PaymentDate]      DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedAt]        DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]        NVARCHAR(256)    NULL,
        [UpdatedAt]        DATETIME2(7)     NULL,
        [UpdatedBy]        NVARCHAR(256)    NULL,

        CONSTRAINT [PK_Payments] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_Payments_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION,

        -- RESTRICT: Prevent deletion of an Enrollment that has associated Payments
        CONSTRAINT [FK_Payments_Enrollments] FOREIGN KEY ([EnrollmentId])
            REFERENCES [dbo].[Enrollments] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_Payments_TenantId]
        ON [dbo].[Payments] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_Payments_EnrollmentId]
        ON [dbo].[Payments] ([EnrollmentId]);

    CREATE NONCLUSTERED INDEX [IX_Payments_TenantId_PaymentDate]
        ON [dbo].[Payments] ([TenantId], [PaymentDate]);

    CREATE NONCLUSTERED INDEX [IX_Payments_ReferenceNumber]
        ON [dbo].[Payments] ([ReferenceNumber])
        WHERE [ReferenceNumber] IS NOT NULL;

    PRINT 'Created table [Payments]';
END
GO

-- =============================================================================
-- 9. WORKFLOW DEFINITIONS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorkflowDefinitions]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[WorkflowDefinitions]
    (
        [Id]            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]      UNIQUEIDENTIFIER NOT NULL,
        [Name]          NVARCHAR(200)    NOT NULL,
        [Description]   NVARCHAR(500)    NULL,
        [IsActive]      BIT              NOT NULL DEFAULT 1,
        [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]     NVARCHAR(256)    NULL,
        [UpdatedAt]     DATETIME2(7)     NULL,
        [UpdatedBy]     NVARCHAR(256)    NULL,

        CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_WorkflowDefinitions_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_WorkflowDefinitions_TenantId]
        ON [dbo].[WorkflowDefinitions] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_WorkflowDefinitions_TenantId_IsActive]
        ON [dbo].[WorkflowDefinitions] ([TenantId], [IsActive]);

    PRINT 'Created table [WorkflowDefinitions]';
END
GO

-- =============================================================================
-- 10. WORKFLOW STEPS
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorkflowSteps]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[WorkflowSteps]
    (
        [Id]                     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [TenantId]               UNIQUEIDENTIFIER NOT NULL,
        [WorkflowDefinitionId]   UNIQUEIDENTIFIER NOT NULL,
        [StepOrder]              INT              NOT NULL,
        [StepName]               NVARCHAR(200)    NOT NULL,
        [FromStatus]             NVARCHAR(50)     NOT NULL,
        [ToStatus]               NVARCHAR(50)     NOT NULL,
        [RequiredRole]           NVARCHAR(50)     NULL,
        [RequiresApproval]       BIT              NOT NULL DEFAULT 0,
        [CreatedAt]              DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]              NVARCHAR(256)    NULL,
        [UpdatedAt]              DATETIME2(7)     NULL,
        [UpdatedBy]              NVARCHAR(256)    NULL,

        CONSTRAINT [PK_WorkflowSteps] PRIMARY KEY CLUSTERED ([Id]),

        CONSTRAINT [FK_WorkflowSteps_Tenants] FOREIGN KEY ([TenantId])
            REFERENCES [dbo].[Tenants] ([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_WorkflowSteps_WorkflowDefinitions] FOREIGN KEY ([WorkflowDefinitionId])
            REFERENCES [dbo].[WorkflowDefinitions] ([Id])
            ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_WorkflowSteps_TenantId]
        ON [dbo].[WorkflowSteps] ([TenantId]);

    CREATE NONCLUSTERED INDEX [IX_WorkflowSteps_WorkflowDefinitionId]
        ON [dbo].[WorkflowSteps] ([WorkflowDefinitionId]);

    CREATE NONCLUSTERED INDEX [IX_WorkflowSteps_WorkflowDefinitionId_StepOrder]
        ON [dbo].[WorkflowSteps] ([WorkflowDefinitionId], [StepOrder]);

    PRINT 'Created table [WorkflowSteps]';
END
GO


-- =============================================================================
-- =============================================================================
--                           SEED DATA
-- =============================================================================
-- =============================================================================
-- NOTE: Password hashes below are placeholders. In production, hashes should
-- be generated by the application layer using BCrypt (or the configured
-- hashing algorithm). The C# application also contains its own seeding logic
-- that will generate proper hashes at startup.
-- =============================================================================

-- Fixed GUIDs for seed data (deterministic for idempotent seeding)
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @AdminUserId     UNIQUEIDENTIFIER = 'B2C3D4E5-F6A7-8901-BCDE-F12345678901';
DECLARE @RegistrarUserId UNIQUEIDENTIFIER = 'C3D4E5F6-A7B8-9012-CDEF-123456789012';
DECLARE @WorkflowId      UNIQUEIDENTIFIER = 'D4E5F6A7-B8C9-0123-DEFA-234567890123';

-- Section GUIDs
DECLARE @Section1Id      UNIQUEIDENTIFIER = 'E5F6A7B8-C9D0-1234-EFAB-345678901234';
DECLARE @Section2Id      UNIQUEIDENTIFIER = 'F6A7B8C9-D0E1-2345-FABC-456789012345';
DECLARE @Section3Id      UNIQUEIDENTIFIER = 'A7B8C9D0-E1F2-3456-ABCD-567890123456';

-- Student GUIDs
DECLARE @Student1Id      UNIQUEIDENTIFIER = 'B8C9D0E1-F2A3-4567-BCDE-678901234567';
DECLARE @Student2Id      UNIQUEIDENTIFIER = 'C9D0E1F2-A3B4-5678-CDEF-789012345678';
DECLARE @Student3Id      UNIQUEIDENTIFIER = 'D0E1F2A3-B4C5-6789-DEFA-890123456789';

-- Fee GUIDs
DECLARE @Fee1Id          UNIQUEIDENTIFIER = 'E1F2A3B4-C5D6-7890-EFAB-901234567890';
DECLARE @Fee2Id          UNIQUEIDENTIFIER = 'F2A3B4C5-D6E7-8901-FABC-012345678901';
DECLARE @Fee3Id          UNIQUEIDENTIFIER = 'A3B4C5D6-E7F8-9012-ABCD-123456789012';

-- Workflow Step GUIDs
DECLARE @Step1Id         UNIQUEIDENTIFIER = 'B4C5D6E7-F8A9-0123-BCDE-234567890123';
DECLARE @Step2Id         UNIQUEIDENTIFIER = 'C5D6E7F8-A9B0-1234-CDEF-345678901234';
DECLARE @Step3Id         UNIQUEIDENTIFIER = 'D6E7F8A9-B0C1-2345-DEFA-456789012345';
DECLARE @Step4Id         UNIQUEIDENTIFIER = 'E7F8A9B0-C1D2-3456-EFAB-567890123456';
DECLARE @Step5Id         UNIQUEIDENTIFIER = 'F8A9B0C1-D2E3-4567-FABC-678901234567';

DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SchoolYear      NVARCHAR(20)     = N'2025-2026';
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';

-- =============================================================================
-- SEED: Demo Tenant
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Tenants] WHERE [Id] = @TenantId)
BEGIN
    INSERT INTO [dbo].[Tenants]
        ([Id], [Name], [Subdomain], [ContactEmail], [ContactPhone], [Address], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@TenantId,
         N'Manila Science High School',
         N'mshs',
         N'admin@mshs.edu.ph',
         N'+63-2-8123-4567',
         N'Taft Avenue, Ermita, Manila, Philippines',
         1,
         @Now,
         @SeededBy);

    PRINT 'Seeded demo tenant: Manila Science High School';
END
GO

-- =============================================================================
-- SEED: Users (Admin & Registrar)
-- =============================================================================
-- NOTE: The PasswordHash values below are NOT real BCrypt hashes. They are
-- placeholders. The application seeds proper hashed passwords via C# code.
-- Admin password intended: "Admin123!"
-- Registrar password intended: "Registrar123!"
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @AdminUserId     UNIQUEIDENTIFIER = 'B2C3D4E5-F6A7-8901-BCDE-F12345678901';
DECLARE @RegistrarUserId UNIQUEIDENTIFIER = 'C3D4E5F6-A7B8-9012-CDEF-123456789012';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @AdminUserId)
BEGIN
    INSERT INTO [dbo].[Users]
        ([Id], [TenantId], [Email], [PasswordHash], [FirstName], [LastName], [Role], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@AdminUserId,
         @TenantId,
         N'admin@mshs.edu.ph',
         N'PLACEHOLDER_HASH_Admin123!_generate_via_app',
         N'System',
         N'Administrator',
         N'Admin',
         1,
         @Now,
         @SeededBy);

    PRINT 'Seeded admin user: admin@mshs.edu.ph';
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @RegistrarUserId)
BEGIN
    INSERT INTO [dbo].[Users]
        ([Id], [TenantId], [Email], [PasswordHash], [FirstName], [LastName], [Role], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@RegistrarUserId,
         @TenantId,
         N'registrar@mshs.edu.ph',
         N'PLACEHOLDER_HASH_Registrar123!_generate_via_app',
         N'School',
         N'Registrar',
         N'Registrar',
         1,
         @Now,
         @SeededBy);

    PRINT 'Seeded registrar user: registrar@mshs.edu.ph';
END
GO

-- =============================================================================
-- SEED: Sections (3 sections for Grade 7)
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @Section1Id      UNIQUEIDENTIFIER = 'E5F6A7B8-C9D0-1234-EFAB-345678901234';
DECLARE @Section2Id      UNIQUEIDENTIFIER = 'F6A7B8C9-D0E1-2345-FABC-456789012345';
DECLARE @Section3Id      UNIQUEIDENTIFIER = 'A7B8C9D0-E1F2-3456-ABCD-567890123456';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';
DECLARE @SchoolYear      NVARCHAR(20)     = N'2025-2026';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Sections] WHERE [Id] = @Section1Id)
BEGIN
    INSERT INTO [dbo].[Sections]
        ([Id], [TenantId], [Name], [GradeLevel], [SchoolYear], [Capacity], [Adviser], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@Section1Id, @TenantId, N'Einstein',  N'Grade 7', @SchoolYear, 40, N'Ms. Maria Santos',    1, @Now, @SeededBy),
        (@Section2Id, @TenantId, N'Newton',    N'Grade 7', @SchoolYear, 40, N'Mr. Jose Reyes',      1, @Now, @SeededBy),
        (@Section3Id, @TenantId, N'Curie',     N'Grade 7', @SchoolYear, 40, N'Ms. Ana Cruz',        1, @Now, @SeededBy);

    PRINT 'Seeded 3 sections for Grade 7';
END
GO

-- =============================================================================
-- SEED: Students (3 sample students)
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @Student1Id      UNIQUEIDENTIFIER = 'B8C9D0E1-F2A3-4567-BCDE-678901234567';
DECLARE @Student2Id      UNIQUEIDENTIFIER = 'C9D0E1F2-A3B4-5678-CDEF-789012345678';
DECLARE @Student3Id      UNIQUEIDENTIFIER = 'D0E1F2A3-B4C5-6789-DEFA-890123456789';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Students] WHERE [Id] = @Student1Id)
BEGIN
    INSERT INTO [dbo].[Students]
        ([Id], [TenantId], [LRN], [FirstName], [MiddleName], [LastName], [BirthDate], [Gender],
         [Address], [ContactNumber], [Email], [GuardianName], [GuardianContact], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@Student1Id, @TenantId,
         N'100234567890', N'Juan',    N'Dela',   N'Cruz',    '2012-05-15', N'Male',
         N'123 Rizal St, Ermita, Manila', N'+63-917-123-4567', N'juan.delacruz@gmail.com',
         N'Maria Dela Cruz', N'+63-917-765-4321', 1, @Now, @SeededBy),

        (@Student2Id, @TenantId,
         N'100234567891', N'Maria',   N'Santos', N'Reyes',   '2012-08-22', N'Female',
         N'456 Mabini St, Malate, Manila', N'+63-918-234-5678', N'maria.reyes@gmail.com',
         N'Pedro Reyes', N'+63-918-876-5432', 1, @Now, @SeededBy),

        (@Student3Id, @TenantId,
         N'100234567892', N'Carlos',  N'Bautista', N'Garcia', '2012-01-10', N'Male',
         N'789 Luna St, Quiapo, Manila', N'+63-919-345-6789', N'carlos.garcia@gmail.com',
         N'Rosa Garcia', N'+63-919-987-6543', 1, @Now, @SeededBy);

    PRINT 'Seeded 3 sample students';
END
GO

-- =============================================================================
-- SEED: Fees (Grade 7 - Tuition, Miscellaneous, Registration)
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @Fee1Id          UNIQUEIDENTIFIER = 'E1F2A3B4-C5D6-7890-EFAB-901234567890';
DECLARE @Fee2Id          UNIQUEIDENTIFIER = 'F2A3B4C5-D6E7-8901-FABC-012345678901';
DECLARE @Fee3Id          UNIQUEIDENTIFIER = 'A3B4C5D6-E7F8-9012-ABCD-123456789012';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';
DECLARE @SchoolYear      NVARCHAR(20)     = N'2025-2026';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Fees] WHERE [Id] = @Fee1Id)
BEGIN
    INSERT INTO [dbo].[Fees]
        ([Id], [TenantId], [Name], [Description], [Amount], [SchoolYear], [GradeLevel], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@Fee1Id, @TenantId, N'Tuition Fee',       N'Annual tuition fee for Grade 7',        15000.00, @SchoolYear, N'Grade 7', 1, @Now, @SeededBy),
        (@Fee2Id, @TenantId, N'Miscellaneous Fee',  N'Laboratory, library, and other fees',    3000.00, @SchoolYear, N'Grade 7', 1, @Now, @SeededBy),
        (@Fee3Id, @TenantId, N'Registration Fee',   N'One-time registration processing fee',    500.00, @SchoolYear, N'Grade 7', 1, @Now, @SeededBy);

    PRINT 'Seeded 3 fees for Grade 7 (Total: PHP 18,500.00)';
END
GO

-- =============================================================================
-- SEED: Workflow Definition (Standard Enrollment Workflow)
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @WorkflowId      UNIQUEIDENTIFIER = 'D4E5F6A7-B8C9-0123-DEFA-234567890123';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';

IF NOT EXISTS (SELECT 1 FROM [dbo].[WorkflowDefinitions] WHERE [Id] = @WorkflowId)
BEGIN
    INSERT INTO [dbo].[WorkflowDefinitions]
        ([Id], [TenantId], [Name], [Description], [IsActive], [CreatedAt], [CreatedBy])
    VALUES
        (@WorkflowId,
         @TenantId,
         N'Standard Enrollment Workflow',
         N'Default 5-step enrollment workflow: Draft -> Submitted -> Assessed -> Approved -> Paid -> Enrolled',
         1,
         @Now,
         @SeededBy);

    PRINT 'Seeded workflow definition: Standard Enrollment Workflow';
END
GO

-- =============================================================================
-- SEED: Workflow Steps (5 steps)
-- =============================================================================
-- Step 1: Draft -> Submitted       (Student/Parent submits the application)
-- Step 2: Submitted -> Assessed    (Registrar assesses requirements)
-- Step 3: Assessed -> Approved     (Admin approves the enrollment)
-- Step 4: Approved -> Paid         (Cashier confirms payment)
-- Step 5: Paid -> Enrolled         (Registrar finalizes enrollment)
-- =============================================================================
DECLARE @TenantId        UNIQUEIDENTIFIER = 'A1B2C3D4-E5F6-7890-ABCD-EF1234567890';
DECLARE @WorkflowId      UNIQUEIDENTIFIER = 'D4E5F6A7-B8C9-0123-DEFA-234567890123';
DECLARE @Step1Id         UNIQUEIDENTIFIER = 'B4C5D6E7-F8A9-0123-BCDE-234567890123';
DECLARE @Step2Id         UNIQUEIDENTIFIER = 'C5D6E7F8-A9B0-1234-CDEF-345678901234';
DECLARE @Step3Id         UNIQUEIDENTIFIER = 'D6E7F8A9-B0C1-2345-DEFA-456789012345';
DECLARE @Step4Id         UNIQUEIDENTIFIER = 'E7F8A9B0-C1D2-3456-EFAB-567890123456';
DECLARE @Step5Id         UNIQUEIDENTIFIER = 'F8A9B0C1-D2E3-4567-FABC-678901234567';
DECLARE @Now             DATETIME2(7)     = SYSUTCDATETIME();
DECLARE @SeededBy        NVARCHAR(256)    = N'system-seed';

IF NOT EXISTS (SELECT 1 FROM [dbo].[WorkflowSteps] WHERE [Id] = @Step1Id)
BEGIN
    INSERT INTO [dbo].[WorkflowSteps]
        ([Id], [TenantId], [WorkflowDefinitionId], [StepOrder], [StepName],
         [FromStatus], [ToStatus], [RequiredRole], [RequiresApproval], [CreatedAt], [CreatedBy])
    VALUES
        -- Step 1: Submit Application
        (@Step1Id, @TenantId, @WorkflowId, 1,
         N'Submit Application',
         N'Draft', N'Submitted',
         NULL, 0, @Now, @SeededBy),

        -- Step 2: Assess Requirements
        (@Step2Id, @TenantId, @WorkflowId, 2,
         N'Assess Requirements',
         N'Submitted', N'Assessed',
         N'Registrar', 1, @Now, @SeededBy),

        -- Step 3: Approve Enrollment
        (@Step3Id, @TenantId, @WorkflowId, 3,
         N'Approve Enrollment',
         N'Assessed', N'Approved',
         N'Admin', 1, @Now, @SeededBy),

        -- Step 4: Confirm Payment
        (@Step4Id, @TenantId, @WorkflowId, 4,
         N'Confirm Payment',
         N'Approved', N'Paid',
         N'Registrar', 0, @Now, @SeededBy),

        -- Step 5: Finalize Enrollment
        (@Step5Id, @TenantId, @WorkflowId, 5,
         N'Finalize Enrollment',
         N'Paid', N'Enrolled',
         N'Registrar', 0, @Now, @SeededBy);

    PRINT 'Seeded 5 workflow steps for Standard Enrollment Workflow';
END
GO

-- =============================================================================
-- VERIFICATION: Print summary of seeded data
-- =============================================================================
PRINT '';
PRINT '=============================================================================';
PRINT ' Enrollify Schema Setup Complete';
PRINT '=============================================================================';
PRINT '';

SELECT 'Tenants'                   AS [Table], COUNT(*) AS [Rows] FROM [dbo].[Tenants]
UNION ALL
SELECT 'Users',                                COUNT(*)           FROM [dbo].[Users]
UNION ALL
SELECT 'Students',                             COUNT(*)           FROM [dbo].[Students]
UNION ALL
SELECT 'Sections',                             COUNT(*)           FROM [dbo].[Sections]
UNION ALL
SELECT 'Enrollments',                          COUNT(*)           FROM [dbo].[Enrollments]
UNION ALL
SELECT 'EnrollmentStatusHistories',            COUNT(*)           FROM [dbo].[EnrollmentStatusHistories]
UNION ALL
SELECT 'Fees',                                 COUNT(*)           FROM [dbo].[Fees]
UNION ALL
SELECT 'Payments',                             COUNT(*)           FROM [dbo].[Payments]
UNION ALL
SELECT 'WorkflowDefinitions',                  COUNT(*)           FROM [dbo].[WorkflowDefinitions]
UNION ALL
SELECT 'WorkflowSteps',                        COUNT(*)           FROM [dbo].[WorkflowSteps]
ORDER BY [Table];
GO
