# Philippine Learner Record (Sub-project 1a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record every learner the way DepEd expects — real (optional) LRN plus an Enrollify student number, BEEF learner fields, PSGC-structured addresses, SHS track/strand, and documents kept on file across years.

**Architecture:** Typed columns on `Student`, `Enrollment` and `AdmissionApplication`, with the DepEd learner fields declared once on a domain interface (`ILearnerProfile`) and the PSGC address as an EF Core owned type (`PsgcAddress`). Shared Application-layer helpers own each rule exactly once: `StudentNumbering` (minting), `LearnerProfileMapping` (DTO ↔ entity), `ShsStrandRules` (strand validity), `EnrollmentRequirements` (checklist creation + carry-over). The frontend gets a shipped PSGC dataset, a reusable `PsgcAddressComponent`, and shared learner-field components used by the public apply form, the staff student form, and the family profile pages.

**Tech Stack:** .NET 8, EF Core 8.0.11 (SQL Server; InMemory in tests), MediatR 12, FluentValidation 11, xUnit; Angular 18 standalone components, Tailwind, Karma/Jasmine; Python 3 stdlib (one-off PSGC data script).

**Spec:** `docs/superpowers/specs/2026-10-12-learner-record-design.md`

## Global Constraints

- No new NuGet or npm dependencies. The PSGC script uses only the Python 3 standard library.
- CQRS: every command/query is a `record` + handler in the same file under `Features/<Area>/...`; controllers stay thin (`ISender.Send`). Handlers depend on `IApplicationDbContext`, never the concrete context.
- Never add manual `TenantId ==` filters to normal queries. Only slug-based public flows use `.IgnoreQueryFilters().Where(x => x.TenantId == tenantId)`.
- LRN: optional; when present on a new write it is exactly 12 ASCII digits; unique per tenant when not null. Column stays `nvarchar(20)` so legacy values survive.
- Student number format `YYYY-NNNNN`: year in Asia/Manila time (UTC+8, no DST), 5-digit zero-padded per-tenant per-year sequence.
- PSGC codes are 10-digit **strings** (leading zeros matter — never parse to numbers). Names are stored beside codes.
- Track/strand applies only to `Grade 11` / `Grade 12`. Strands are deactivated, never deleted.
- Every FluentValidation max length mirrors the EF column length (over-length → 400, never a SQL 500).
- Visual identity: identifiers (Student No., LRN, codes) use `folio-mono`; folder tabs and stamps stay exclusive to the public flow — never in staff pages or print output.
- Backend must build with **0 warnings** (`<Nullable>enable</Nullable>` everywhere).
- Commands (run from the stated folder):
  - Backend build: `dotnet build Enrollify.sln` (from `backend/`)
  - Backend tests: `dotnet test tests/Enrollify.Application.Tests` (from `backend/`); single class: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~<ClassName>"`
  - Frontend build: `npm run build` (from `enrollify.client/`)
  - Frontend single spec: `npx ng test --watch=false --browsers=ChromeHeadless --include=<path/to/file.spec.ts>` (from `enrollify.client/`)
- Every commit message ends with a blank line and `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`.

## Review Focus

1. **Legacy rows everywhere.** Pre-migration students (flagged LRN, single-line address, no profile) and enrollments with `LearnerType == null` must render and search without errors — a `null` LRN in `s.LRN.Contains(search)` throws under InMemory and is a latent bug in SQL projections. Pinned by tests in Task 1 (enrollment search) and Task 3 (student search).
2. **Siblings with different grades in one application.** A parent submitting a Grade 11 child and a Grade 3 child: only the Grade 11 child needs a strand, and the error must name that child. Pinned in Task 8.
3. **Restarts are no-ops.** Re-running the startup seeder/backfill must not duplicate strands or form fields, and must not re-flag LRNs a registrar already confirmed (flagging happens only in the migration SQL). Pinned in Task 6 and Task 10.
4. **A school that switched SHS off.** If every strand is deactivated, Grade 11/12 applications and assessments must proceed without a strand. Pinned in Task 6 (rules), Task 8 (submission), Task 11 (assessment gate).
5. **Saving an unrelated edit on a legacy student.** Editing a flagged student's contact number must not silently clear `LrnNeedsReview`, and must not be blocked by a legacy non-12-digit LRN the registrar didn't touch. Pinned in Task 4.

## Spec adjustments (decided while planning)

- **PSGC converter:** a Python 3 standard-library script reads PSA's `.xlsx` directly, instead of the spec's Node script over a manual CSV export — one fewer manual step, still zero project dependencies.
- **Enrollment "overview":** enrollment detail has no overview tab, so the learner fields get a new first tab, **Learner**, with a "Needs strand" badge.
- **Demo seed:** students get student numbers, real-shaped LRNs (one without) and PSGC addresses, but no Grade 11 demo enrollment — the demo seed creates no enrollments today. Recorded as a follow-up in Task 22.

---

## File Structure

**Backend — create**
- `backend/src/Enrollify.Domain/ValueObjects/PsgcAddress.cs` — owned address value object.
- `backend/src/Enrollify.Domain/Common/ILearnerProfile.cs` — BEEF learner fields shared by Student + AdmissionApplication.
- `backend/src/Enrollify.Domain/Entities/ShsStrand.cs` — per-tenant track/strand catalog row.
- `backend/src/Enrollify.Infrastructure/Persistence/Configurations/LearnerProfileConfiguration.cs` — column mapping for `ILearnerProfile` (used by two entity configs).
- `backend/src/Enrollify.Infrastructure/Persistence/Configurations/ShsStrandConfiguration.cs`
- `backend/src/Enrollify.Infrastructure/Persistence/Migrations/<timestamp>_AddLearnerRecord.cs` (+ Designer, snapshot update) — scaffolded then hand-edited.
- `backend/src/Enrollify.Application/Common/DepEdLists.cs` — DepEd vocabularies + `IsShsGrade` / `IsTwelveDigitLrn`.
- `backend/src/Enrollify.Application/DTOs/Learners/LearnerDtos.cs` — `PsgcAddressDto`, `LearnerProfileDto`, `EnrollmentLearnerInfoDto`.
- `backend/src/Enrollify.Application/Features/Students/LearnerProfileMapping.cs` — DTO ↔ entity, fill-blanks, copy.
- `backend/src/Enrollify.Application/Features/Students/StudentMapping.cs` — `Student` → `StudentDto`.
- `backend/src/Enrollify.Application/Features/Students/LearnerValidators.cs` — `PsgcAddressDtoValidator`, `LearnerProfileDtoValidator`, `EnrollmentLearnerInfoDtoValidator`.
- `backend/src/Enrollify.Application/Features/Students/Commands/ConfirmLrnCommand.cs`
- `backend/src/Enrollify.Application/Features/ShsStrands/{DefaultShsStrands.cs, ShsStrandRules.cs}`, `Commands/{CreateShsStrandCommand.cs, UpdateShsStrandCommand.cs}`, `Queries/{GetShsStrandsQuery.cs, GetShsStrandsByTenantQuery.cs}`
- `backend/src/Enrollify.Application/DTOs/ShsStrands/ShsStrandDto.cs`
- `backend/src/Enrollify.API/Controllers/ShsStrandsController.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequirements.cs` — single requirement-checklist builder.
- `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentLearnerInfoMapping.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequestPlanner.cs` — shared grade/learner-type/strand resolution for parent + student requests.
- `backend/src/Enrollify.Application/Features/Enrollments/Commands/UpdateEnrollmentLearnerInfoCommand.cs`
- `backend/src/Enrollify.Application/Features/Admissions/ApplicationMapping.cs` — `AdmissionApplication` → `ApplicationDetailDto`, `ApplicantData` → command applicant.
- `backend/src/Enrollify.Application/Features/Students/LearnerRecordBackfill.cs`
- Tests (all in `backend/tests/Enrollify.Application.Tests/`): `LearnerRecordModelTests.cs`, `StudentListLearnerTests.cs`, `StaffStudentEditTests.cs`, `FamilyProfileUpdateTests.cs`, `ShsStrandTests.cs`, `EnrollmentRequirementsTests.cs`, `SubmitApplicationLearnerTests.cs`, `ApprovalLearnerRecordTests.cs`, `LearnerRecordBackfillTests.cs`, `EnrollmentLearnerInfoTests.cs`, `EnrollmentCreationLearnerTests.cs`.

**Backend — modify** (each task lists exact files): `Student.cs`, `Enrollment.cs`, `AdmissionApplication.cs`, `RequirementTemplate.cs`, `IApplicationDbContext.cs`, `ApplicationDbContext.cs`, Student/Enrollment/AdmissionApplication/RequirementTemplate configurations, `ApplicationDbContextSeed.cs`, `StudentNumbering.cs`, all student commands/queries, `SubmitApplicationCommand.cs`, `ReviewApplicationCommand.cs`, `GetApplicationByIdQuery.cs`, `DefaultApplicationFormFields.cs`, `CreateApplicationFormFieldCommand.cs`, enrollment creation commands, `MoveEnrollmentStepCommand.cs`, `BulkReenrollCommand.cs`, `GetEnrollmentByIdQuery.cs`, `GetEnrollmentsQuery.cs`, register/roster reports, requirement-template commands + DTO, `CreateTenantCommand.cs`, `StudentsController.cs`, `EnrollmentsController.cs`, `ParentController.cs`, `SchoolsController.cs`, `AdmissionsController.cs`, `RequirementTemplatesController.cs`.

**Frontend — create**
- `enrollify.client/scripts/psgc/build_psgc.py` + `scripts/psgc/README.md` — PSA workbook → JSON.
- `enrollify.client/public/psgc/index.json`, `enrollify.client/public/psgc/barangays/<code>.json` — generated, committed.
- `enrollify.client/src/app/core/learner.ts` — empty-object factories + `FieldConfigView`.
- `enrollify.client/src/app/core/services/psgc.service.ts`
- `enrollify.client/src/app/core/components/psgc-address.component.ts` (+ `.spec.ts`)
- `enrollify.client/src/app/core/components/dynamic-field.component.ts`
- `enrollify.client/src/app/core/components/learner-background-fields.component.ts`
- `enrollify.client/src/app/core/components/learner-family-fields.component.ts`
- `enrollify.client/src/app/core/components/strand-select.component.ts`

**Frontend — modify:** `core/models/index.ts`, `core/constants.ts`, `core/services/api.service.ts`, `pages/apply/apply.component.ts`, `pages/students/student-form/student-form.component.ts`, `pages/students/student-list/student-list.component.ts`, `pages/enrollments/enrollment-detail/enrollment-detail.component.ts`, `pages/enrollments/enrollment-wizard/enrollment-wizard.component.ts`, `pages/print/print-enrollment.component.ts`, `pages/parent/child-profile/child-profile.component.ts`, `pages/student/my-profile.component.ts`, `pages/parent/child-enrollment/child-enrollment.component.ts`, `pages/student/my-enrollment.component.ts`, `pages/settings/settings.component.ts`.

---

### Task 0: Baseline

**Files:** none (verification only).

- [ ] **Step 1: Confirm a clean, green starting point**

The working tree must contain no uncommitted changes from earlier work (the previous batch — enrollment register, section roster, Under Review — must already be committed by the human partner, or you must be in a fresh worktree branched from a commit that includes it).

Run (from repo root): `git status --short`
Expected: empty output.

- [ ] **Step 2: Build and test**

Run (from `backend/`): `dotnet build Enrollify.sln` → `Build succeeded. 0 Warning(s) 0 Error(s)`
Run (from `backend/`): `dotnet test tests/Enrollify.Application.Tests` → all tests pass; note the count.
Run (from `enrollify.client/`): `npm run build` → completes without errors.

If anything fails here, stop and report — do not start Task 1 on a red baseline.

---

### Task 1: Domain model, EF mapping, migration

**Files:**
- Create: `backend/src/Enrollify.Domain/ValueObjects/PsgcAddress.cs`
- Create: `backend/src/Enrollify.Domain/Common/ILearnerProfile.cs`
- Create: `backend/src/Enrollify.Domain/Entities/ShsStrand.cs`
- Create: `backend/src/Enrollify.Application/Common/DepEdLists.cs`
- Create: `backend/src/Enrollify.Infrastructure/Persistence/Configurations/LearnerProfileConfiguration.cs`
- Create: `backend/src/Enrollify.Infrastructure/Persistence/Configurations/ShsStrandConfiguration.cs`
- Modify: `backend/src/Enrollify.Domain/Entities/Student.cs`, `Enrollment.cs`, `AdmissionApplication.cs`, `RequirementTemplate.cs`
- Modify: `backend/src/Enrollify.Application/Common/Interfaces/IApplicationDbContext.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContext.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/Configurations/StudentConfiguration.cs`, `EnrollmentConfiguration.cs`, `AdmissionApplicationConfiguration.cs`, `RequirementTemplateConfiguration.cs`
- Modify (nullable-LRN ripple): `backend/src/Enrollify.Application/DTOs/Students/StudentDto.cs`, `Features/Enrollments/Queries/GetEnrollmentsQuery.cs`, `Features/Students/Queries/GetStudentsQuery.cs`, `Features/Reports/Queries/GetEnrollmentRegisterQuery.cs`, `Features/Sections/Queries/GetSectionRosterQuery.cs`, `Features/Students/Commands/CreateStudentCommand.cs`, `Features/Students/Commands/UpdateStudentCommand.cs`
- Create: migration `AddLearnerRecord` under `backend/src/Enrollify.Infrastructure/Persistence/Migrations/`
- Test: `backend/tests/Enrollify.Application.Tests/LearnerRecordModelTests.cs`

**Interfaces:**
- Produces: `PsgcAddress` (props `RegionCode/RegionName/ProvinceCode/ProvinceName/CityMunicipalityCode/CityMunicipalityName/BarangayCode/BarangayName/ZipCode`, computed `IsMatched`); `ILearnerProfile` (listed below); `Student.StudentNumber` (string, default `""`), `Student.LRN` (`string?`), `Student.LrnNeedsReview` (bool), `Student.HomeAddress`; `Enrollment.LearnerType/LastGradeLevelCompleted/LastSchoolYearCompleted/LastSchoolAttended/LastSchoolAddress/LastSchoolId/Track/Strand` (all `string?`); `AdmissionApplication.Lrn` + `ILearnerProfile` + `LearnerType/LastGradeLevelCompleted/LastSchoolYearCompleted/LastSchoolId/Track/Strand`; `ShsStrand { Track, Strand, IsActive, DisplayOrder }`; `RequirementTemplate.KeepOnFile`; `IApplicationDbContext.ShsStrands`; `DepEdLists.LearnerTypes`, `DepEdLists.DisabilityTypes`, `DepEdLists.ExtensionNameSuggestions`, `DepEdLists.IsShsGrade(string?)`, `DepEdLists.IsTwelveDigitLrn(string?)`.

- [ ] **Step 1: Write the failing model tests**

Create `backend/tests/Enrollify.Application.Tests/LearnerRecordModelTests.cs`:

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>Persistence shape of the learner record: owned address, nullable LRN, new columns.</summary>
public class LearnerRecordModelTests
{
    [Fact]
    public async Task Student_WithNoAddress_ReloadsWithEmptyNonNullHomeAddress()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        using (var ctx = TestDb.Create(dbName))
        {
            ctx.Students.Add(new Student { StudentNumber = "2026-00001", FirstName = "Ana", LastName = "Cruz", Address = "" });
            await ctx.SaveChangesAsync();
        }

        using var verify = TestDb.Create(dbName);
        var s = await verify.Students.SingleAsync();
        Assert.NotNull(s.HomeAddress);
        Assert.Null(s.HomeAddress.BarangayCode);
        Assert.False(s.HomeAddress.IsMatched);
        Assert.Null(s.LRN);
        Assert.False(s.LrnNeedsReview);
    }

    [Fact]
    public async Task Student_LearnerProfileAndAddress_RoundTrip()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        using (var ctx = TestDb.Create(dbName))
        {
            ctx.Students.Add(new Student
            {
                StudentNumber = "2026-00002", LRN = "136512260001", FirstName = "Ben", LastName = "Reyes", Address = "12 Mabini St",
                ExtensionName = "Jr.", PsaBirthCertificateNumber = "PSA-123", PlaceOfBirth = "Quezon City",
                MotherTongue = "Tagalog", Religion = "Catholic",
                IsIndigenousPeople = true, IpCommunity = "Aeta",
                Is4PsBeneficiary = true, FourPsHouseholdId = "HH-555",
                HasDisability = true, DisabilityTypes = "Hearing Impairment,Cancer",
                FatherLastName = "Reyes", FatherFirstName = "Carlos", MotherMaidenLastName = "Santos", MotherMaidenFirstName = "Liza",
                HomeAddress = new Domain.ValueObjects.PsgcAddress
                {
                    RegionCode = "1300000000", RegionName = "NCR",
                    CityMunicipalityCode = "1380600000", CityMunicipalityName = "City of Manila",
                    BarangayCode = "1380601001", BarangayName = "Barangay 1", ZipCode = "1013"
                }
            });
            await ctx.SaveChangesAsync();
        }

        using var verify = TestDb.Create(dbName);
        var s = await verify.Students.SingleAsync();
        Assert.Equal("136512260001", s.LRN);
        Assert.Equal("Aeta", s.IpCommunity);
        Assert.Equal("Hearing Impairment,Cancer", s.DisabilityTypes);
        Assert.Equal("1380601001", s.HomeAddress.BarangayCode);
        Assert.Null(s.HomeAddress.ProvinceCode);
        Assert.True(s.HomeAddress.IsMatched);
    }

    [Fact]
    public async Task Enrollment_LearnerInfoColumns_RoundTrip()
    {
        var dbName = $"model-{Guid.NewGuid()}";
        Guid enrollmentId;
        using (var ctx = TestDb.Create(dbName))
        {
            var student = new Student { StudentNumber = "2026-00003", FirstName = "Cy", LastName = "Uy", Address = "" };
            ctx.Students.Add(student);
            var e = new Enrollment
            {
                StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 11", Status = EnrollmentStatus.Draft,
                LearnerType = "Transferee", LastGradeLevelCompleted = "Grade 10", LastSchoolYearCompleted = "2025-2026",
                LastSchoolAttended = "Rizal HS", LastSchoolAddress = "Pasig", LastSchoolId = "305123",
                Track = "Academic", Strand = "STEM"
            };
            ctx.Enrollments.Add(e);
            await ctx.SaveChangesAsync();
            enrollmentId = e.Id;
        }

        using var verify = TestDb.Create(dbName);
        var reloaded = await verify.Enrollments.SingleAsync(x => x.Id == enrollmentId);
        Assert.Equal("Transferee", reloaded.LearnerType);
        Assert.Equal("305123", reloaded.LastSchoolId);
        Assert.Equal("STEM", reloaded.Strand);
    }

    [Fact]
    public async Task ShsStrand_IsTenantScoped()
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.ShsStrands.Add(new ShsStrand { TenantId = Guid.NewGuid(), Track = "Academic", Strand = "ABM", IsActive = true });
        await ctx.SaveChangesAsync();

        var visible = await ctx.ShsStrands.Select(s => s.Strand).ToListAsync();
        Assert.Equal(new[] { "STEM" }, visible);
    }

    [Fact]
    public async Task EnrollmentSearch_StudentWithoutLrn_DoesNotThrow()
    {
        var ctx = TestDb.Create();
        var student = new Student { StudentNumber = "2026-00004", FirstName = "Dee", LastName = "Lim", Address = "" };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(new Enrollment { StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 7", Status = EnrollmentStatus.Draft });
        await ctx.SaveChangesAsync();

        var result = await new GetEnrollmentsQueryHandler(ctx).Handle(
            new GetEnrollmentsQuery(null, null, null, "dee"), default);

        Assert.Single(result.Items);
    }

    [Theory]
    [InlineData("Grade 11", true)]
    [InlineData(" grade 12 ", true)]
    [InlineData("Grade 10", false)]
    [InlineData(null, false)]
    public void IsShsGrade(string? grade, bool expected) => Assert.Equal(expected, DepEdLists.IsShsGrade(grade));

    [Theory]
    [InlineData("136512260001", true)]
    [InlineData("13651226000", false)]
    [InlineData("13651226000A", false)]
    [InlineData("１３６５１２２６０００１", false)] // full-width digits are not ASCII digits
    [InlineData(null, false)]
    public void IsTwelveDigitLrn(string? value, bool expected) => Assert.Equal(expected, DepEdLists.IsTwelveDigitLrn(value));
}
```

(`GetEnrollmentsQuery` is `(string? SchoolYear, string? GradeLevel, EnrollmentStatus? Status, string? Search, int Page = 1, int PageSize = 20, bool PendingPaymentsOnly = false)`, so `"dee"` is the `Search` argument.)

- [ ] **Step 2: Run the tests to confirm they fail**

Run (from `backend/`): `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~LearnerRecordModelTests"`
Expected: build FAILS (`PsgcAddress`, `StudentNumber`, `ShsStrands`, `DepEdLists` … do not exist).

- [ ] **Step 3: Add the domain types**

`backend/src/Enrollify.Domain/ValueObjects/PsgcAddress.cs`:

```csharp
namespace Enrollify.Domain.ValueObjects;

/// <summary>
/// A Philippine address resolved against the PSA's Philippine Standard Geographic Code (PSGC).
/// Codes AND names are stored so a later PSGC revision never rewrites how an old record reads.
/// Codes are 10-digit strings (leading zeros matter — never numbers). ProvinceCode is null for
/// cities that sit directly under a region (NCR, independent/highly-urbanized cities).
/// Owned by Student and AdmissionApplication; the columns live in the owner's table.
/// </summary>
public class PsgcAddress
{
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? ProvinceCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityMunicipalityCode { get; set; }
    public string? CityMunicipalityName { get; set; }
    public string? BarangayCode { get; set; }
    public string? BarangayName { get; set; }
    public string? ZipCode { get; set; }

    /// <summary>True once the address is resolved down to a barangay (what SF1 needs).</summary>
    public bool IsMatched => CityMunicipalityCode != null && BarangayCode != null;
}
```

`backend/src/Enrollify.Domain/Common/ILearnerProfile.cs`:

```csharp
using Enrollify.Domain.ValueObjects;

namespace Enrollify.Domain.Common;

/// <summary>
/// DepEd Basic Education Enrollment Form learner fields, shared by Student (the record of truth)
/// and AdmissionApplication (what the family submitted) so mapping and validation exist once.
/// </summary>
public interface ILearnerProfile
{
    string? ExtensionName { get; set; }
    string? PsaBirthCertificateNumber { get; set; }
    string? PlaceOfBirth { get; set; }
    string? MotherTongue { get; set; }
    string? Religion { get; set; }
    bool IsIndigenousPeople { get; set; }
    string? IpCommunity { get; set; }
    bool Is4PsBeneficiary { get; set; }
    string? FourPsHouseholdId { get; set; }
    bool HasDisability { get; set; }
    /// <summary>Comma-separated values from DepEdLists.DisabilityTypes (none contain commas).</summary>
    string? DisabilityTypes { get; set; }
    string? FatherLastName { get; set; }
    string? FatherFirstName { get; set; }
    string? FatherMiddleName { get; set; }
    string? FatherContactNumber { get; set; }
    string? MotherMaidenLastName { get; set; }
    string? MotherMaidenFirstName { get; set; }
    string? MotherMaidenMiddleName { get; set; }
    string? MotherContactNumber { get; set; }
    PsgcAddress HomeAddress { get; set; }
}
```

`backend/src/Enrollify.Domain/Entities/ShsStrand.cs`:

```csharp
using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// One Senior High School track/strand a school offers (e.g. Academic – STEM). Deactivated,
/// never deleted, so an empty catalog always means "defaults never seeded". Enrollments store
/// the Track/Strand strings, so renaming or deactivating a row never rewrites history.
/// </summary>
public class ShsStrand : TenantEntity
{
    public string Track { get; set; } = default!;
    public string Strand { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
```

Replace `backend/src/Enrollify.Domain/Entities/Student.cs` with:

```csharp
using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;

namespace Enrollify.Domain.Entities;

public class Student : TenantEntity, ILearnerProfile
{
    /// <summary>Enrollify-issued "YYYY-NNNNN" (StudentNumbering). Never confused with the LRN.</summary>
    public string StudentNumber { get; set; } = string.Empty;
    /// <summary>DepEd-issued Learner Reference Number (12 digits). Null until known.</summary>
    public string? LRN { get; set; }
    /// <summary>Set by the AddLearnerRecord migration on every pre-existing LRN; cleared by Confirm LRN or an LRN edit.</summary>
    public bool LrnNeedsReview { get; set; }
    public string FirstName { get; set; } = default!;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = default!;
    public DateTime BirthDate { get; set; }
    public string? Gender { get; set; }
    /// <summary>House no. / street / sitio line. The structured part lives in HomeAddress.</summary>
    public string Address { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }          // Set when the student self-registered (Student-role User)
    public Guid? ParentUserId { get; set; }    // Set when a parent registered the student (Parent-role User)

    // DepEd BEEF learner fields (ILearnerProfile)
    public string? ExtensionName { get; set; }
    public string? PsaBirthCertificateNumber { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? MotherTongue { get; set; }
    public string? Religion { get; set; }
    public bool IsIndigenousPeople { get; set; }
    public string? IpCommunity { get; set; }
    public bool Is4PsBeneficiary { get; set; }
    public string? FourPsHouseholdId { get; set; }
    public bool HasDisability { get; set; }
    public string? DisabilityTypes { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherMiddleName { get; set; }
    public string? FatherContactNumber { get; set; }
    public string? MotherMaidenLastName { get; set; }
    public string? MotherMaidenFirstName { get; set; }
    public string? MotherMaidenMiddleName { get; set; }
    public string? MotherContactNumber { get; set; }
    public PsgcAddress HomeAddress { get; set; } = new();

    public string FullName => $"{LastName}, {FirstName} {MiddleName}".TrimEnd();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
```

In `backend/src/Enrollify.Domain/Entities/Enrollment.cs` add after `AssessedAt`:

```csharp
    // DepEd BEEF per-year fields
    public string? LearnerType { get; set; }               // DepEdLists.LearnerTypes; null on legacy rows
    public string? LastGradeLevelCompleted { get; set; }
    public string? LastSchoolYearCompleted { get; set; }
    public string? LastSchoolAttended { get; set; }
    public string? LastSchoolAddress { get; set; }
    public string? LastSchoolId { get; set; }              // 6-digit DepEd School ID
    public string? Track { get; set; }                     // Grade 11/12 only
    public string? Strand { get; set; }                    // Grade 11/12 only
```

In `backend/src/Enrollify.Domain/Entities/AdmissionApplication.cs`: change the class line to `public class AdmissionApplication : TenantEntity, ILearnerProfile`, add `using Enrollify.Domain.ValueObjects;`, and add after `CustomFieldValues`:

```csharp
    // DepEd learner record (transcribed onto Student/Enrollment at approval).
    // PreviousSchool / PreviousSchoolAddress above double as last-school-attended / its address.
    public string? Lrn { get; set; }
    public string? LearnerType { get; set; }
    public string? LastGradeLevelCompleted { get; set; }
    public string? LastSchoolYearCompleted { get; set; }
    public string? LastSchoolId { get; set; }
    public string? Track { get; set; }
    public string? Strand { get; set; }
    public string? ExtensionName { get; set; }
    public string? PsaBirthCertificateNumber { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? MotherTongue { get; set; }
    public string? Religion { get; set; }
    public bool IsIndigenousPeople { get; set; }
    public string? IpCommunity { get; set; }
    public bool Is4PsBeneficiary { get; set; }
    public string? FourPsHouseholdId { get; set; }
    public bool HasDisability { get; set; }
    public string? DisabilityTypes { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherMiddleName { get; set; }
    public string? FatherContactNumber { get; set; }
    public string? MotherMaidenLastName { get; set; }
    public string? MotherMaidenFirstName { get; set; }
    public string? MotherMaidenMiddleName { get; set; }
    public string? MotherContactNumber { get; set; }
    public PsgcAddress HomeAddress { get; set; } = new();
```

In `backend/src/Enrollify.Domain/Entities/RequirementTemplate.cs` add:

```csharp
    /// <summary>Submitted once per learner: a verified copy carries forward to later enrollments.</summary>
    public bool KeepOnFile { get; set; }
```

- [ ] **Step 4: Add `DepEdLists`**

`backend/src/Enrollify.Application/Common/DepEdLists.cs`:

```csharp
namespace Enrollify.Application.Common;

/// <summary>
/// DepEd vocabularies for the learner record. Mirrored in the frontend's
/// enrollify.client/src/app/core/constants.ts — keep spellings identical.
/// </summary>
public static class DepEdLists
{
    public static readonly IReadOnlyList<string> LearnerTypes = new[] { "New", "Continuing", "Transferee", "Returning" };

    /// <summary>BEEF "learner with disability" categories. None contain commas (stored comma-joined).</summary>
    public static readonly IReadOnlyList<string> DisabilityTypes = new[]
    {
        "Visual Impairment", "Hearing Impairment", "Learning Disability", "Intellectual Disability",
        "Autism Spectrum Disorder", "Emotional-Behavioral Disorder", "Orthopedic/Physical Handicap",
        "Speech/Language Disorder", "Cerebral Palsy", "Special Health Problem/Chronic Disease",
        "Multiple Disorder", "Cancer",
    };

    public static readonly IReadOnlyList<string> ExtensionNameSuggestions = new[] { "Jr.", "Sr.", "II", "III", "IV" };

    public static bool IsShsGrade(string? gradeLevel)
    {
        var g = gradeLevel?.Trim();
        return string.Equals(g, "Grade 11", StringComparison.OrdinalIgnoreCase)
            || string.Equals(g, "Grade 12", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTwelveDigitLrn(string? value) =>
        value != null && value.Length == 12 && value.All(char.IsAsciiDigit);
}
```

- [ ] **Step 5: Wire the DbSet**

In `IApplicationDbContext.cs` add `DbSet<ShsStrand> ShsStrands { get; }` after `ApplicationFormFields`. In `ApplicationDbContext.cs` add `public DbSet<ShsStrand> ShsStrands => Set<ShsStrand>();` after the `ApplicationFormFields` property.

- [ ] **Step 6: EF configurations**

`backend/src/Enrollify.Infrastructure/Persistence/Configurations/LearnerProfileConfiguration.cs` (string-based property names because the generic `T` only knows the interface):

```csharp
using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

/// <summary>
/// Column mapping for the ILearnerProfile fields, shared by Student and AdmissionApplication.
/// Lengths here are mirrored by LearnerProfileDtoValidator / PsgcAddressDtoValidator.
/// </summary>
internal static class LearnerProfileConfiguration
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : class, ILearnerProfile
    {
        builder.Property<string?>(nameof(ILearnerProfile.ExtensionName)).HasMaxLength(10);
        builder.Property<string?>(nameof(ILearnerProfile.PsaBirthCertificateNumber)).HasMaxLength(30);
        builder.Property<string?>(nameof(ILearnerProfile.PlaceOfBirth)).HasMaxLength(200);
        builder.Property<string?>(nameof(ILearnerProfile.MotherTongue)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.Religion)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.IpCommunity)).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.FourPsHouseholdId)).HasMaxLength(30);
        builder.Property<string?>(nameof(ILearnerProfile.DisabilityTypes)).HasMaxLength(500);
        foreach (var name in new[]
                 {
                     nameof(ILearnerProfile.FatherLastName), nameof(ILearnerProfile.FatherFirstName), nameof(ILearnerProfile.FatherMiddleName),
                     nameof(ILearnerProfile.MotherMaidenLastName), nameof(ILearnerProfile.MotherMaidenFirstName), nameof(ILearnerProfile.MotherMaidenMiddleName),
                 })
            builder.Property<string?>(name).HasMaxLength(100);
        builder.Property<string?>(nameof(ILearnerProfile.FatherContactNumber)).HasMaxLength(50);
        builder.Property<string?>(nameof(ILearnerProfile.MotherContactNumber)).HasMaxLength(50);

        builder.OwnsOne<PsgcAddress>(nameof(ILearnerProfile.HomeAddress), a =>
        {
            a.Property(p => p.RegionCode).HasColumnName("AddressRegionCode").HasMaxLength(10);
            a.Property(p => p.RegionName).HasColumnName("AddressRegionName").HasMaxLength(150);
            a.Property(p => p.ProvinceCode).HasColumnName("AddressProvinceCode").HasMaxLength(10);
            a.Property(p => p.ProvinceName).HasColumnName("AddressProvinceName").HasMaxLength(150);
            a.Property(p => p.CityMunicipalityCode).HasColumnName("AddressCityMunicipalityCode").HasMaxLength(10);
            a.Property(p => p.CityMunicipalityName).HasColumnName("AddressCityMunicipalityName").HasMaxLength(150);
            a.Property(p => p.BarangayCode).HasColumnName("AddressBarangayCode").HasMaxLength(10);
            a.Property(p => p.BarangayName).HasColumnName("AddressBarangayName").HasMaxLength(150);
            a.Property(p => p.ZipCode).HasColumnName("AddressZipCode").HasMaxLength(10);
            a.Ignore(p => p.IsMatched);
        });
        // Required owned navigation: always materialized, even when every column is NULL.
        builder.Navigation(nameof(ILearnerProfile.HomeAddress)).IsRequired();
    }
}
```

`backend/src/Enrollify.Infrastructure/Persistence/Configurations/ShsStrandConfiguration.cs`:

```csharp
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class ShsStrandConfiguration : IEntityTypeConfiguration<ShsStrand>
{
    public void Configure(EntityTypeBuilder<ShsStrand> builder)
    {
        builder.ToTable("ShsStrands");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Track).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Strand).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.Track, s.Strand }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.IsActive, s.DisplayOrder });
    }
}
```

In `StudentConfiguration.cs` replace the LRN property line and the `(TenantId, LRN)` index, and add the new mappings (keep every other existing line):

```csharp
        builder.Property(s => s.StudentNumber).HasMaxLength(20).IsRequired();
        // Nullable since the learner record: a blank LRN is valid (e.g. incoming Kindergarten).
        // Stays 20 wide so pre-migration values that aren't 12 digits survive.
        builder.Property(s => s.LRN).HasMaxLength(20);
```

```csharp
        builder.HasIndex(s => new { s.TenantId, s.LRN }).IsUnique().HasFilter("[LRN] IS NOT NULL");
        builder.HasIndex(s => new { s.TenantId, s.StudentNumber }).IsUnique();

        LearnerProfileConfiguration.Configure(builder);
```

In `EnrollmentConfiguration.cs` add before the `HasOne` calls:

```csharp
        builder.Property(e => e.LearnerType).HasMaxLength(20);
        builder.Property(e => e.LastGradeLevelCompleted).HasMaxLength(50);
        builder.Property(e => e.LastSchoolYearCompleted).HasMaxLength(20);
        builder.Property(e => e.LastSchoolAttended).HasMaxLength(200);
        builder.Property(e => e.LastSchoolAddress).HasMaxLength(500);
        builder.Property(e => e.LastSchoolId).HasMaxLength(6);
        builder.Property(e => e.Track).HasMaxLength(100);
        builder.Property(e => e.Strand).HasMaxLength(100);
```

In `AdmissionApplicationConfiguration.cs` add before the indexes:

```csharp
        builder.Property(a => a.Lrn).HasMaxLength(20);
        builder.Property(a => a.LearnerType).HasMaxLength(20);
        builder.Property(a => a.LastGradeLevelCompleted).HasMaxLength(50);
        builder.Property(a => a.LastSchoolYearCompleted).HasMaxLength(20);
        builder.Property(a => a.LastSchoolId).HasMaxLength(6);
        builder.Property(a => a.Track).HasMaxLength(100);
        builder.Property(a => a.Strand).HasMaxLength(100);
        LearnerProfileConfiguration.Configure(builder);
```

`RequirementTemplateConfiguration.cs` needs no change (bool maps by convention).

- [ ] **Step 7: Fix the nullable-LRN ripple (0 warnings)**

1. `StudentDto.cs`: change `string LRN,` to `string? LRN,` in `StudentDto`; in `CreateStudentRequest` and `UpdateStudentRequest` change `string LRN,` to `string? LRN,`.
2. `CreateStudentCommand.cs`: the record already has `string? LRN`. No change in this task.
3. `UpdateStudentCommand.cs`: change the record parameter `string LRN` to `string? LRN`, and the validator line to `RuleFor(x => x.LRN).MaximumLength(20);` (Task 4 rewrites the handler).
4. `GetStudentsQuery.cs`: replace `s.LRN.Contains(search));` with `(s.LRN != null && s.LRN.Contains(search)));`.
5. `GetEnrollmentsQuery.cs` line ~47: replace `e.Student.LRN.Contains(search));` with `(e.Student.LRN != null && e.Student.LRN.Contains(search)));`.
6. `GetEnrollmentRegisterQuery.cs`: `EnrollmentRegisterRowDto` parameter `string Lrn,` → `string? Lrn,`.
7. `GetSectionRosterQuery.cs`: `RosterStudentDto` parameter `string Lrn,` → `string? Lrn,`.

Then build: `dotnet build Enrollify.sln` (from `backend/`). Fix any remaining CS8600/CS8601/CS8604 warnings that name `LRN` the same way (null-safe access or `string?` types). Expected: `0 Warning(s) 0 Error(s)`.

- [ ] **Step 8: Run the model tests**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~LearnerRecordModelTests"`
Expected: PASS (all).

- [ ] **Step 9: Scaffold the migration**

Run (from `backend/`):
`dotnet ef migrations add AddLearnerRecord --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API`

- [ ] **Step 10: Hand-edit the migration `Up` method**

Open the new `..._AddLearnerRecord.cs`. EF will have generated `AddColumn<string>("StudentNumber", ..., nullable: false, defaultValue: "")` and `CreateIndex("IX_Students_TenantId_StudentNumber", unique: true)`. A unique index over all-empty strings fails on any database with ≥2 students, so:

1. Cut the `migrationBuilder.CreateIndex(name: "IX_Students_TenantId_StudentNumber", ...)` call.
2. Paste the following at the **end** of `Up`, followed by the cut `CreateIndex` call:

```csharp
            // Backfill Enrollify student numbers ("YYYY-NNNNN", Manila year, per tenant per year)
            // before the unique index exists. Same shape StudentNumbering mints at runtime.
            migrationBuilder.Sql(@"
WITH numbered AS (
    SELECT Id,
           YEAR(DATEADD(hour, 8, CreatedAt)) AS Yr,
           ROW_NUMBER() OVER (PARTITION BY TenantId, YEAR(DATEADD(hour, 8, CreatedAt)) ORDER BY CreatedAt, Id) AS Seq
    FROM Students
)
UPDATE s SET StudentNumber = CAST(n.Yr AS varchar(4)) + '-' + RIGHT('00000' + CAST(n.Seq AS varchar(10)), 5)
FROM Students s JOIN numbered n ON n.Id = s.Id;");

            // Every pre-existing LRN is either Enrollify-minted or unverified, and minted values
            // can't be told apart from real ones — flag them all for registrar review. Runs ONCE
            // (here), never at startup, so confirmed LRNs are never re-flagged.
            migrationBuilder.Sql("UPDATE Students SET LrnNeedsReview = 1 WHERE LRN IS NOT NULL;");

            // DepEd Order 17 s. 2025: the PSA birth certificate is submitted once per learner.
            migrationBuilder.Sql(@"
UPDATE RequirementTemplates SET KeepOnFile = 1
WHERE DocumentName LIKE '%PSA%' OR DocumentName LIKE '%Birth Certificate%';");
```

3. Confirm the generated order for the LRN change is: `DropIndex(IX_Students_TenantId_LRN)` → `AlterColumn(LRN, nullable: true)` → `CreateIndex(IX_Students_TenantId_LRN, filter: "[LRN] IS NOT NULL")`. If `AlterColumn` comes before the `DropIndex`, move the `DropIndex` above it (SQL Server cannot alter a column's nullability while it is in an index).

- [ ] **Step 11: Verify the generated SQL**

Run (from `backend/`):
`dotnet ef migrations script AddAccountSecurityFields AddLearnerRecord --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API -o ../learner-record.sql`

Open `learner-record.sql` and check, in order: Students `StudentNumber` column added → the `WITH numbered` update → `LrnNeedsReview` update → `KeepOnFile` update → `CREATE UNIQUE INDEX [IX_Students_TenantId_StudentNumber]`; the `IX_Students_TenantId_LRN` index is dropped before `ALTER COLUMN [LRN]` and recreated with `WHERE [LRN] IS NOT NULL`; `ShsStrands` table created; `Address*` columns on both `Students` and `AdmissionApplications`. Then delete `learner-record.sql` (do not commit it).

If a local SQL Server is reachable (default connection string in `appsettings.json`), also run `dotnet ef database update --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API` and confirm it succeeds. If no database is reachable, say so in your report — do not skip the script check.

- [ ] **Step 12: Full build + test**

Run: `dotnet build Enrollify.sln` → 0 warnings. Run: `dotnet test tests/Enrollify.Application.Tests` → all pass (existing tests construct `Student` without `StudentNumber`; the `""` default keeps them valid under InMemory).

- [ ] **Step 13: Commit**

```bash
git add backend/
git commit -m "Learner record: domain model, EF mapping, AddLearnerRecord migration

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Student numbers replace minted LRNs

**Files:**
- Modify: `backend/src/Enrollify.Application/Features/Students/StudentNumbering.cs` (rewrite)
- Modify: `backend/src/Enrollify.Application/Features/Students/Commands/CreateStudentCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Admissions/Commands/ReviewApplicationCommand.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs` (demo students)
- Modify: `backend/tests/Enrollify.Application.Tests/StudentNumberingTests.cs` (rewrite)
- Modify: `backend/tests/Enrollify.Application.Tests/StudentDedupTests.cs`

**Interfaces:**
- Consumes: `Student.StudentNumber`, `DepEdLists.IsTwelveDigitLrn` (Task 1).
- Produces: `StudentNumbering.ManilaYear(DateTime utcNow) : int`; `StudentNumbering.NextStudentNumberAsync(IApplicationDbContext, DateTime utcNow, CancellationToken) : Task<string>`. `NextLrnAsync` is deleted. `CreateStudentCommand` never invents an LRN.

- [ ] **Step 1: Rewrite the numbering tests (failing)**

Replace `backend/tests/Enrollify.Application.Tests/StudentNumberingTests.cs`:

```csharp
using Enrollify.Application.Features.Students;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Interfaces;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class StudentNumberingTests
{
    private static readonly DateTime Mid2026Utc = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Student StudentWith(string number) => new()
    {
        StudentNumber = number, FirstName = "Test", LastName = "Student", Address = "Manila"
    };

    [Fact]
    public async Task Next_StartsAt00001_ForTheYear()
    {
        var ctx = TestDb.Create();
        Assert.Equal("2026-00001", await StudentNumbering.NextStudentNumberAsync(ctx, Mid2026Utc, default));
    }

    [Fact]
    public async Task Next_IncrementsPastMax_IgnoringOtherYears()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(StudentWith("2026-00002"));
        ctx.Students.Add(StudentWith("2026-00007"));
        ctx.Students.Add(StudentWith("2025-00099"));
        await ctx.SaveChangesAsync();

        Assert.Equal("2026-00008", await StudentNumbering.NextStudentNumberAsync(ctx, Mid2026Utc, default));
    }

    [Fact]
    public void ManilaYear_RollsOverEightHoursBeforeUtc()
    {
        // 2025-12-31 16:30 UTC is already 2026-01-01 00:30 in Manila.
        Assert.Equal(2026, StudentNumbering.ManilaYear(new DateTime(2025, 12, 31, 16, 30, 0, DateTimeKind.Utc)));
        Assert.Equal(2025, StudentNumbering.ManilaYear(new DateTime(2025, 12, 31, 15, 59, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task CreateStudent_BlankLrn_StaysNull_AndGetsStudentNumber()
    {
        var ctx = TestDb.Create();
        var dto = await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "Manila", null, null, null, null), default);

        Assert.Null(dto.LRN);
        var year = StudentNumbering.ManilaYear(DateTime.UtcNow);
        Assert.Equal($"{year}-00001", (await ctx.Students.SingleAsync()).StudentNumber);
    }

    [Fact]
    public async Task CreateStudent_KeepsProvidedLrn()
    {
        var ctx = TestDb.Create();
        var dto = await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            " 136512260001 ", "Ben", "", "Reyes", new DateTime(2013, 2, 2), null, "Manila", null, null, null, null), default);

        Assert.Equal("136512260001", dto.LRN);
    }

    [Fact]
    public void CreateStudentValidator_RejectsNonTwelveDigitLrn()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            "100100", "Ben", "", "Reyes", new DateTime(2013, 2, 2), null, "Manila", null, null, null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "LRN");
    }

    /// <summary>
    /// A rival request lands the same freshly-minted student number between our generation
    /// and save; the save fails the way the (TenantId, StudentNumber) unique index would.
    /// </summary>
    private class RacingContext : ApplicationDbContext
    {
        private readonly string _dbName;
        private bool _raced;

        public RacingContext(string dbName)
            : base(TestDb.Options(dbName), new FixedTenantProvider(TestDb.TenantId)) => _dbName = dbName;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_raced)
            {
                _raced = true;
                using var rival = TestDb.Create(_dbName);
                rival.Students.Add(StudentWith($"{StudentNumbering.ManilaYear(DateTime.UtcNow)}-00001"));
                await rival.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated unique index violation on (TenantId, StudentNumber).");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task CreateStudent_RetriesWithFreshNumber_OnUniqueCollision()
    {
        var dbName = $"sn-race-{Guid.NewGuid()}";
        using var ctx = new RacingContext(dbName);

        await new CreateStudentCommandHandler(ctx).Handle(new CreateStudentCommand(
            null, "Cara", "", "Reyes", new DateTime(2012, 5, 5), null, "Manila", null, null, null, null), default);

        using var verify = TestDb.Create(dbName);
        var numbers = await verify.Students.Select(s => s.StudentNumber).OrderBy(n => n).ToListAsync();
        var year = StudentNumbering.ManilaYear(DateTime.UtcNow);
        Assert.Equal(new[] { $"{year}-00001", $"{year}-00002" }, numbers);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~StudentNumberingTests"` → FAIL (`NextStudentNumberAsync`/`ManilaYear` missing).

- [ ] **Step 2: Rewrite `StudentNumbering`**

Replace `backend/src/Enrollify.Application/Features/Students/StudentNumbering.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Enrollify student numbers: "YYYY-NNNNN" — the Manila calendar year plus a per-tenant,
/// per-year 5-digit sequence. Deliberately NOT LRN-shaped (LRNs are DepEd-issued, 12 digits).
/// Two concurrent requests can compute the same next number; the (TenantId, StudentNumber)
/// unique index rejects the loser, so callers inserting a fresh number retry a couple of times,
/// regenerating between attempts.
/// </summary>
public static class StudentNumbering
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8); // PH has no DST

    public static int ManilaYear(DateTime utcNow) => (utcNow + ManilaOffset).Year;

    public static async Task<string> NextStudentNumberAsync(IApplicationDbContext context, DateTime utcNow, CancellationToken ct)
    {
        var prefix = $"{ManilaYear(utcNow)}-";
        // Fixed width within a year, so lexicographic max == numeric max.
        var max = await context.Students
            .Where(s => s.StudentNumber.StartsWith(prefix) && s.StudentNumber.Length == prefix.Length + 5)
            .OrderByDescending(s => s.StudentNumber)
            .Select(s => s.StudentNumber)
            .FirstOrDefaultAsync(ct);

        var next = max != null && int.TryParse(max.AsSpan(prefix.Length), out var seq) ? seq + 1 : 1;
        return $"{prefix}{next:D5}";
    }
}
```

- [ ] **Step 3: Update `CreateStudentCommand`**

In `CreateStudentCommand.cs`:

1. Add `using Enrollify.Application.Common;`.
2. Validator: replace `RuleFor(x => x.LRN).MaximumLength(20);` with:

```csharp
        RuleFor(x => x.LRN)
            .Must(l => DepEdLists.IsTwelveDigitLrn(l!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.LRN))
            .WithMessage("LRN must be exactly 12 digits (leave it blank if the learner has none yet).");
```

3. Replace the handler's `Handle` body from the `var lrn = ...` line through the retry loop with:

```csharp
        var lrn = string.IsNullOrWhiteSpace(request.LRN) ? null : request.LRN.Trim();
        if (lrn != null && await _context.Students.AnyAsync(s => s.LRN == lrn, cancellationToken))
            throw new InvalidOperationException($"A student with LRN '{lrn}' already exists.");

        var student = new Student
        {
            StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, cancellationToken),
            LRN = lrn,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            Email = request.Email,
            GuardianName = request.GuardianName,
            GuardianContact = request.GuardianContact,
            UserId = request.UserId,
            ParentUserId = request.ParentUserId
        };

        _context.Students.Add(student);

        // Two concurrent creates can mint the same student number; the (TenantId, StudentNumber)
        // unique index rejects the loser — regenerate and retry a couple of times.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                student.StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, cancellationToken);
            }
        }
```

Keep the existing `return new StudentDto(...)` (Task 3 replaces it).

- [ ] **Step 4: Update approval minting**

In `ReviewApplicationCommand.cs`:

1. In the `new Student { ... }` initializer replace `LRN = await StudentNumbering.NextLrnAsync(_context, ct),` with `StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, ct),`.
2. In the retry `catch` replace `createdStudent.LRN = await StudentNumbering.NextLrnAsync(_context, ct);` with `createdStudent.StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, ct);` and update both comments there to say "student number" instead of "LRN".
3. Replace `existingStudent.LRN` in the already-enrolled exception message with `existingStudent.StudentNumber`, and `student.LRN` in the enrollment `Remarks` with `student.StudentNumber`.

- [ ] **Step 5: Update dedup tests**

In `StudentDedupTests.cs`, in `ExistingStudent(...)` add `StudentNumber = "2026-00001",` to the initializer. In `Approval_MatchesByNameAndDob_CaseInsensitively_AndReusesStudent` change the last assertion to `Assert.Contains($"existing student {existing.StudentNumber}", enrollment.Remarks);`. Leave `Approval_MatchesByProvidedLrn_FromCustomField` as is (Task 9 changes it).

- [ ] **Step 6: Seeder demo students**

In `ApplicationDbContextSeed.cs`, in the three demo `new() { TenantId = tenantId, LRN = "1001001000xx", ... }` initializers, add `StudentNumber = "2026-00001"` / `"2026-00002"` / `"2026-00003"` respectively and replace the fake LRNs: Juan `LRN = "136512260001"`, Maria `LRN = null`, Jose `LRN = "136512260002"`. (Fresh databases have no migration flag to clear; these demo LRNs are presented as confirmed.)

- [ ] **Step 7: Build, run all tests**

Run: `dotnet build Enrollify.sln` → 0 warnings, no references to `NextLrnAsync` remain (`grep -rn NextLrnAsync backend/` → no output).
Run: `dotnet test tests/Enrollify.Application.Tests` → all pass.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "Learner record: Enrollify student numbers replace minted LRNs

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Learner DTOs, mapping, validators, student read paths

**Files:**
- Create: `backend/src/Enrollify.Application/DTOs/Learners/LearnerDtos.cs`
- Create: `backend/src/Enrollify.Application/Features/Students/LearnerProfileMapping.cs`
- Create: `backend/src/Enrollify.Application/Features/Students/StudentMapping.cs`
- Create: `backend/src/Enrollify.Application/Features/Students/LearnerValidators.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Students/StudentDto.cs`
- Modify (use `StudentMapping.ToDto`): `Features/Students/Commands/CreateStudentCommand.cs`, `SetStudentActiveCommand.cs`, `UpdateChildProfileCommand.cs`, `UpdateMyProfileCommand.cs`, `UpdateStudentCommand.cs`, `Features/Students/Queries/GetChildProfileQuery.cs`, `GetMyProfileQuery.cs`, `GetStudentByIdQuery.cs`, `GetStudentsQuery.cs`
- Modify: `backend/src/Enrollify.API/Controllers/StudentsController.cs` (`GetAll` filters)
- Test: `backend/tests/Enrollify.Application.Tests/StudentListLearnerTests.cs`

**Interfaces:**
- Consumes: Task 1 entities, `DepEdLists`, `GradeLevels.All`.
- Produces:
  - `PsgcAddressDto(string? RegionCode = null, string? RegionName = null, string? ProvinceCode = null, string? ProvinceName = null, string? CityMunicipalityCode = null, string? CityMunicipalityName = null, string? BarangayCode = null, string? BarangayName = null, string? ZipCode = null)`
  - `LearnerProfileDto(string? ExtensionName = null, string? PsaBirthCertificateNumber = null, string? PlaceOfBirth = null, string? MotherTongue = null, string? Religion = null, bool IsIndigenousPeople = false, string? IpCommunity = null, bool Is4PsBeneficiary = false, string? FourPsHouseholdId = null, bool HasDisability = false, List<string>? DisabilityTypes = null, string? FatherLastName = null, string? FatherFirstName = null, string? FatherMiddleName = null, string? FatherContactNumber = null, string? MotherMaidenLastName = null, string? MotherMaidenFirstName = null, string? MotherMaidenMiddleName = null, string? MotherContactNumber = null, PsgcAddressDto? HomeAddress = null)`
  - `EnrollmentLearnerInfoDto(string? LearnerType = null, string? LastGradeLevelCompleted = null, string? LastSchoolYearCompleted = null, string? LastSchoolAttended = null, string? LastSchoolAddress = null, string? LastSchoolId = null, string? Track = null, string? Strand = null)`
  - `LearnerProfileMapping.ToDto(ILearnerProfile) : LearnerProfileDto`, `.ToDto(PsgcAddress?) : PsgcAddressDto`, `.Apply(LearnerProfileDto, ILearnerProfile)`, `.ApplyAddress(PsgcAddressDto?, PsgcAddress)`, `.FillBlanks(ILearnerProfile source, ILearnerProfile target)`, `.JoinDisabilities(IEnumerable<string>?) : string?`, `.SplitDisabilities(string?) : List<string>`, `.Clean(string?) : string?`
  - `StudentMapping.ToDto(Student) : StudentDto`
  - `StudentDto` gains trailing `string StudentNumber = "", bool LrnNeedsReview = false, LearnerProfileDto? Profile = null`
  - Validators: `PsgcAddressDtoValidator`, `LearnerProfileDtoValidator`, `EnrollmentLearnerInfoDtoValidator`
  - `GetStudentsQuery(string? Search, int Page = 1, int PageSize = 20, bool IncludeInactive = false, bool MissingLrn = false, bool LrnNeedsReview = false)`; `GET /api/students?missingLrn=&lrnNeedsReview=`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/StudentListLearnerTests.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students;
using Enrollify.Application.Features.Students.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.ValueObjects;
using Xunit;

namespace Enrollify.Application.Tests;

public class StudentListLearnerTests
{
    private static Student S(string number, string first, string? lrn = null, bool flagged = false) => new()
    {
        StudentNumber = number, FirstName = first, LastName = "Cruz", Address = "", LRN = lrn, LrnNeedsReview = flagged
    };

    [Fact]
    public async Task Search_WithNullLrnStudents_FindsByStudentNumber()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery("2026-00001"), default);

        Assert.Equal("Ana", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task Search_FindsByLrn()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery("1365122600"), default);

        Assert.Equal("Ben", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task MissingLrnFilter_ReturnsOnlyStudentsWithoutLrn()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null, MissingLrn: true), default);

        Assert.Equal("Ana", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task LrnNeedsReviewFilter_ReturnsOnlyFlagged()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana", "100100100001", flagged: true), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null, LrnNeedsReview: true), default);

        var only = Assert.Single(page.Items);
        Assert.True(only.LrnNeedsReview);
        Assert.Equal("2026-00001", only.StudentNumber);
    }

    [Fact]
    public void StudentMapping_ExposesProfileAndSplitsDisabilities()
    {
        var s = S("2026-00009", "Cy");
        s.HasDisability = true;
        s.DisabilityTypes = "Hearing Impairment,Cancer";
        s.HomeAddress = new PsgcAddress { CityMunicipalityCode = "1380600000", BarangayCode = "1380601001" };

        var dto = StudentMapping.ToDto(s);

        Assert.Equal("2026-00009", dto.StudentNumber);
        Assert.Equal(new[] { "Hearing Impairment", "Cancer" }, dto.Profile!.DisabilityTypes);
        Assert.Equal("1380601001", dto.Profile.HomeAddress!.BarangayCode);
    }

    [Fact]
    public void Apply_ClearsFollowUpFields_WhenTheirFlagIsOff()
    {
        var target = S("2026-00010", "Dee");
        LearnerProfileMapping.Apply(new LearnerProfileDto(
            IsIndigenousPeople: false, IpCommunity: "Aeta",
            Is4PsBeneficiary: false, FourPsHouseholdId: "HH-1",
            HasDisability: false, DisabilityTypes: new() { "Cancer" },
            Religion: "  Catholic  "), target);

        Assert.Null(target.IpCommunity);
        Assert.Null(target.FourPsHouseholdId);
        Assert.Null(target.DisabilityTypes);
        Assert.Equal("Catholic", target.Religion);
    }

    [Fact]
    public void FillBlanks_NeverOverwritesExistingValues()
    {
        var target = S("2026-00011", "Eve");
        target.Religion = "Catholic";
        var source = S("2026-00012", "Eve");
        source.Religion = "INC";
        source.MotherTongue = "Ilocano";
        source.IsIndigenousPeople = true;
        source.IpCommunity = "Igorot";
        source.HomeAddress = new PsgcAddress { RegionCode = "0100000000", CityMunicipalityCode = "0102800000", BarangayCode = "0102801001" };

        LearnerProfileMapping.FillBlanks(source, target);

        Assert.Equal("Catholic", target.Religion);
        Assert.Equal("Ilocano", target.MotherTongue);
        Assert.True(target.IsIndigenousPeople);
        Assert.Equal("Igorot", target.IpCommunity);
        Assert.Equal("0102801001", target.HomeAddress.BarangayCode);
    }

    [Theory]
    [InlineData("13000000", "Region code must be a 10-digit PSGC code.")]
    [InlineData("١٣٠٠٠٠٠٠٠٠", "Region code must be a 10-digit PSGC code.")] // non-ASCII digits
    public void PsgcValidator_RejectsBadRegionCodes(string code, string message)
    {
        var result = new PsgcAddressDtoValidator().Validate(new PsgcAddressDto(RegionCode: code, RegionName: "X"));
        Assert.Contains(result.Errors, e => e.ErrorMessage == message);
    }

    [Fact]
    public void PsgcValidator_BarangayRequiresCity()
    {
        var result = new PsgcAddressDtoValidator().Validate(new PsgcAddressDto(
            RegionCode: "1300000000", RegionName: "NCR", BarangayCode: "1380601001", BarangayName: "Barangay 1"));
        Assert.Contains(result.Errors, e => e.PropertyName == "CityMunicipalityCode");
    }

    [Fact]
    public void ProfileValidator_EnforcesFollowUpsAndKnownDisabilityTypes()
    {
        var result = new LearnerProfileDtoValidator().Validate(new LearnerProfileDto(
            IsIndigenousPeople: true, Is4PsBeneficiary: true, HasDisability: true, DisabilityTypes: new() { "Flu" }));

        Assert.Contains(result.Errors, e => e.PropertyName == "IpCommunity");
        Assert.Contains(result.Errors, e => e.PropertyName == "FourPsHouseholdId");
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("'Flu' is not a recognized disability type"));
    }

    [Fact]
    public void ProfileValidator_RequiresAtLeastOneDisabilityType()
    {
        var result = new LearnerProfileDtoValidator().Validate(new LearnerProfileDto(HasDisability: true));
        Assert.Contains(result.Errors, e => e.PropertyName == "DisabilityTypes");
    }

    [Theory]
    [InlineData("LearnerType", "Graduate")]
    [InlineData("LastSchoolYearCompleted", "2025/2026")]
    [InlineData("LastSchoolId", "30512")]
    [InlineData("LastGradeLevelCompleted", "Grade 13")]
    public void LearnerInfoValidator_RejectsMalformedValues(string property, string value)
    {
        var dto = property switch
        {
            "LearnerType" => new EnrollmentLearnerInfoDto(LearnerType: value),
            "LastSchoolYearCompleted" => new EnrollmentLearnerInfoDto(LastSchoolYearCompleted: value),
            "LastSchoolId" => new EnrollmentLearnerInfoDto(LastSchoolId: value),
            _ => new EnrollmentLearnerInfoDto(LastGradeLevelCompleted: value),
        };

        var result = new EnrollmentLearnerInfoDtoValidator().Validate(dto);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void LearnerInfoValidator_AcceptsBlankStrings()
    {
        var result = new EnrollmentLearnerInfoDtoValidator().Validate(
            new EnrollmentLearnerInfoDto(LearnerType: "", LastSchoolId: " ", LastSchoolYearCompleted: ""));
        Assert.True(result.IsValid);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~StudentListLearnerTests"` → build FAILS (types missing).

- [ ] **Step 2: Create the DTOs**

`backend/src/Enrollify.Application/DTOs/Learners/LearnerDtos.cs`:

```csharp
namespace Enrollify.Application.DTOs.Learners;

/// <summary>PSGC-resolved address. Codes are 10-digit strings; names travel with them.</summary>
public record PsgcAddressDto(
    string? RegionCode = null, string? RegionName = null,
    string? ProvinceCode = null, string? ProvinceName = null,
    string? CityMunicipalityCode = null, string? CityMunicipalityName = null,
    string? BarangayCode = null, string? BarangayName = null,
    string? ZipCode = null);

/// <summary>DepEd BEEF learner fields (no LRN — that travels separately, with its own rules).</summary>
public record LearnerProfileDto(
    string? ExtensionName = null,
    string? PsaBirthCertificateNumber = null,
    string? PlaceOfBirth = null,
    string? MotherTongue = null,
    string? Religion = null,
    bool IsIndigenousPeople = false,
    string? IpCommunity = null,
    bool Is4PsBeneficiary = false,
    string? FourPsHouseholdId = null,
    bool HasDisability = false,
    List<string>? DisabilityTypes = null,
    string? FatherLastName = null,
    string? FatherFirstName = null,
    string? FatherMiddleName = null,
    string? FatherContactNumber = null,
    string? MotherMaidenLastName = null,
    string? MotherMaidenFirstName = null,
    string? MotherMaidenMiddleName = null,
    string? MotherContactNumber = null,
    PsgcAddressDto? HomeAddress = null);

/// <summary>Per-enrollment DepEd fields (learner type, previous school, SHS track/strand).</summary>
public record EnrollmentLearnerInfoDto(
    string? LearnerType = null,
    string? LastGradeLevelCompleted = null,
    string? LastSchoolYearCompleted = null,
    string? LastSchoolAttended = null,
    string? LastSchoolAddress = null,
    string? LastSchoolId = null,
    string? Track = null,
    string? Strand = null);
```

- [ ] **Step 3: Create `LearnerProfileMapping`**

`backend/src/Enrollify.Application/Features/Students/LearnerProfileMapping.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// The one place DepEd learner fields move between DTOs and entities (Student, AdmissionApplication).
/// Follow-up fields are cleared whenever their yes/no is off, so stale values never linger.
/// The owned PsgcAddress is always mutated in place (never replaced) so EF tracks it as an update.
/// </summary>
public static class LearnerProfileMapping
{
    public static LearnerProfileDto ToDto(ILearnerProfile p) => new(
        p.ExtensionName, p.PsaBirthCertificateNumber, p.PlaceOfBirth, p.MotherTongue, p.Religion,
        p.IsIndigenousPeople, p.IpCommunity, p.Is4PsBeneficiary, p.FourPsHouseholdId,
        p.HasDisability, SplitDisabilities(p.DisabilityTypes),
        p.FatherLastName, p.FatherFirstName, p.FatherMiddleName, p.FatherContactNumber,
        p.MotherMaidenLastName, p.MotherMaidenFirstName, p.MotherMaidenMiddleName, p.MotherContactNumber,
        ToDto(p.HomeAddress));

    public static PsgcAddressDto ToDto(PsgcAddress? a) => a == null
        ? new PsgcAddressDto()
        : new PsgcAddressDto(a.RegionCode, a.RegionName, a.ProvinceCode, a.ProvinceName,
            a.CityMunicipalityCode, a.CityMunicipalityName, a.BarangayCode, a.BarangayName, a.ZipCode);

    /// <summary>Overwrites every learner field on <paramref name="target"/> (staff edit, new application, new student).</summary>
    public static void Apply(LearnerProfileDto dto, ILearnerProfile target)
    {
        target.ExtensionName = Clean(dto.ExtensionName);
        target.PsaBirthCertificateNumber = Clean(dto.PsaBirthCertificateNumber);
        target.PlaceOfBirth = Clean(dto.PlaceOfBirth);
        target.MotherTongue = Clean(dto.MotherTongue);
        target.Religion = Clean(dto.Religion);
        target.IsIndigenousPeople = dto.IsIndigenousPeople;
        target.IpCommunity = dto.IsIndigenousPeople ? Clean(dto.IpCommunity) : null;
        target.Is4PsBeneficiary = dto.Is4PsBeneficiary;
        target.FourPsHouseholdId = dto.Is4PsBeneficiary ? Clean(dto.FourPsHouseholdId) : null;
        target.HasDisability = dto.HasDisability;
        target.DisabilityTypes = dto.HasDisability ? JoinDisabilities(dto.DisabilityTypes) : null;
        target.FatherLastName = Clean(dto.FatherLastName);
        target.FatherFirstName = Clean(dto.FatherFirstName);
        target.FatherMiddleName = Clean(dto.FatherMiddleName);
        target.FatherContactNumber = Clean(dto.FatherContactNumber);
        target.MotherMaidenLastName = Clean(dto.MotherMaidenLastName);
        target.MotherMaidenFirstName = Clean(dto.MotherMaidenFirstName);
        target.MotherMaidenMiddleName = Clean(dto.MotherMaidenMiddleName);
        target.MotherContactNumber = Clean(dto.MotherContactNumber);
        ApplyAddress(dto.HomeAddress, target.HomeAddress);
    }

    /// <summary>Overwrites the address in place; a null DTO clears it. Names are dropped when their code is.</summary>
    public static void ApplyAddress(PsgcAddressDto? dto, PsgcAddress target)
    {
        target.RegionCode = Clean(dto?.RegionCode);
        target.RegionName = target.RegionCode == null ? null : Clean(dto?.RegionName);
        target.ProvinceCode = Clean(dto?.ProvinceCode);
        target.ProvinceName = target.ProvinceCode == null ? null : Clean(dto?.ProvinceName);
        target.CityMunicipalityCode = Clean(dto?.CityMunicipalityCode);
        target.CityMunicipalityName = target.CityMunicipalityCode == null ? null : Clean(dto?.CityMunicipalityName);
        target.BarangayCode = Clean(dto?.BarangayCode);
        target.BarangayName = target.BarangayCode == null ? null : Clean(dto?.BarangayName);
        target.ZipCode = Clean(dto?.ZipCode);
    }

    /// <summary>
    /// Fills only blank fields on <paramref name="target"/> from <paramref name="source"/> — used when
    /// approval matches an existing student. A false yes/no counts as blank; the address is copied only
    /// when the target's isn't PSGC-matched yet.
    /// </summary>
    public static void FillBlanks(ILearnerProfile source, ILearnerProfile target)
    {
        target.ExtensionName ??= source.ExtensionName;
        target.PsaBirthCertificateNumber ??= source.PsaBirthCertificateNumber;
        target.PlaceOfBirth ??= source.PlaceOfBirth;
        target.MotherTongue ??= source.MotherTongue;
        target.Religion ??= source.Religion;
        if (!target.IsIndigenousPeople && source.IsIndigenousPeople)
        {
            target.IsIndigenousPeople = true;
            target.IpCommunity ??= source.IpCommunity;
        }
        if (!target.Is4PsBeneficiary && source.Is4PsBeneficiary)
        {
            target.Is4PsBeneficiary = true;
            target.FourPsHouseholdId ??= source.FourPsHouseholdId;
        }
        if (!target.HasDisability && source.HasDisability)
        {
            target.HasDisability = true;
            target.DisabilityTypes ??= source.DisabilityTypes;
        }
        target.FatherLastName ??= source.FatherLastName;
        target.FatherFirstName ??= source.FatherFirstName;
        target.FatherMiddleName ??= source.FatherMiddleName;
        target.FatherContactNumber ??= source.FatherContactNumber;
        target.MotherMaidenLastName ??= source.MotherMaidenLastName;
        target.MotherMaidenFirstName ??= source.MotherMaidenFirstName;
        target.MotherMaidenMiddleName ??= source.MotherMaidenMiddleName;
        target.MotherContactNumber ??= source.MotherContactNumber;
        if (!target.HomeAddress.IsMatched && source.HomeAddress.IsMatched)
            ApplyAddress(ToDto(source.HomeAddress), target.HomeAddress);
    }

    public static string? JoinDisabilities(IEnumerable<string>? types)
    {
        var cleaned = (types ?? Enumerable.Empty<string>())
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return cleaned.Count == 0 ? null : string.Join(",", cleaned);
    }

    public static List<string> SplitDisabilities(string? csv) =>
        csv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>();

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 4: Create `StudentMapping` and extend `StudentDto`**

In `StudentDto.cs` add `using Enrollify.Application.DTOs.Learners;` and change the `StudentDto` record tail from `bool IsActive = true);` to:

```csharp
    bool IsActive = true,
    string StudentNumber = "",
    bool LrnNeedsReview = false,
    LearnerProfileDto? Profile = null);
```

`backend/src/Enrollify.Application/Features/Students/StudentMapping.cs`:

```csharp
using Enrollify.Application.DTOs.Students;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Students;

/// <summary>Single Student → StudentDto mapping; new learner fields are added here, nowhere else.</summary>
public static class StudentMapping
{
    public static StudentDto ToDto(Student s) => new(
        s.Id, s.LRN, s.FirstName, s.MiddleName, s.LastName,
        s.BirthDate, s.Gender, s.Address, s.ContactNumber, s.Email,
        s.GuardianName, s.GuardianContact, s.FullName,
        s.UserId, s.ParentUserId, s.IsActive,
        s.StudentNumber, s.LrnNeedsReview, LearnerProfileMapping.ToDto(s));
}
```

- [ ] **Step 5: Create the validators**

`backend/src/Enrollify.Application/Features/Students/LearnerValidators.cs`:

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.DTOs.Learners;
using FluentValidation;

namespace Enrollify.Application.Features.Students;

// Lengths mirror LearnerProfileConfiguration / EnrollmentConfiguration. Blank strings are
// treated as "not provided" everywhere (LearnerProfileMapping.Clean turns them into null).
// Digit checks use [0-9] / char.IsAsciiDigit — .NET's \d also matches non-ASCII digits.

public class PsgcAddressDtoValidator : AbstractValidator<PsgcAddressDto>
{
    public PsgcAddressDtoValidator()
    {
        RuleFor(x => x.RegionCode).Must(BeTenDigits).When(x => Has(x.RegionCode)).WithMessage("Region code must be a 10-digit PSGC code.");
        RuleFor(x => x.ProvinceCode).Must(BeTenDigits).When(x => Has(x.ProvinceCode)).WithMessage("Province code must be a 10-digit PSGC code.");
        RuleFor(x => x.CityMunicipalityCode).Must(BeTenDigits).When(x => Has(x.CityMunicipalityCode)).WithMessage("City/municipality code must be a 10-digit PSGC code.");
        RuleFor(x => x.BarangayCode).Must(BeTenDigits).When(x => Has(x.BarangayCode)).WithMessage("Barangay code must be a 10-digit PSGC code.");

        RuleFor(x => x.RegionName).NotEmpty().When(x => Has(x.RegionCode)).MaximumLength(150);
        RuleFor(x => x.ProvinceName).NotEmpty().When(x => Has(x.ProvinceCode)).MaximumLength(150);
        RuleFor(x => x.CityMunicipalityName).NotEmpty().When(x => Has(x.CityMunicipalityCode)).MaximumLength(150);
        RuleFor(x => x.BarangayName).NotEmpty().When(x => Has(x.BarangayCode)).MaximumLength(150);

        RuleFor(x => x.CityMunicipalityCode).NotEmpty().When(x => Has(x.BarangayCode))
            .WithMessage("Choose a city/municipality before a barangay.");
        RuleFor(x => x.RegionCode).NotEmpty().When(x => Has(x.CityMunicipalityCode))
            .WithMessage("Choose a region before a city/municipality.");
        RuleFor(x => x.ZipCode).MaximumLength(10);
    }

    private static bool Has(string? v) => !string.IsNullOrWhiteSpace(v);
    private static bool BeTenDigits(string? v) => v != null && v.Trim().Length == 10 && v.Trim().All(char.IsAsciiDigit);
}

public class LearnerProfileDtoValidator : AbstractValidator<LearnerProfileDto>
{
    public LearnerProfileDtoValidator()
    {
        RuleFor(x => x.ExtensionName).MaximumLength(10);
        RuleFor(x => x.PsaBirthCertificateNumber).MaximumLength(30);
        RuleFor(x => x.PlaceOfBirth).MaximumLength(200);
        RuleFor(x => x.MotherTongue).MaximumLength(100);
        RuleFor(x => x.Religion).MaximumLength(100);
        RuleFor(x => x.IpCommunity).NotEmpty().When(x => x.IsIndigenousPeople)
            .WithMessage("Name the Indigenous Peoples community.").MaximumLength(100);
        RuleFor(x => x.FourPsHouseholdId).NotEmpty().When(x => x.Is4PsBeneficiary)
            .WithMessage("Enter the 4Ps household ID.").MaximumLength(30);
        RuleFor(x => x.DisabilityTypes).NotEmpty().When(x => x.HasDisability)
            .WithMessage("Select at least one disability type.");
        RuleForEach(x => x.DisabilityTypes).Must(t => DepEdLists.DisabilityTypes.Contains(t))
            .When(x => x.HasDisability)
            .WithMessage("'{PropertyValue}' is not a recognized disability type.");
        RuleFor(x => x.FatherLastName).MaximumLength(100);
        RuleFor(x => x.FatherFirstName).MaximumLength(100);
        RuleFor(x => x.FatherMiddleName).MaximumLength(100);
        RuleFor(x => x.FatherContactNumber).MaximumLength(50);
        RuleFor(x => x.MotherMaidenLastName).MaximumLength(100);
        RuleFor(x => x.MotherMaidenFirstName).MaximumLength(100);
        RuleFor(x => x.MotherMaidenMiddleName).MaximumLength(100);
        RuleFor(x => x.MotherContactNumber).MaximumLength(50);
        RuleFor(x => x.HomeAddress!).SetValidator(new PsgcAddressDtoValidator()).When(x => x.HomeAddress != null);
    }
}

public class EnrollmentLearnerInfoDtoValidator : AbstractValidator<EnrollmentLearnerInfoDto>
{
    public EnrollmentLearnerInfoDtoValidator()
    {
        RuleFor(x => x.LearnerType).Must(t => DepEdLists.LearnerTypes.Contains(t!.Trim())).When(x => Has(x.LearnerType))
            .WithMessage("Learner type must be New, Continuing, Transferee, or Returning.");
        RuleFor(x => x.LastGradeLevelCompleted).Must(g => GradeLevels.All.Contains(g!.Trim())).When(x => Has(x.LastGradeLevelCompleted))
            .WithMessage("Choose the last grade level completed from the list.");
        RuleFor(x => x.LastSchoolYearCompleted).Matches("^[0-9]{4}-[0-9]{4}$").When(x => Has(x.LastSchoolYearCompleted))
            .WithMessage("Use the school-year format 2025-2026.");
        RuleFor(x => x.LastSchoolAttended).MaximumLength(200);
        RuleFor(x => x.LastSchoolAddress).MaximumLength(500);
        RuleFor(x => x.LastSchoolId).Matches("^[0-9]{6}$").When(x => Has(x.LastSchoolId))
            .WithMessage("A DepEd School ID is 6 digits.");
        RuleFor(x => x.Track).MaximumLength(100);
        RuleFor(x => x.Strand).MaximumLength(100);
    }

    private static bool Has(string? v) => !string.IsNullOrWhiteSpace(v);
}
```

- [ ] **Step 6: Route every StudentDto through `StudentMapping`**

In each of these files, replace the whole `new StudentDto(...)` expression with `StudentMapping.ToDto(<variable>)` (variable is `student` or `s`), adding `using Enrollify.Application.Features.Students;` where the namespace differs: `CreateStudentCommand.cs`, `SetStudentActiveCommand.cs`, `UpdateChildProfileCommand.cs`, `UpdateMyProfileCommand.cs`, `UpdateStudentCommand.cs`, `GetChildProfileQuery.cs`, `GetMyProfileQuery.cs`, `GetStudentByIdQuery.cs`.

Rewrite `GetStudentsQuery.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Common.Models;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Queries;

public record GetStudentsQuery(
    string? Search, int Page = 1, int PageSize = 20, bool IncludeInactive = false,
    bool MissingLrn = false, bool LrnNeedsReview = false) : IRequest<PagedResult<StudentDto>>;

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, PagedResult<StudentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStudentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Students.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        if (request.MissingLrn)
            query = query.Where(s => s.LRN == null);

        if (request.LrnNeedsReview)
            query = query.Where(s => s.LrnNeedsReview);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(search) ||
                s.LastName.ToLower().Contains(search) ||
                s.StudentNumber.Contains(search) ||
                (s.LRN != null && s.LRN.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Materialize then map: the DTO carries the learner profile (owned address, split
        // disability list), which isn't translatable inside a SQL projection. Pages are small.
        var students = await query
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentDto>
        {
            Items = students.Select(StudentMapping.ToDto).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
```

In `StudentsController.GetAll` add `[FromQuery] bool missingLrn = false, [FromQuery] bool lrnNeedsReview = false` parameters and pass them: `new GetStudentsQuery(search, page, pageSize, includeInactive, missingLrn, lrnNeedsReview)`.

- [ ] **Step 7: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~StudentListLearnerTests"` → PASS.
Run: `dotnet build Enrollify.sln` → 0 warnings. Run full `dotnet test tests/Enrollify.Application.Tests` → all pass.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "Learner record: learner DTOs, mapping, validators; students list filters

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Staff student create/update and Confirm LRN

**Files:**
- Modify: `backend/src/Enrollify.Application/Features/Students/Commands/CreateStudentCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Students/Commands/UpdateStudentCommand.cs`
- Create: `backend/src/Enrollify.Application/Features/Students/Commands/ConfirmLrnCommand.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Students/StudentDto.cs` (request records)
- Modify: `backend/src/Enrollify.API/Controllers/StudentsController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/StaffStudentEditTests.cs`

**Interfaces:**
- Consumes: `LearnerProfileDto`, `LearnerProfileDtoValidator`, `LearnerProfileMapping.Apply`, `StudentMapping.ToDto`, `DepEdLists.IsTwelveDigitLrn` (Tasks 1–3).
- Produces:
  - `CreateStudentCommand(..., Guid? UserId = null, Guid? ParentUserId = null, LearnerProfileDto? Profile = null)`
  - `UpdateStudentCommand(Guid Id, string? LRN, string FirstName, string MiddleName, string LastName, DateTime BirthDate, string? Gender, string Address, string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact, LearnerProfileDto? Profile = null)`
  - `ConfirmLrnCommand(Guid StudentId) : IRequest<StudentDto>`; `POST /api/students/{id}/confirm-lrn` [Admin, Registrar]
  - `CreateStudentRequest` / `UpdateStudentRequest` gain trailing `LearnerProfileDto? Profile = null`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/StaffStudentEditTests.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class StaffStudentEditTests
{
    private static readonly PsgcAddressDto ManilaBarangay = new(
        "1300000000", "NCR", null, null, "1380600000", "City of Manila", "1380601001", "Barangay 1", "1013");

    private static Student Legacy(string? lrn = "L-777", bool flagged = true) => new()
    {
        StudentNumber = "2024-00001", LRN = lrn, LrnNeedsReview = flagged,
        FirstName = "Mika", LastName = "Velasco", BirthDate = new DateTime(2012, 3, 10), Address = "Old line, Manila"
    };

    private static UpdateStudentCommand Update(Student s, string? lrn, string? contact = null, LearnerProfileDto? profile = null) => new(
        s.Id, lrn, s.FirstName, s.MiddleName, s.LastName, s.BirthDate, s.Gender, s.Address,
        contact, s.Email, s.GuardianName, s.GuardianContact, profile);

    [Fact]
    public async Task UnrelatedEdit_KeepsFlag_AndUntouchedLegacyLrn()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "L-777", contact: "0917-000-0000"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("0917-000-0000", reloaded.ContactNumber);
        Assert.Equal("L-777", reloaded.LRN);
        Assert.True(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ChangedLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "136512260001"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("136512260001", reloaded.LRN);
        Assert.False(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ClearingLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "  "), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Null(reloaded.LRN);
        Assert.False(reloaded.LrnNeedsReview);
    }

    [Fact]
    public async Task ChangedLrn_NotTwelveDigits_IsRefused()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateStudentCommandHandler(ctx).Handle(Update(s, "12345"), default));
        Assert.Contains("12 digits", ex.Message);
    }

    [Fact]
    public async Task ChangedLrn_TakenByAnotherStudent_IsRefused()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        ctx.Students.Add(new Student { StudentNumber = "2024-00002", LRN = "136512260001", FirstName = "B", LastName = "C", Address = "" });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateStudentCommandHandler(ctx).Handle(Update(s, "136512260001"), default));
    }

    [Fact]
    public async Task UpdateWithProfile_AppliesLearnerFieldsAndAddress()
    {
        var ctx = TestDb.Create();
        var s = Legacy();
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateStudentCommandHandler(ctx).Handle(Update(s, "L-777",
            profile: new LearnerProfileDto(Religion: "Catholic", HomeAddress: ManilaBarangay)), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("Catholic", reloaded.Religion);
        Assert.Equal("1380601001", reloaded.HomeAddress.BarangayCode);
    }

    [Fact]
    public async Task ConfirmLrn_ClearsFlag()
    {
        var ctx = TestDb.Create();
        var s = Legacy(lrn: "136512260001");
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var dto = await new ConfirmLrnCommandHandler(ctx).Handle(new ConfirmLrnCommand(s.Id), default);

        Assert.False(dto.LrnNeedsReview);
        Assert.False((await ctx.Students.SingleAsync()).LrnNeedsReview);
    }

    [Theory]
    [InlineData(null, "no LRN")]
    [InlineData("L-777", "not 12 digits")]
    public async Task ConfirmLrn_RefusesMissingOrMalformed(string? lrn, string messagePart)
    {
        var ctx = TestDb.Create();
        var s = Legacy(lrn: lrn);
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConfirmLrnCommandHandler(ctx).Handle(new ConfirmLrnCommand(s.Id), default));
        Assert.Contains(messagePart, ex.Message);
    }

    [Fact]
    public void CreateValidator_RequiresPsgcCityAndBarangay()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "12 Mabini St", null, null, null, null));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Choose the learner's city/municipality and barangay.");
    }

    [Fact]
    public void CreateValidator_AcceptsPsgcAddressWithoutStreetLine()
    {
        var result = new CreateStudentCommandValidator().Validate(new CreateStudentCommand(
            null, "Ana", "", "Reyes", new DateTime(2012, 1, 1), null, "", null, null, null, null,
            Profile: new LearnerProfileDto(HomeAddress: ManilaBarangay)));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void UpdateValidator_AllowsLegacyAddressLineWithoutPsgc()
    {
        var s = Legacy();
        Assert.True(new UpdateStudentCommandValidator().Validate(Update(s, "L-777")).IsValid);
    }

    [Fact]
    public void UpdateValidator_RequiresEitherPsgcOrAddressLine()
    {
        var s = Legacy();
        s.Address = "";
        var result = new UpdateStudentCommandValidator().Validate(Update(s, "L-777"));
        Assert.Contains(result.Errors, e => e.PropertyName == "Address");
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~StaffStudentEditTests"` → build FAILS.

- [ ] **Step 2: Extend `CreateStudentCommand`**

1. Add `using Enrollify.Application.DTOs.Learners;`.
2. Record: append `, LearnerProfileDto? Profile = null` after `Guid? ParentUserId = null   // ...` (keep the comment).
3. Validator: replace `RuleFor(x => x.Address).NotEmpty().MaximumLength(500);` with:

```csharp
        // Staff-created learners must be PSGC-resolved down to the barangay (SF1 needs it);
        // the free-text line is now just house/street and may be blank (rural sitios).
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Profile)
            .Must(p => !string.IsNullOrWhiteSpace(p?.HomeAddress?.CityMunicipalityCode) && !string.IsNullOrWhiteSpace(p.HomeAddress.BarangayCode))
            .WithMessage("Choose the learner's city/municipality and barangay.");
        RuleFor(x => x.Profile!).SetValidator(new LearnerProfileDtoValidator()).When(x => x.Profile != null);
```

4. Handler: right after `_context.Students.Add(student);` insert:

```csharp
        if (request.Profile != null)
            LearnerProfileMapping.Apply(request.Profile, student);
```

- [ ] **Step 3: Rewrite `UpdateStudentCommand`**

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record UpdateStudentCommand(
    Guid Id, string? LRN, string FirstName, string MiddleName, string LastName,
    DateTime BirthDate, string? Gender, string Address,
    string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact,
    LearnerProfileDto? Profile = null
) : IRequest<StudentDto>;

public class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // Shape is checked in the handler, and only when the LRN CHANGES — an untouched legacy
        // value must never block an unrelated edit.
        RuleFor(x => x.LRN).MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        // DELIBERATE data-quality ratchet on the STAFF path: a staff save needs either a
        // PSGC-matched address or (for legacy records not matched yet) the free-text line.
        // Do not relax this to match the public application form.
        RuleFor(x => x.Address).NotEmpty().When(x => !IsPsgcMatched(x.Profile))
            .WithMessage("Enter the address, or choose the city/municipality and barangay.")
            .MaximumLength(500);
        RuleFor(x => x.Profile!).SetValidator(new LearnerProfileDtoValidator()).When(x => x.Profile != null);
    }

    private static bool IsPsgcMatched(LearnerProfileDto? p) =>
        !string.IsNullOrWhiteSpace(p?.HomeAddress?.CityMunicipalityCode) && !string.IsNullOrWhiteSpace(p.HomeAddress.BarangayCode);
}

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateStudentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Student with ID '{request.Id}' not found.");

        var newLrn = string.IsNullOrWhiteSpace(request.LRN) ? null : request.LRN.Trim();
        if (newLrn != student.LRN)
        {
            if (newLrn != null && !DepEdLists.IsTwelveDigitLrn(newLrn))
                throw new InvalidOperationException("LRN must be exactly 12 digits (leave it blank if the learner has none yet).");
            if (newLrn != null && await _context.Students.AnyAsync(s => s.Id != student.Id && s.LRN == newLrn, cancellationToken))
                throw new InvalidOperationException($"A student with LRN '{newLrn}' already exists.");
            student.LRN = newLrn;
            student.LrnNeedsReview = false; // the registrar just set it deliberately
        }

        student.FirstName = request.FirstName;
        student.MiddleName = request.MiddleName;
        student.LastName = request.LastName;
        student.BirthDate = request.BirthDate;
        student.Gender = request.Gender;
        student.Address = request.Address;
        student.ContactNumber = request.ContactNumber;
        student.Email = request.Email;
        student.GuardianName = request.GuardianName;
        student.GuardianContact = request.GuardianContact;

        if (request.Profile != null)
            LearnerProfileMapping.Apply(request.Profile, student);

        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
```

- [ ] **Step 4: Create `ConfirmLrnCommand`**

`backend/src/Enrollify.Application/Features/Students/Commands/ConfirmLrnCommand.cs`:

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>
/// Registrar confirms a pre-migration LRN is the learner's real DepEd LRN — clears the
/// "Check LRN" flag. Only a well-formed 12-digit LRN can be confirmed; anything else must be
/// corrected through the edit form first.
/// </summary>
public record ConfirmLrnCommand(Guid StudentId) : IRequest<StudentDto>;

public class ConfirmLrnCommandHandler : IRequestHandler<ConfirmLrnCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public ConfirmLrnCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(ConfirmLrnCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (student.LRN == null)
            throw new InvalidOperationException("This student has no LRN to confirm.");
        if (!DepEdLists.IsTwelveDigitLrn(student.LRN))
            throw new InvalidOperationException($"LRN '{student.LRN}' is not 12 digits — correct it in the edit form before confirming.");

        student.LrnNeedsReview = false;
        await _context.SaveChangesAsync(cancellationToken);
        return StudentMapping.ToDto(student);
    }
}
```

- [ ] **Step 5: Request DTOs and controller**

In `StudentDto.cs`, append `, LearnerProfileDto? Profile = null` as the last parameter of both `CreateStudentRequest` and `UpdateStudentRequest`.

In `StudentsController.cs`:
- `Create`: pass `request.Profile` as the `Profile:` named argument: `new CreateStudentCommand(request.LRN, ..., request.GuardianContact, Profile: request.Profile)`.
- `Update`: `new UpdateStudentCommand(id, request.LRN, ..., request.GuardianContact, request.Profile)`.
- Add:

```csharp
    [HttpPost("{id:guid}/confirm-lrn")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> ConfirmLrn(Guid id)
    {
        var result = await _sender.Send(new ConfirmLrnCommand(id));
        return Ok(result);
    }
```

- [ ] **Step 6: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~StaffStudentEditTests"` → PASS. Full build (0 warnings) and full test run → all pass.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "Learner record: staff create/update with DepEd profile, Confirm LRN

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Family self-service profile updates

**Files:**
- Modify: `backend/src/Enrollify.Application/Features/Students/Commands/UpdateChildProfileCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Students/Commands/UpdateMyProfileCommand.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Students/StudentDto.cs` (`UpdateChildProfileRequest`, `UpdateMyProfileRequest`)
- Modify: `backend/src/Enrollify.API/Controllers/ParentController.cs`, `StudentsController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/FamilyProfileUpdateTests.cs`

**Interfaces:**
- Consumes: `PsgcAddressDto`, `PsgcAddressDtoValidator`, `LearnerProfileMapping.ApplyAddress`, `StudentMapping.ToDto`.
- Produces: both commands gain trailing `PsgcAddressDto? HomeAddress = null, string? FatherContactNumber = null, string? MotherContactNumber = null`; both request records gain the same three trailing properties. Families can never change DepEd identity fields (there are no parameters for them).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/FamilyProfileUpdateTests.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class FamilyProfileUpdateTests
{
    private static readonly PsgcAddressDto QcBarangay = new(
        "1300000000", "NCR", null, null, "1381300000", "Quezon City", "1381300001", "Alicia", "1105");

    [Fact]
    public async Task Parent_UpdatesAddressAndParentContacts_IdentityFieldsUntouched()
    {
        var ctx = TestDb.Create();
        var parentId = Guid.NewGuid();
        var s = new Student
        {
            StudentNumber = "2026-00001", FirstName = "Ana", LastName = "Cruz", Address = "old",
            ParentUserId = parentId, PsaBirthCertificateNumber = "PSA-1", Religion = "Catholic"
        };
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateChildProfileCommandHandler(ctx).Handle(new UpdateChildProfileCommand(
            s.Id, parentId, null, null, "12 Mabini St", null, null,
            HomeAddress: QcBarangay, FatherContactNumber: "0917-111-1111", MotherContactNumber: "0917-222-2222"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("1381300001", reloaded.HomeAddress.BarangayCode);
        Assert.Equal("12 Mabini St", reloaded.Address);
        Assert.Equal("0917-111-1111", reloaded.FatherContactNumber);
        Assert.Equal("PSA-1", reloaded.PsaBirthCertificateNumber);
        Assert.Equal("Catholic", reloaded.Religion);
    }

    [Fact]
    public async Task OmittedAddress_LeavesExistingAddressAlone()
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2026-00002", FirstName = "Ben", LastName = "Uy", Address = "", UserId = userId };
        s.HomeAddress.BarangayCode = "1381300001";
        s.HomeAddress.CityMunicipalityCode = "1381300000";
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateMyProfileCommandHandler(ctx).Handle(new UpdateMyProfileCommand(
            userId, "0917-000-0000", null, null, null, null), default);

        Assert.Equal("1381300001", (await ctx.Students.SingleAsync()).HomeAddress.BarangayCode);
    }

    [Fact]
    public void Validator_RejectsMalformedPsgcCode()
    {
        var result = new UpdateChildProfileCommandValidator().Validate(new UpdateChildProfileCommand(
            Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null,
            HomeAddress: new PsgcAddressDto(RegionCode: "13", RegionName: "NCR")));
        Assert.False(result.IsValid);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~FamilyProfileUpdateTests"` → build FAILS.

- [ ] **Step 2: Update `UpdateChildProfileCommand`**

Replace the file with:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>
/// Parent edits their child's contact details and home address. DepEd identity fields
/// (LRN, PSA no., IP/4Ps/disability, parents' names) are registrar-only — no parameters here.
/// Null means "unchanged" for every field.
/// </summary>
public record UpdateChildProfileCommand(
    Guid StudentId,
    Guid ParentUserId,
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact,
    PsgcAddressDto? HomeAddress = null,
    string? FatherContactNumber = null,
    string? MotherContactNumber = null) : IRequest<StudentDto>;

public class UpdateChildProfileCommandValidator : AbstractValidator<UpdateChildProfileCommand>
{
    public UpdateChildProfileCommandValidator()
    {
        RuleFor(x => x.ContactNumber).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.GuardianName).MaximumLength(200);
        RuleFor(x => x.GuardianContact).MaximumLength(50);
        RuleFor(x => x.FatherContactNumber).MaximumLength(50);
        RuleFor(x => x.MotherContactNumber).MaximumLength(50);
        RuleFor(x => x.HomeAddress!).SetValidator(new PsgcAddressDtoValidator()).When(x => x.HomeAddress != null);
    }
}

public class UpdateChildProfileCommandHandler : IRequestHandler<UpdateChildProfileCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateChildProfileCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(UpdateChildProfileCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        if (request.ContactNumber != null) student.ContactNumber = request.ContactNumber;
        if (request.Email != null) student.Email = request.Email;
        if (request.Address != null) student.Address = request.Address;
        if (request.GuardianName != null) student.GuardianName = request.GuardianName;
        if (request.GuardianContact != null) student.GuardianContact = request.GuardianContact;
        if (request.FatherContactNumber != null) student.FatherContactNumber = LearnerProfileMapping.Clean(request.FatherContactNumber);
        if (request.MotherContactNumber != null) student.MotherContactNumber = LearnerProfileMapping.Clean(request.MotherContactNumber);
        if (request.HomeAddress != null) LearnerProfileMapping.ApplyAddress(request.HomeAddress, student.HomeAddress);

        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
```

- [ ] **Step 3: Update `UpdateMyProfileCommand` the same way**

Replace the file with:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>Student edits their own contact details and home address (null = unchanged).</summary>
public record UpdateMyProfileCommand(
    Guid UserId,
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact,
    PsgcAddressDto? HomeAddress = null,
    string? FatherContactNumber = null,
    string? MotherContactNumber = null) : IRequest<StudentDto>;

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.ContactNumber).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.GuardianName).MaximumLength(200);
        RuleFor(x => x.GuardianContact).MaximumLength(50);
        RuleFor(x => x.FatherContactNumber).MaximumLength(50);
        RuleFor(x => x.MotherContactNumber).MaximumLength(50);
        RuleFor(x => x.HomeAddress!).SetValidator(new PsgcAddressDtoValidator()).When(x => x.HomeAddress != null);
    }
}

public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateMyProfileCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (request.ContactNumber != null) student.ContactNumber = request.ContactNumber;
        if (request.Email != null) student.Email = request.Email;
        if (request.Address != null) student.Address = request.Address;
        if (request.GuardianName != null) student.GuardianName = request.GuardianName;
        if (request.GuardianContact != null) student.GuardianContact = request.GuardianContact;
        if (request.FatherContactNumber != null) student.FatherContactNumber = LearnerProfileMapping.Clean(request.FatherContactNumber);
        if (request.MotherContactNumber != null) student.MotherContactNumber = LearnerProfileMapping.Clean(request.MotherContactNumber);
        if (request.HomeAddress != null) LearnerProfileMapping.ApplyAddress(request.HomeAddress, student.HomeAddress);

        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
```

- [ ] **Step 4: Request records and controllers**

In `StudentDto.cs` append to both `UpdateMyProfileRequest` and `UpdateChildProfileRequest`:
`, PsgcAddressDto? HomeAddress = null, string? FatherContactNumber = null, string? MotherContactNumber = null`

`ParentController.UpdateChildProfile`: pass `request.HomeAddress, request.FatherContactNumber, request.MotherContactNumber` as the three trailing arguments. `StudentsController.UpdateMyProfile`: same three trailing arguments.

- [ ] **Step 5: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~FamilyProfileUpdateTests"` → PASS. Full build (0 warnings) + full tests → pass.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "Learner record: families update PSGC address and parent contacts

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: SHS strand catalog

**Files:**
- Create: `backend/src/Enrollify.Application/DTOs/ShsStrands/ShsStrandDto.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/DefaultShsStrands.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/ShsStrandRules.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/Commands/CreateShsStrandCommand.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/Commands/UpdateShsStrandCommand.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/Queries/GetShsStrandsQuery.cs`
- Create: `backend/src/Enrollify.Application/Features/ShsStrands/Queries/GetShsStrandsByTenantQuery.cs`
- Create: `backend/src/Enrollify.API/Controllers/ShsStrandsController.cs`
- Modify: `backend/src/Enrollify.API/Controllers/SchoolsController.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs`
- Modify: `backend/src/Enrollify.Application/Features/Tenants/Commands/CreateTenantCommand.cs`
- Test: `backend/tests/Enrollify.Application.Tests/ShsStrandTests.cs`

**Interfaces:**
- Consumes: `ShsStrand`, `IApplicationDbContext.ShsStrands`, `DepEdLists.IsShsGrade` (Task 1).
- Produces:
  - `ShsStrandDto(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder)`; `CreateShsStrandRequest(string Track, string Strand, int DisplayOrder = 0)`; `UpdateShsStrandRequest(string Track, string Strand, bool IsActive, int DisplayOrder)`
  - `DefaultShsStrands.EnsureForTenantAsync(IApplicationDbContext, Guid tenantId, CancellationToken = default) : Task<int>` (caller saves)
  - `ShsStrandRules.ResolveAsync(IApplicationDbContext, string gradeLevel, string? track, string? strand, CancellationToken, Guid? tenantId = null) : Task<(string? Track, string? Strand)>` — returns catalog casing; `(null, null)` when both blank; throws `InvalidOperationException` otherwise-invalid
  - `ShsStrandRules.IsStrandRequiredAsync(IApplicationDbContext, string gradeLevel, CancellationToken, Guid? tenantId = null) : Task<bool>`
  - `GetShsStrandsQuery(bool IncludeInactive = false)`, `GetShsStrandsByTenantQuery(Guid TenantId)` (active only), `CreateShsStrandCommand(string Track, string Strand, int DisplayOrder)`, `UpdateShsStrandCommand(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder)`
  - HTTP: `GET /api/shs-strands?includeInactive=` (any signed-in role; inactive only for Admin/Registrar), `POST /api/shs-strands`, `PUT /api/shs-strands/{id}` (Admin, Registrar), `GET /api/schools/{slug}/shs-strands` (anonymous, active only)

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/ShsStrandTests.cs`:

```csharp
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Application.Features.ShsStrands.Commands;
using Enrollify.Application.Features.ShsStrands.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ShsStrandTests
{
    private static async Task<ApplicationDbContext> WithStemAsync(bool active = true)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = active, DisplayOrder = 10 });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Ensure_SeedsDefaultsOnce()
    {
        var ctx = TestDb.Create();
        Assert.Equal(10, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();

        Assert.Equal(10, await ctx.ShsStrands.CountAsync());
        Assert.True(await ctx.ShsStrands.AnyAsync(s => s.Track == "Academic" && s.Strand == "STEM"));
    }

    [Fact]
    public async Task Ensure_DoesNotReseed_WhenEveryStrandWasDeactivated()
    {
        var ctx = await WithStemAsync(active: false);
        Assert.Equal(0, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task Create_RefusesCaseInsensitiveDuplicate()
    {
        var ctx = await WithStemAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateShsStrandCommandHandler(ctx).Handle(new CreateShsStrandCommand(" academic ", "stem", 20), default));
    }

    [Fact]
    public async Task Update_RefusesDuplicateOfAnotherRow_ButAllowsSelf()
    {
        var ctx = await WithStemAsync();
        var abm = new ShsStrand { Track = "Academic", Strand = "ABM", IsActive = true };
        ctx.ShsStrands.Add(abm);
        await ctx.SaveChangesAsync();
        var handler = new UpdateShsStrandCommandHandler(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateShsStrandCommand(abm.Id, "Academic", "STEM", true, 0), default));
        var dto = await handler.Handle(new UpdateShsStrandCommand(abm.Id, "Academic", "ABM", false, 5), default);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Resolve_BlankPair_ReturnsNulls()
    {
        var ctx = await WithStemAsync();
        var (track, strand) = await ShsStrandRules.ResolveAsync(ctx, "Grade 11", " ", null, default);
        Assert.Null(track);
        Assert.Null(strand);
    }

    [Fact]
    public async Task Resolve_NormalizesToCatalogCasing()
    {
        var ctx = await WithStemAsync();
        Assert.Equal(("Academic", "STEM"), await ShsStrandRules.ResolveAsync(ctx, "Grade 12", "academic", " stem ", default));
    }

    [Theory]
    [InlineData("Grade 8", "Academic", "STEM", "only applies to Grade 11 and Grade 12")]
    [InlineData("Grade 11", "Academic", null, "Choose both a track and a strand")]
    [InlineData("Grade 11", "TVL", "ICT", "not a strand this school currently offers")]
    public async Task Resolve_RefusesInvalidPairs(string grade, string? track, string? strand, string message)
    {
        var ctx = await WithStemAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ShsStrandRules.ResolveAsync(ctx, grade, track, strand, default));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task Resolve_InactiveStrand_IsRefused()
    {
        var ctx = await WithStemAsync(active: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ShsStrandRules.ResolveAsync(ctx, "Grade 11", "Academic", "STEM", default));
    }

    [Fact]
    public async Task Resolve_ExplicitTenant_IgnoresTheAmbientTenant()
    {
        var ctx = TestDb.Create();
        var otherTenant = Guid.NewGuid();
        ctx.ShsStrands.Add(new ShsStrand { TenantId = otherTenant, Track = "Academic", Strand = "HUMSS", IsActive = true });
        await ctx.SaveChangesAsync();

        Assert.Equal(("Academic", "HUMSS"),
            await ShsStrandRules.ResolveAsync(ctx, "Grade 11", "Academic", "HUMSS", default, otherTenant));
    }

    [Fact]
    public async Task IsStrandRequired_OnlyForShsGradesWithActiveStrands()
    {
        var active = await WithStemAsync();
        Assert.True(await ShsStrandRules.IsStrandRequiredAsync(active, "Grade 11", default));
        Assert.False(await ShsStrandRules.IsStrandRequiredAsync(active, "Grade 10", default));

        var switchedOff = await WithStemAsync(active: false);
        Assert.False(await ShsStrandRules.IsStrandRequiredAsync(switchedOff, "Grade 11", default));
    }

    [Fact]
    public async Task PublicQuery_ReturnsOnlyActiveStrandsOfThatTenant()
    {
        var ctx = await WithStemAsync();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "GAS", IsActive = false });
        ctx.ShsStrands.Add(new ShsStrand { TenantId = Guid.NewGuid(), Track = "TVL", Strand = "ICT", IsActive = true });
        await ctx.SaveChangesAsync();

        var list = await new GetShsStrandsByTenantQueryHandler(ctx).Handle(new GetShsStrandsByTenantQuery(TestDb.TenantId), default);

        Assert.Equal("STEM", Assert.Single(list).Strand);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~ShsStrandTests"` → build FAILS.

- [ ] **Step 2: DTOs**

`backend/src/Enrollify.Application/DTOs/ShsStrands/ShsStrandDto.cs`:

```csharp
namespace Enrollify.Application.DTOs.ShsStrands;

public record ShsStrandDto(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder);

public record CreateShsStrandRequest(string Track, string Strand, int DisplayOrder = 0);

public record UpdateShsStrandRequest(string Track, string Strand, bool IsActive, int DisplayOrder);
```

- [ ] **Step 3: Defaults and rules**

`backend/src/Enrollify.Application/Features/ShsStrands/DefaultShsStrands.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands;

/// <summary>
/// The default SHS offerings (current DepEd tracks/strands). Seeded only for a tenant with NO
/// strand rows at all — strands are deactivated, never deleted, so an empty catalog means
/// "never seeded" and a school that switched SHS off is left alone. Caller saves.
/// </summary>
public static class DefaultShsStrands
{
    private static readonly (string Track, string Strand)[] Defaults =
    {
        ("Academic", "STEM"), ("Academic", "ABM"), ("Academic", "HUMSS"), ("Academic", "GAS"),
        ("TVL", "ICT"), ("TVL", "HE"), ("TVL", "IA"), ("TVL", "AFA"),
        ("Sports", "Sports"), ("Arts and Design", "Arts and Design"),
    };

    public static async Task<int> EnsureForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var any = await context.ShsStrands.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct);
        if (any) return 0;

        for (var i = 0; i < Defaults.Length; i++)
        {
            context.ShsStrands.Add(new ShsStrand
            {
                TenantId = tenantId,
                Track = Defaults[i].Track,
                Strand = Defaults[i].Strand,
                IsActive = true,
                DisplayOrder = (i + 1) * 10
            });
        }
        return Defaults.Length;
    }
}
```

`backend/src/Enrollify.Application/Features/ShsStrands/ShsStrandRules.cs`:

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands;

/// <summary>
/// The one definition of a valid enrollment track/strand. Tenant scoping comes from the normal
/// query filter, or from an explicit <c>tenantId</c> for the slug-based public apply flow.
/// </summary>
public static class ShsStrandRules
{
    /// <returns>The pair in catalog casing, or (null, null) when both are blank.</returns>
    /// <exception cref="InvalidOperationException">Strand on a non-SHS grade, half a pair, or a pair the school doesn't currently offer.</exception>
    public static async Task<(string? Track, string? Strand)> ResolveAsync(
        IApplicationDbContext context, string gradeLevel, string? track, string? strand, CancellationToken ct, Guid? tenantId = null)
    {
        var t = string.IsNullOrWhiteSpace(track) ? null : track.Trim();
        var s = string.IsNullOrWhiteSpace(strand) ? null : strand.Trim();
        if (t == null && s == null) return (null, null);

        if (!DepEdLists.IsShsGrade(gradeLevel))
            throw new InvalidOperationException($"Track/strand only applies to Grade 11 and Grade 12 (this is {gradeLevel}).");
        if (t == null || s == null)
            throw new InvalidOperationException("Choose both a track and a strand.");

        var tl = t.ToLower();
        var sl = s.ToLower();
        var match = await Catalog(context, tenantId)
            .FirstOrDefaultAsync(x => x.IsActive && x.Track.ToLower() == tl && x.Strand.ToLower() == sl, ct)
            ?? throw new InvalidOperationException($"'{t} – {s}' is not a strand this school currently offers.");
        return (match.Track, match.Strand);
    }

    /// <summary>True when the grade is SHS and the school offers at least one active strand.</summary>
    public static async Task<bool> IsStrandRequiredAsync(IApplicationDbContext context, string gradeLevel, CancellationToken ct, Guid? tenantId = null) =>
        DepEdLists.IsShsGrade(gradeLevel) && await Catalog(context, tenantId).AnyAsync(x => x.IsActive, ct);

    private static IQueryable<ShsStrand> Catalog(IApplicationDbContext context, Guid? tenantId) =>
        tenantId == null
            ? context.ShsStrands
            : context.ShsStrands.IgnoreQueryFilters().Where(x => x.TenantId == tenantId.Value);
}
```

- [ ] **Step 4: Commands and queries**

`backend/src/Enrollify.Application/Features/ShsStrands/Commands/CreateShsStrandCommand.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Commands;

public record CreateShsStrandCommand(string Track, string Strand, int DisplayOrder) : IRequest<ShsStrandDto>;

public class CreateShsStrandCommandValidator : AbstractValidator<CreateShsStrandCommand>
{
    public CreateShsStrandCommandValidator()
    {
        RuleFor(x => x.Track).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Strand).NotEmpty().MaximumLength(100);
    }
}

public class CreateShsStrandCommandHandler : IRequestHandler<CreateShsStrandCommand, ShsStrandDto>
{
    private readonly IApplicationDbContext _context;

    public CreateShsStrandCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShsStrandDto> Handle(CreateShsStrandCommand request, CancellationToken cancellationToken)
    {
        var track = request.Track.Trim();
        var strand = request.Strand.Trim();
        var tl = track.ToLower();
        var sl = strand.ToLower();
        if (await _context.ShsStrands.AnyAsync(s => s.Track.ToLower() == tl && s.Strand.ToLower() == sl, cancellationToken))
            throw new InvalidOperationException($"'{track} – {strand}' already exists. Reactivate it instead of adding it again.");

        var row = new ShsStrand { Track = track, Strand = strand, IsActive = true, DisplayOrder = request.DisplayOrder };
        _context.ShsStrands.Add(row);
        await _context.SaveChangesAsync(cancellationToken);
        return new ShsStrandDto(row.Id, row.Track, row.Strand, row.IsActive, row.DisplayOrder);
    }
}
```

`backend/src/Enrollify.Application/Features/ShsStrands/Commands/UpdateShsStrandCommand.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Commands;

/// <summary>
/// Renames, reorders, or (de)activates a strand. Existing enrollments keep the Track/Strand
/// strings they were saved with — this never rewrites history.
/// </summary>
public record UpdateShsStrandCommand(Guid Id, string Track, string Strand, bool IsActive, int DisplayOrder) : IRequest<ShsStrandDto>;

public class UpdateShsStrandCommandValidator : AbstractValidator<UpdateShsStrandCommand>
{
    public UpdateShsStrandCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Track).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Strand).NotEmpty().MaximumLength(100);
    }
}

public class UpdateShsStrandCommandHandler : IRequestHandler<UpdateShsStrandCommand, ShsStrandDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateShsStrandCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ShsStrandDto> Handle(UpdateShsStrandCommand request, CancellationToken cancellationToken)
    {
        var row = await _context.ShsStrands.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Strand not found.");

        var track = request.Track.Trim();
        var strand = request.Strand.Trim();
        var tl = track.ToLower();
        var sl = strand.ToLower();
        if (await _context.ShsStrands.AnyAsync(s => s.Id != row.Id && s.Track.ToLower() == tl && s.Strand.ToLower() == sl, cancellationToken))
            throw new InvalidOperationException($"'{track} – {strand}' already exists.");

        row.Track = track;
        row.Strand = strand;
        row.IsActive = request.IsActive;
        row.DisplayOrder = request.DisplayOrder;
        await _context.SaveChangesAsync(cancellationToken);
        return new ShsStrandDto(row.Id, row.Track, row.Strand, row.IsActive, row.DisplayOrder);
    }
}
```

`backend/src/Enrollify.Application/Features/ShsStrands/Queries/GetShsStrandsQuery.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Queries;

public record GetShsStrandsQuery(bool IncludeInactive = false) : IRequest<List<ShsStrandDto>>;

public class GetShsStrandsQueryHandler : IRequestHandler<GetShsStrandsQuery, List<ShsStrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShsStrandsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ShsStrandDto>> Handle(GetShsStrandsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ShsStrands.AsQueryable();
        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Track).ThenBy(s => s.Strand)
            .Select(s => new ShsStrandDto(s.Id, s.Track, s.Strand, s.IsActive, s.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
```

`backend/src/Enrollify.Application/Features/ShsStrands/Queries/GetShsStrandsByTenantQuery.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Queries;

/// <summary>
/// Active strands for the public, slug-based /apply flow. TenantId comes from the school slug,
/// so the query bypasses the global filter and scopes explicitly (like GetSchoolYearsByTenantQuery).
/// </summary>
public record GetShsStrandsByTenantQuery(Guid TenantId) : IRequest<List<ShsStrandDto>>;

public class GetShsStrandsByTenantQueryHandler : IRequestHandler<GetShsStrandsByTenantQuery, List<ShsStrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShsStrandsByTenantQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ShsStrandDto>> Handle(GetShsStrandsByTenantQuery request, CancellationToken cancellationToken)
    {
        return await _context.ShsStrands
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == request.TenantId && s.IsActive)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Track).ThenBy(s => s.Strand)
            .Select(s => new ShsStrandDto(s.Id, s.Track, s.Strand, s.IsActive, s.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Controller + public endpoint**

`backend/src/Enrollify.API/Controllers/ShsStrandsController.cs`:

```csharp
using Enrollify.Application.DTOs.ShsStrands;
using Enrollify.Application.Features.ShsStrands.Commands;
using Enrollify.Application.Features.ShsStrands.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/shs-strands")]
[Authorize]
public class ShsStrandsController : ControllerBase
{
    private readonly ISender _sender;

    public ShsStrandsController(ISender sender) => _sender = sender;

    // Any signed-in role in the tenant: families need the active list to re-enroll into SHS.
    // Inactive rows are a staff concern only.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var staff = User.IsInRole("Admin") || User.IsInRole("Registrar");
        var result = await _sender.Send(new GetShsStrandsQuery(includeInactive && staff));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreateShsStrandRequest request)
    {
        var result = await _sender.Send(new CreateShsStrandCommand(request.Track, request.Strand, request.DisplayOrder));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShsStrandRequest request)
    {
        var result = await _sender.Send(new UpdateShsStrandCommand(id, request.Track, request.Strand, request.IsActive, request.DisplayOrder));
        return Ok(result);
    }
}
```

In `SchoolsController.cs` add `using Enrollify.Application.Features.ShsStrands.Queries;` and:

```csharp
    [HttpGet("shs-strands")]
    public async Task<IActionResult> GetShsStrands(string slug)
    {
        var school = await _sender.Send(new GetTenantBySlugQuery(slug));
        var result = await _sender.Send(new GetShsStrandsByTenantQuery(school.Id));
        return Ok(result);
    }
```

- [ ] **Step 6: Seed per tenant + new-tenant defaults**

In `ApplicationDbContextSeed.SeedAsync`, add `await SeedShsStrandsAsync(context);` right after `await SeedApplicationFormFieldsAsync(context);`, and add the method beside `SeedApplicationFormFieldsAsync` (add `using Enrollify.Application.Features.ShsStrands;`):

```csharp
    private static async Task SeedShsStrandsAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        var added = 0;
        foreach (var tenantId in tenantIds)
            added += await DefaultShsStrands.EnsureForTenantAsync(context, tenantId);
        if (added > 0) await context.SaveChangesAsync();
    }
```

In `CreateTenantCommand.cs` add `using Enrollify.Application.Features.ShsStrands;`, and in `SeedDefaultsForTenantAsync` add before the final `await ctx.SaveChangesAsync(ct);`:

```csharp
        await DefaultShsStrands.EnsureForTenantAsync(ctx, tenant.Id, ct);
```

- [ ] **Step 7: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~ShsStrandTests"` → PASS. Full build (0 warnings) + full tests → pass.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "Learner record: per-school SHS track/strand catalog and rules

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: One requirements builder, with documents kept on file

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequirements.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/RequirementTemplates/RequirementTemplateDto.cs` (the file holding `RequirementTemplateDto` and the two request records)
- Modify: `backend/src/Enrollify.Application/Features/RequirementTemplates/Commands/CreateRequirementTemplateCommand.cs`, `UpdateRequirementTemplateCommand.cs`, `Queries/GetRequirementTemplatesQuery.cs`
- Modify: `backend/src/Enrollify.API/Controllers/RequirementTemplatesController.cs`
- Modify (replace template loops): `Features/Admissions/Commands/ReviewApplicationCommand.cs`, `Features/Enrollments/Commands/CreateEnrollmentCommand.cs`, `RequestEnrollmentCommand.cs`, `StudentEnrollmentCommands.cs` (`StudentRequestEnrollmentCommandHandler`), `BulkReenrollCommand.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs` (`SeedRequirementTemplatesAsync`), `Features/Tenants/Commands/CreateTenantCommand.cs` (default templates)
- Test: `backend/tests/Enrollify.Application.Tests/EnrollmentRequirementsTests.cs`

**Interfaces:**
- Consumes: `RequirementTemplate.KeepOnFile` (Task 1).
- Produces:
  - `EnrollmentRequirements.AddForAsync(IApplicationDbContext, Enrollment, CancellationToken, Guid? tenantId = null) : Task`
  - `EnrollmentRequirements.AddForManyAsync(IApplicationDbContext, IReadOnlyCollection<Enrollment>, CancellationToken, Guid? tenantId = null) : Task` (one query for all on-file lookups)
  - `RequirementTemplateDto(..., int DisplayOrder, bool KeepOnFile = false)`; `CreateRequirementTemplateRequest(..., int DisplayOrder = 0, bool KeepOnFile = false)`; `UpdateRequirementTemplateRequest(..., int DisplayOrder, bool KeepOnFile = false)`; commands gain trailing `bool KeepOnFile = false`.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/EnrollmentRequirementsTests.cs`:

```csharp
using Enrollify.Application.Features.Enrollments;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentRequirementsTests
{
    private static readonly DateTime VerifiedOn = new(2025, 6, 3, 2, 0, 0, DateTimeKind.Utc);

    private static async Task<(ApplicationDbContext Ctx, Student Student)> SeedAsync(
        bool verified = true, EnrollmentStatus priorStatus = EnrollmentStatus.Enrolled, string priorDocName = "PSA Birth Certificate")
    {
        var ctx = TestDb.Create();
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, KeepOnFile = true, DisplayOrder = 1 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Form 138", IsActive = true, DisplayOrder = 2 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Grade 11 Waiver", GradeLevel = "Grade 11", IsActive = true, DisplayOrder = 3 });

        var student = new Student { StudentNumber = "2025-00001", FirstName = "Ana", LastName = "Cruz", Address = "" };
        ctx.Students.Add(student);
        var prior = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = priorStatus };
        ctx.Enrollments.Add(prior);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = priorDocName, IsSubmitted = true, IsVerified = verified,
            FileName = "psa.pdf", Notes = "/api/files/abc", VerifiedBy = verified ? "registrar@mshs.edu.ph" : null,
            VerifiedAt = verified ? VerifiedOn : null
        });
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = "Form 138", IsSubmitted = true, IsVerified = true, FileName = "f138.pdf"
        });
        await ctx.SaveChangesAsync();
        return (ctx, student);
    }

    private static async Task<List<EnrollmentRequirement>> AddForNewAsync(ApplicationDbContext ctx, Student student, string grade = "Grade 8")
    {
        var next = new Enrollment { StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = grade, Status = EnrollmentStatus.Draft };
        ctx.Enrollments.Add(next);
        await EnrollmentRequirements.AddForAsync(ctx, next, default);
        await ctx.SaveChangesAsync();
        return await ctx.EnrollmentRequirements.Where(r => r.EnrollmentId == next.Id).OrderBy(r => r.DocumentName).ToListAsync();
    }

    [Fact]
    public async Task VerifiedKeepOnFileDocument_IsCarriedOver()
    {
        var (ctx, student) = await SeedAsync();
        var reqs = await AddForNewAsync(ctx, student);

        var psa = reqs.Single(r => r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsSubmitted);
        Assert.True(psa.IsVerified);
        Assert.Equal("psa.pdf", psa.FileName);
        Assert.Equal("/api/files/abc", psa.Notes);
        Assert.Equal("registrar@mshs.edu.ph", psa.VerifiedBy);
        Assert.Equal("On file — verified Jun 3, 2025 (SY 2025-2026)", psa.ReviewNotes);
    }

    [Fact]
    public async Task NonKeepOnFileDocument_StartsFresh_EvenIfVerifiedBefore()
    {
        var (ctx, student) = await SeedAsync();
        var reqs = await AddForNewAsync(ctx, student);

        var f138 = reqs.Single(r => r.DocumentName == "Form 138");
        Assert.False(f138.IsSubmitted);
        Assert.Null(f138.FileName);
    }

    [Fact]
    public async Task UnverifiedCopy_IsNotCarried()
    {
        var (ctx, student) = await SeedAsync(verified: false);
        var reqs = await AddForNewAsync(ctx, student);
        Assert.False(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsSubmitted);
    }

    [Fact]
    public async Task CopyFromCancelledEnrollment_IsStillCarried()
    {
        var (ctx, student) = await SeedAsync(priorStatus: EnrollmentStatus.Cancelled);
        var reqs = await AddForNewAsync(ctx, student);
        Assert.True(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsVerified);
    }

    [Fact]
    public async Task DocumentNames_MatchIgnoringCaseAndWhitespace()
    {
        var (ctx, student) = await SeedAsync(priorDocName: "  psa birth certificate ");
        var reqs = await AddForNewAsync(ctx, student);
        Assert.True(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsVerified);
    }

    [Fact]
    public async Task GradeSpecificTemplates_StillFilterByGrade()
    {
        var (ctx, student) = await SeedAsync();
        Assert.DoesNotContain(await AddForNewAsync(ctx, student, "Grade 8"), r => r.DocumentName == "Grade 11 Waiver");
    }

    [Fact]
    public async Task MostRecentVerifiedCopy_Wins()
    {
        var (ctx, student) = await SeedAsync();
        var later = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Cancelled };
        ctx.Enrollments.Add(later);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = later.Id, DocumentName = "PSA Birth Certificate", IsSubmitted = true, IsVerified = true,
            FileName = "psa-v2.pdf", VerifiedBy = "admin@mshs.edu.ph", VerifiedAt = VerifiedOn.AddMonths(1)
        });
        await ctx.SaveChangesAsync();

        var reqs = await AddForNewAsync(ctx, student);
        Assert.Equal("psa-v2.pdf", reqs.Single(r => r.DocumentName == "PSA Birth Certificate").FileName);
    }

    [Fact]
    public async Task StaffCreateEnrollment_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var dto = await new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(student.Id, "2026-2027", "Grade 8"), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task BulkReenroll_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand("2025-2026", "2026-2027"), default);

        var created = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == "2026-2027");
        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == created.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task ParentRequest_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var parentId = Guid.NewGuid();
        student.ParentUserId = parentId;
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        var dto = await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(student.Id, parentId), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task StudentRequest_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var userId = Guid.NewGuid();
        student.UserId = userId;
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx).Handle(new StudentRequestEnrollmentCommand(userId), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentRequirementsTests"` → build FAILS (`EnrollmentRequirements`, `KeepOnFile` initializer usage compiles after Task 1 — the builder is what's missing).

- [ ] **Step 2: Create the builder**

`backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequirements.cs`:

```csharp
using System.Globalization;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments;

/// <summary>
/// The single place a new enrollment's requirement checklist is created (approval, staff
/// create, parent/student requests, bulk re-enroll). Active templates matching the grade
/// (GradeLevel == null or == grade) become requirements; for a KeepOnFile template the
/// learner's most recent VERIFIED copy of that document — from ANY earlier enrollment,
/// cancelled ones included — is carried over already verified, pointing at the same file.
/// Submitted-unverified and rejected copies never carry. Callers save.
///
/// A FileDocument can therefore be referenced by several enrollments' requirements: any
/// future blob cleanup must check every reference before deleting (sub-project 7).
/// </summary>
public static class EnrollmentRequirements
{
    public static Task AddForAsync(IApplicationDbContext context, Enrollment enrollment, CancellationToken ct, Guid? tenantId = null)
        => AddForManyAsync(context, new[] { enrollment }, ct, tenantId);

    public static async Task AddForManyAsync(
        IApplicationDbContext context, IReadOnlyCollection<Enrollment> enrollments, CancellationToken ct, Guid? tenantId = null)
    {
        if (enrollments.Count == 0) return;

        var templates = await context.RequirementTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
            .ToListAsync(ct);

        var keepOnFileNames = templates.Where(t => t.KeepOnFile).Select(t => Normalize(t.DocumentName)).ToHashSet();
        var onFile = new Dictionary<(Guid StudentId, string Doc), OnFileCopy>();

        if (keepOnFileNames.Count > 0)
        {
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            // One query for every student: their verified requirements (filtered by name in memory,
            // because the name match is trim/case-insensitive).
            var verified = await context.EnrollmentRequirements
                .Where(r => r.IsVerified && studentIds.Contains(r.Enrollment.StudentId))
                .Select(r => new OnFileCopy(
                    r.Enrollment.StudentId, r.Enrollment.SchoolYear, r.DocumentName,
                    r.FileName, r.Notes, r.VerifiedBy, r.VerifiedAt, r.CreatedAt))
                .ToListAsync(ct);

            foreach (var group in verified
                         .Where(r => keepOnFileNames.Contains(Normalize(r.DocumentName)))
                         .GroupBy(r => (r.StudentId, Normalize(r.DocumentName))))
            {
                onFile[group.Key] = group.OrderByDescending(r => r.VerifiedAt ?? r.CreatedAt).First();
            }
        }

        foreach (var enrollment in enrollments)
        {
            foreach (var template in templates.Where(t => t.GradeLevel == null || t.GradeLevel == enrollment.GradeLevel))
            {
                var requirement = new EnrollmentRequirement
                {
                    EnrollmentId = enrollment.Id,
                    DocumentName = template.DocumentName
                };
                if (tenantId != null) requirement.TenantId = tenantId.Value;

                if (template.KeepOnFile && onFile.TryGetValue((enrollment.StudentId, Normalize(template.DocumentName)), out var prior))
                {
                    requirement.IsSubmitted = true;
                    requirement.IsVerified = true;
                    requirement.FileName = prior.FileName;
                    requirement.Notes = prior.Notes;
                    requirement.VerifiedBy = prior.VerifiedBy;
                    requirement.VerifiedAt = prior.VerifiedAt;
                    var when = prior.VerifiedAt?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? "earlier";
                    requirement.ReviewNotes = $"On file — verified {when} (SY {prior.SchoolYear})";
                }

                context.EnrollmentRequirements.Add(requirement);
            }
        }
    }

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();

    private record OnFileCopy(
        Guid StudentId, string SchoolYear, string DocumentName,
        string? FileName, string? Notes, string? VerifiedBy, DateTime? VerifiedAt, DateTime CreatedAt);
}
```

- [ ] **Step 3: Replace the five template loops**

In each handler below, delete the `var templates = await _context.RequirementTemplates...` query and its `foreach` that adds `EnrollmentRequirement`s, and put the builder call in its place (add `using Enrollify.Application.Features.Enrollments;` where needed):

1. `ReviewApplicationCommand.cs` (inside the transaction lambda, variable `ct`): `await EnrollmentRequirements.AddForAsync(_context, enrollment, ct, app.TenantId);`
2. `CreateEnrollmentCommand.cs`: `await EnrollmentRequirements.AddForAsync(_context, enrollment, cancellationToken);` — keep the comment about using templates, update it to mention the shared builder.
3. `RequestEnrollmentCommand.cs`: `await EnrollmentRequirements.AddForAsync(_context, enrollment, cancellationToken);`
4. `StudentEnrollmentCommands.cs` → `StudentRequestEnrollmentCommandHandler`: `await EnrollmentRequirements.AddForAsync(_context, enrollment, cancellationToken);`
5. `BulkReenrollCommand.cs`: delete the `templates` query before the loop and the inner `foreach (var template ...)`; declare `var createdEnrollments = new List<Enrollment>();` before the loop, `createdEnrollments.Add(enrollment);` right after `_context.Enrollments.Add(enrollment);`, and after the loop (before `SaveChangesAsync`): `await EnrollmentRequirements.AddForManyAsync(_context, createdEnrollments, ct);`

- [ ] **Step 4: KeepOnFile through the template API**

1. DTO file: append `, bool KeepOnFile = false` to `RequirementTemplateDto`, `CreateRequirementTemplateRequest`, and `UpdateRequirementTemplateRequest`.
2. `CreateRequirementTemplateCommand`: record gains `, bool KeepOnFile = false`; set `KeepOnFile = request.KeepOnFile` in the initializer; return `new RequirementTemplateDto(..., template.DisplayOrder, template.KeepOnFile)`.
3. `UpdateRequirementTemplateCommand`: record gains `, bool KeepOnFile = false`; set `template.KeepOnFile = request.KeepOnFile;`; include it in the returned DTO.
4. `GetRequirementTemplatesQuery`: projection `new RequirementTemplateDto(t.Id, t.DocumentName, t.GradeLevel, t.IsActive, t.DisplayOrder, t.KeepOnFile)`.
5. `RequirementTemplatesController`: pass `request.KeepOnFile` as the last argument in both `Create` and `Update`.

- [ ] **Step 5: Defaults mark the PSA as kept on file**

- `ApplicationDbContextSeed.SeedRequirementTemplatesAsync`: in the `new RequirementTemplate { ... }` initializer add `KeepOnFile = defaults[i] == "PSA Birth Certificate",`.
- `CreateTenantCommand.SeedDefaultsForTenantAsync`: in its `new RequirementTemplate { ... }` add `KeepOnFile = defaultRequirements[i] == "PSA Birth Certificate",`.

- [ ] **Step 6: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentRequirementsTests"` → PASS.
Run full suite → all pass (existing `BulkReenrollTests`, `CreateEnrollmentTemplateTests`, approval tests keep their template behavior). Build → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "Learner record: one requirements builder; PSA kept on file across years

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Application submission carries the learner record

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Admissions/ApplicationMapping.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Admissions/*.cs` (the file with `ApplicantData` and `ApplicationDetailDto`)
- Modify: `backend/src/Enrollify.Application/Features/Admissions/Commands/SubmitApplicationCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Admissions/Commands/ReviewApplicationCommand.cs` (return mapping only)
- Modify: `backend/src/Enrollify.Application/Features/Admissions/Queries/GetApplicationByIdQuery.cs`
- Modify: `backend/src/Enrollify.Application/Features/ApplicationFormFields/DefaultApplicationFormFields.cs`
- Modify: `backend/src/Enrollify.Application/Features/ApplicationFormFields/Commands/CreateApplicationFormFieldCommand.cs`
- Modify: `backend/src/Enrollify.API/Controllers/SchoolsController.cs`, `AdmissionsController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/SubmitApplicationLearnerTests.cs`

**Interfaces:**
- Consumes: `LearnerProfileDto`, `EnrollmentLearnerInfoDto`, validators, `LearnerProfileMapping` (Task 3); `ShsStrandRules` (Task 6).
- Produces:
  - `SubmitApplicationCommand.Applicant` gains trailing `string? Lrn = null, LearnerProfileDto? Learner = null, string? LearnerType = null, string? LastGradeLevelCompleted = null, string? LastSchoolYearCompleted = null, string? LastSchoolId = null, string? Track = null, string? Strand = null`, plus method `ToLearnerInfo() : EnrollmentLearnerInfoDto`.
  - `ApplicantData` gains the same eight trailing properties.
  - `ApplicationDetailDto` gains trailing `string? Lrn = null, LearnerProfileDto? Learner = null, EnrollmentLearnerInfoDto? EnrollmentInfo = null`.
  - `ApplicationMapping.ToDetailDto(AdmissionApplication) : ApplicationDetailDto`, `ApplicationMapping.ToCommandApplicant(ApplicantData) : SubmitApplicationCommand.Applicant`, `ApplicationMapping.ParseCustomFieldValues(string?)`.
  - New built-in form-field keys: `lrn`, `extensionName`, `psaBirthCertificateNumber`, `placeOfBirth`, `motherTongue`, `religion`, `indigenousPeople`, `fourPs`, `disability` (Student); `fatherName`, `motherMaidenName` (new `Family` section); `learnerType`, `lastGradeLevelCompleted`, `lastSchoolYearCompleted`, `lastSchoolId`, `strand` (Enrollment). `DefaultApplicationFormFields.LrnHelpText` constant.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/SubmitApplicationLearnerTests.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class SubmitApplicationLearnerTests
{
    private static SubmitApplicationCommand.Applicant Child(
        string first = "Ana", string grade = "Grade 11", string? lrn = null, LearnerProfileDto? learner = null,
        string? track = null, string? strand = null, string? lastSchoolId = null) => new(
        first, null, "Cruz", "", null, "Female", new DateTime(2009, 3, 10), null,
        grade, "2026-2027", null, null, null, null, "Mother", null,
        Lrn: lrn, Learner: learner, LearnerType: "Transferee", LastSchoolId: lastSchoolId, Track: track, Strand: strand);

    private static SubmitApplicationCommand Parent(params SubmitApplicationCommand.Applicant[] children) => new(
        null, "Parent", "Rosa", "Cruz", "rosa@example.com", "0917-555-0000", children.ToList(), TestDb.TenantId);

    private static async Task<ApplicationDbContext> SeedAsync(bool strandActive = true)
    {
        var ctx = TestDb.Create();
        await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(ctx, TestDb.TenantId);
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = strandActive });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public void Validator_RejectsMalformedLrn()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(lrn: "12345")));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("Lrn"));
    }

    [Fact]
    public void Validator_ChecksEnrollmentFields_UnderAnEnrollmentPath()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(lastSchoolId: "12")));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("Enrollment.LastSchoolId"));
    }

    [Fact]
    public void Validator_ChecksNestedLearnerProfile()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(learner: new LearnerProfileDto(IsIndigenousPeople: true))));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("IpCommunity"));
    }

    [Fact]
    public async Task Siblings_OnlyTheShsChildNeedsAStrand_AndTheErrorNamesThatChild()
    {
        var ctx = await SeedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child("Ana", "Grade 11"), Child("Ben", "Grade 3")), default));

        Assert.Contains("Ana (Grade 11)", ex.Message);
        Assert.Equal(0, await ctx.AdmissionApplications.CountAsync());
    }

    [Fact]
    public async Task Strand_IsNormalizedAndPersisted()
    {
        var ctx = await SeedAsync();
        await new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child(track: "academic", strand: "stem")), default);

        var app = await ctx.AdmissionApplications.SingleAsync();
        Assert.Equal("Academic", app.Track);
        Assert.Equal("STEM", app.Strand);
    }

    [Fact]
    public async Task StrandOnNonShsGrade_IsRefused_NamingTheChild()
    {
        var ctx = await SeedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child("Ben", "Grade 3", track: "Academic", strand: "STEM")), default));
        Assert.StartsWith("Ben:", ex.Message);
    }

    [Fact]
    public async Task SchoolWithNoActiveStrands_AcceptsGrade11WithoutStrand()
    {
        var ctx = await SeedAsync(strandActive: false);
        await new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child()), default);
        Assert.Equal(1, await ctx.AdmissionApplications.CountAsync());
    }

    [Fact]
    public async Task LearnerFields_ArePersisted_AndReturnedInTheDetailDto()
    {
        var ctx = await SeedAsync();
        var learner = new LearnerProfileDto(
            PsaBirthCertificateNumber: "PSA-9", Religion: "Catholic", Is4PsBeneficiary: true, FourPsHouseholdId: "HH-7",
            FatherLastName: "Cruz", FatherFirstName: "Jun",
            HomeAddress: new PsgcAddressDto("1300000000", "NCR", null, null, "1381300000", "Quezon City", "1381300001", "Alicia", "1105"));

        var result = await new SubmitApplicationCommandHandler(ctx).Handle(
            Parent(Child(lrn: "136512260001", learner: learner, track: "Academic", strand: "STEM")), default);

        var dto = Assert.Single(result);
        Assert.Equal("136512260001", dto.Lrn);
        Assert.Equal("HH-7", dto.Learner!.FourPsHouseholdId);
        Assert.Equal("1381300001", dto.Learner.HomeAddress!.BarangayCode);
        Assert.Equal("Transferee", dto.EnrollmentInfo!.LearnerType);
        Assert.Equal("STEM", dto.EnrollmentInfo.Strand);
    }

    [Fact]
    public async Task AdminRequiredLrn_IsEnforced()
    {
        var ctx = await SeedAsync();
        var field = await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "lrn");
        field.IsRequired = true;
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child(track: "Academic", strand: "STEM")), default));
        Assert.Contains("is required", ex.Message);
    }

    [Fact]
    public async Task DefaultFormFields_IncludeTheDepEdKeys()
    {
        var ctx = await SeedAsync();
        var keys = await ctx.ApplicationFormFields.Select(f => f.FieldKey).ToListAsync();
        foreach (var k in new[] { "lrn", "psaBirthCertificateNumber", "indigenousPeople", "fourPs", "disability",
                     "fatherName", "motherMaidenName", "learnerType", "lastSchoolId", "strand" })
            Assert.Contains(k, keys);
        Assert.Equal("Family", (await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "fatherName")).Section);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~SubmitApplicationLearnerTests"` → build FAILS.

- [ ] **Step 2: Extend the API and command records**

In the Admissions DTO file add `using Enrollify.Application.DTOs.Learners;`. Change `ApplicantData`'s last line from `Dictionary<string, string?>? CustomFieldValues);` to:

```csharp
    Dictionary<string, string?>? CustomFieldValues,
    // DepEd learner record (optional; blanks are fine unless the school's form requires them).
    string? Lrn = null,
    LearnerProfileDto? Learner = null,
    string? LearnerType = null,
    string? LastGradeLevelCompleted = null,
    string? LastSchoolYearCompleted = null,
    string? LastSchoolId = null,
    string? Track = null,
    string? Strand = null);
```

Change `ApplicationDetailDto`'s last line from `Dictionary<string, string?>? CustomFieldValues);` to:

```csharp
    Dictionary<string, string?>? CustomFieldValues,
    string? Lrn = null,
    LearnerProfileDto? Learner = null,
    EnrollmentLearnerInfoDto? EnrollmentInfo = null);
```

In `SubmitApplicationCommand.cs` add `using Enrollify.Application.Common;`, `using Enrollify.Application.DTOs.Learners;`, `using Enrollify.Application.Features.ShsStrands;`, `using Enrollify.Application.Features.Students;`, and replace the nested `Applicant` record with:

```csharp
    public record Applicant(
        string FirstName, string? MiddleName, string LastName,
        string Email, string? ContactNumber, string Gender,
        DateTime DateOfBirth, string? Address,
        string GradeLevel, string SchoolYear,
        string? PreviousSchool, string? PreviousSchoolAddress,
        string? GuardianName, string? GuardianContact, string? GuardianRelationship,
        Dictionary<string, string?>? CustomFieldValues,
        string? Lrn = null,
        LearnerProfileDto? Learner = null,
        string? LearnerType = null,
        string? LastGradeLevelCompleted = null,
        string? LastSchoolYearCompleted = null,
        string? LastSchoolId = null,
        string? Track = null,
        string? Strand = null)
    {
        /// <summary>The per-enrollment DepEd fields as one DTO (PreviousSchool = last school attended).</summary>
        public EnrollmentLearnerInfoDto ToLearnerInfo() => new(
            LearnerType, LastGradeLevelCompleted, LastSchoolYearCompleted,
            PreviousSchool, PreviousSchoolAddress, LastSchoolId, Track, Strand);
    }
```

- [ ] **Step 3: Validator rules**

Inside the existing `RuleForEach(x => x.Applicants).ChildRules(a => { ... })` block, append:

```csharp
            a.RuleFor(x => x.Lrn)
                .Must(l => DepEdLists.IsTwelveDigitLrn(l!.Trim()))
                .When(x => !string.IsNullOrWhiteSpace(x.Lrn))
                .WithMessage("LRN must be exactly 12 digits (leave it blank if the learner has none yet).");
            a.RuleFor(x => x.Learner!).SetValidator(new LearnerProfileDtoValidator()).When(x => x.Learner != null);
            a.RuleFor(x => x.ToLearnerInfo()).SetValidator(new EnrollmentLearnerInfoDtoValidator())
                .OverridePropertyName("Enrollment");
```

- [ ] **Step 4: Handler — required built-ins, strand, persistence**

In `SubmitApplicationCommandHandler.Handle`:

1. In the built-in `switch (field.FieldKey)`, change the `address` case and add the new keys:

```csharp
                    // The address field is the PSGC picker: "required" means resolved to a barangay.
                    case "address": value = applicant.Learner?.HomeAddress?.BarangayCode; break;
                    case "lrn": value = applicant.Lrn; break;
                    case "extensionName": value = applicant.Learner?.ExtensionName; break;
                    case "psaBirthCertificateNumber": value = applicant.Learner?.PsaBirthCertificateNumber; break;
                    case "placeOfBirth": value = applicant.Learner?.PlaceOfBirth; break;
                    case "motherTongue": value = applicant.Learner?.MotherTongue; break;
                    case "religion": value = applicant.Learner?.Religion; break;
                    case "fatherName": value = BothNames(applicant.Learner?.FatherLastName, applicant.Learner?.FatherFirstName); break;
                    case "motherMaidenName": value = BothNames(applicant.Learner?.MotherMaidenLastName, applicant.Learner?.MotherMaidenFirstName); break;
                    case "learnerType": value = applicant.LearnerType; break;
                    case "lastGradeLevelCompleted": value = applicant.LastGradeLevelCompleted; break;
                    case "lastSchoolYearCompleted": value = applicant.LastSchoolYearCompleted; break;
                    case "lastSchoolId": value = applicant.LastSchoolId; break;
```

(Checkbox keys `indigenousPeople`, `fourPs`, `disability` and the core `strand` fall through to `default: continue;` — requiredness doesn't apply to a yes/no, and strand is enforced below.)

2. Right after the built-in requiredness `foreach`, before the custom-field loop, add:

```csharp
            // SHS track/strand against THIS school's catalog (explicit tenant — the public apply
            // flow has no tenant context). Errors name the child so siblings aren't confused.
            string? track, strand;
            try
            {
                (track, strand) = await ShsStrandRules.ResolveAsync(
                    _context, applicant.GradeLevel, applicant.Track, applicant.Strand, cancellationToken, request.TenantId);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"{applicant.FirstName}: {ex.Message}");
            }
            if (strand == null && await ShsStrandRules.IsStrandRequiredAsync(_context, applicant.GradeLevel, cancellationToken, request.TenantId))
                throw new InvalidOperationException($"Choose a track/strand for {applicant.FirstName} ({applicant.GradeLevel}).");
```

3. In the `new AdmissionApplication { ... }` initializer add:

```csharp
                Lrn = LearnerProfileMapping.Clean(applicant.Lrn),
                LearnerType = LearnerProfileMapping.Clean(applicant.LearnerType),
                LastGradeLevelCompleted = LearnerProfileMapping.Clean(applicant.LastGradeLevelCompleted),
                LastSchoolYearCompleted = LearnerProfileMapping.Clean(applicant.LastSchoolYearCompleted),
                LastSchoolId = LearnerProfileMapping.Clean(applicant.LastSchoolId),
                Track = track,
                Strand = strand,
```

and right after the initializer (before `_context.AdmissionApplications.Add(application);`):

```csharp
            if (applicant.Learner != null)
                LearnerProfileMapping.Apply(applicant.Learner, application);
```

4. Add the helper to the handler class:

```csharp
    // A required parent name needs at least last + first name.
    private static string? BothNames(string? last, string? first) =>
        string.IsNullOrWhiteSpace(last) || string.IsNullOrWhiteSpace(first) ? null : "provided";
```

5. Replace `return created.Select(ToDetailDto).ToList();` with `return created.Select(ApplicationMapping.ToDetailDto).ToList();` and delete the private `ToDetailDto` and `ParseCustomFieldValues` methods (they move to `ApplicationMapping`).

- [ ] **Step 5: `ApplicationMapping` and its call sites**

`backend/src/Enrollify.Application/Features/Admissions/ApplicationMapping.cs`:

```csharp
using System.Text.Json;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Admissions;

/// <summary>The one AdmissionApplication → ApplicationDetailDto mapping, and API → command applicant mapping.</summary>
public static class ApplicationMapping
{
    public static ApplicationDetailDto ToDetailDto(AdmissionApplication a) => new(
        a.Id, a.ApplicationNumber, a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender, a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear, a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.Status, a.CreatedAt, a.ReviewedAt, a.ReviewNotes, a.StudentId,
        a.ApplicationType, a.ParentFirstName, a.ParentLastName, a.ParentEmail, a.ParentContactNumber,
        ParseCustomFieldValues(a.CustomFieldValues),
        a.Lrn,
        LearnerProfileMapping.ToDto(a),
        new EnrollmentLearnerInfoDto(a.LearnerType, a.LastGradeLevelCompleted, a.LastSchoolYearCompleted,
            a.PreviousSchool, a.PreviousSchoolAddress, a.LastSchoolId, a.Track, a.Strand));

    public static SubmitApplicationCommand.Applicant ToCommandApplicant(ApplicantData a) => new(
        a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender,
        a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear,
        a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.CustomFieldValues,
        a.Lrn, a.Learner, a.LearnerType, a.LastGradeLevelCompleted, a.LastSchoolYearCompleted,
        a.LastSchoolId, a.Track, a.Strand);

    public static Dictionary<string, string?>? ParseCustomFieldValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json); }
        catch { return null; } // malformed JSON is ignored, as before
    }
}
```

- `GetApplicationByIdQuery`: replace everything after the `var a = ...` lookup with `return ApplicationMapping.ToDetailDto(a);` (drop the now-unused `System.Text.Json` using).
- `ReviewApplicationCommand`: replace the `Dictionary<string, string?>? custom = ...` block and the `return new ApplicationDetailDto(...)` with `return ApplicationMapping.ToDetailDto(app);`.
- `SchoolsController.Apply` and `AdmissionsController` apply action: replace the inline `.Select(a => new SubmitApplicationCommand.Applicant(...))` with `.Select(ApplicationMapping.ToCommandApplicant)` (add `using Enrollify.Application.Features.Admissions;`).

- [ ] **Step 6: Default form fields and the Family section**

In `DefaultApplicationFormFields.cs`:

1. Change the record to `private record FieldDefault(string Key, string Label, string Type, string Section, string AppliesTo, bool Required, bool Core, int Order, string? Options, string? HelpText = null);`
2. Add `public const string LrnHelpText = "12 digits. Leave blank if none yet (e.g. incoming Kindergarten).";`
3. Change the two existing defaults' labels: `previousSchool` → `"Last School Attended"`, `previousSchoolAddress` → `"Last School Address"` (only affects tenants created from now on — existing labels are never overwritten).
4. Append to `Defaults` (keep existing entries):

```csharp
        // DepEd learner record — Student section
        new FieldDefault("lrn",                       "LRN (Learner Reference Number)",                 "Text",     "Student",    "Both", false, false, 6,  null, LrnHelpText),
        new FieldDefault("extensionName",             "Name Extension (Jr., III)",                      "Text",     "Student",    "Both", false, false, 11, null),
        new FieldDefault("psaBirthCertificateNumber", "PSA Birth Certificate No.",                      "Text",     "Student",    "Both", false, false, 40, null),
        new FieldDefault("placeOfBirth",              "Place of Birth (City/Municipality)",             "Text",     "Student",    "Both", false, false, 41, null),
        new FieldDefault("motherTongue",              "Mother Tongue",                                  "Text",     "Student",    "Both", false, false, 42, null),
        new FieldDefault("religion",                  "Religion",                                       "Text",     "Student",    "Both", false, false, 43, null),
        new FieldDefault("indigenousPeople",          "Belongs to an Indigenous Peoples (IP) community", "Checkbox", "Student",    "Both", false, false, 50, null),
        new FieldDefault("fourPs",                    "4Ps beneficiary household",                      "Checkbox", "Student",    "Both", false, false, 51, null),
        new FieldDefault("disability",                "Learner with disability",                        "Checkbox", "Student",    "Both", false, false, 52, null),
        // Family section (per child — siblings can have different parents)
        new FieldDefault("fatherName",                "Father's Name",                                  "Text",     "Family",     "Both", false, false, 10, null),
        new FieldDefault("motherMaidenName",          "Mother's Maiden Name",                           "Text",     "Family",     "Both", false, false, 20, null),
        // Enrollment section
        new FieldDefault("strand",                    "Track / Strand",                                 "Dropdown", "Enrollment", "Both", false, true,  3,  null,
            "Shown only for Grade 11–12 when the school offers strands; always required there."),
        new FieldDefault("learnerType",               "Learner Type",                                   "Dropdown", "Enrollment", "Both", false, false, 5,  "[\"New\",\"Continuing\",\"Transferee\",\"Returning\"]"),
        new FieldDefault("lastGradeLevelCompleted",   "Last Grade Level Completed",                     "Text",     "Enrollment", "Both", false, false, 30, null),
        new FieldDefault("lastSchoolYearCompleted",   "Last School Year Completed",                     "Text",     "Enrollment", "Both", false, false, 31, null),
        new FieldDefault("lastSchoolId",              "Last School's DepEd School ID",                  "Text",     "Enrollment", "Both", false, false, 32, null),
```

5. In `EnsureFieldsForTenantAsync`'s `new ApplicationFormField { ... }` add `HelpText = d.HelpText`.

In `CreateApplicationFormFieldCommand.cs` change `ValidSections` to `new() { "Parent", "Student", "Enrollment", "Guardian", "Family" }`.

- [ ] **Step 7: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~SubmitApplicationLearnerTests"` → PASS. Full suite (including `SubmitApplicationGuardTests`, `SubmitApplicationEmailChecksTests`) → all pass. Build → 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add backend/
git commit -m "Learner record: applications carry LRN, BEEF fields, strand; DepEd form fields

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 9: Approval transcribes the learner record

**Files:**
- Modify: `backend/src/Enrollify.Application/Features/Admissions/Commands/ReviewApplicationCommand.cs`
- Modify: `backend/tests/Enrollify.Application.Tests/StudentDedupTests.cs`
- Test: `backend/tests/Enrollify.Application.Tests/ApprovalLearnerRecordTests.cs`

**Interfaces:**
- Consumes: `AdmissionApplication` learner fields (Tasks 1, 8), `LearnerProfileMapping.Apply/ToDto/FillBlanks/Clean` (Task 3), `EnrollmentRequirements.AddForAsync` (Task 7).
- Produces: approval behavior only — new students get the full profile, `LRN = app.Lrn`, a minted `StudentNumber`; matched students get blanks filled and the LRN adopted only when theirs is blank; enrollments get learner type, previous school, track/strand. `ExtractProvidedLrn` is removed.

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/ApprovalLearnerRecordTests.cs`:

```csharp
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ApprovalLearnerRecordTests
{
    private static readonly DateTime Dob = new(2009, 3, 10);

    private static AdmissionApplication App(string first = "Ana", string? lrn = "136512260001") => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
        FirstName = first, LastName = "Cruz", Email = "", Gender = "Female", DateOfBirth = Dob,
        GradeLevel = "Grade 11", SchoolYear = "2026-2027", Status = "Submitted",
        ApplicationType = "Parent", ParentUserId = Guid.NewGuid(),
        Lrn = lrn, LearnerType = "Transferee", PreviousSchool = "Rizal HS", PreviousSchoolAddress = "Pasig",
        LastGradeLevelCompleted = "Grade 10", LastSchoolYearCompleted = "2025-2026", LastSchoolId = "305123",
        Track = "Academic", Strand = "STEM",
        PsaBirthCertificateNumber = "PSA-9", Religion = "Catholic", MotherTongue = "Tagalog",
        HomeAddress = new Domain.ValueObjects.PsgcAddress
        {
            RegionCode = "1300000000", RegionName = "NCR",
            CityMunicipalityCode = "1381300000", CityMunicipalityName = "Quezon City",
            BarangayCode = "1381300001", BarangayName = "Alicia"
        }
    };

    private static Task ApproveAsync(ApplicationDbContext ctx, Guid appId) =>
        new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(appId, IsApproved: true, null), default);

    [Fact]
    public async Task NewLearner_GetsProfileLrnStudentNumberAndEnrollmentInfo()
    {
        var ctx = TestDb.Create();
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var s = await ctx.Students.SingleAsync();
        Assert.Equal("136512260001", s.LRN);
        Assert.False(s.LrnNeedsReview);
        Assert.Matches("^[0-9]{4}-[0-9]{5}$", s.StudentNumber);
        Assert.Equal("PSA-9", s.PsaBirthCertificateNumber);
        Assert.Equal("1381300001", s.HomeAddress.BarangayCode);

        var e = await ctx.Enrollments.SingleAsync();
        Assert.Equal("Transferee", e.LearnerType);
        Assert.Equal("Rizal HS", e.LastSchoolAttended);
        Assert.Equal("Pasig", e.LastSchoolAddress);
        Assert.Equal("305123", e.LastSchoolId);
        Assert.Equal("STEM", e.Strand);
    }

    [Fact]
    public async Task MatchesByLrnColumn_EvenWhenTheNameDiffers()
    {
        var ctx = TestDb.Create();
        var existing = new Student { StudentNumber = "2025-00001", LRN = "136512260001", FirstName = "Ana Marie", LastName = "Cruz", BirthDate = Dob, Address = "" };
        ctx.Students.Add(existing);
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal(1, await ctx.Students.CountAsync());
        Assert.Equal(existing.Id, (await ctx.Enrollments.SingleAsync()).StudentId);
    }

    [Fact]
    public async Task NameDobCandidate_WithADifferentLrn_IsADifferentLearner()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(new Student { StudentNumber = "2025-00001", LRN = "999999999999", FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "" });
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal(2, await ctx.Students.CountAsync());
    }

    [Fact]
    public async Task MatchedStudent_GetsBlanksFilled_NeverOverwritten_AndAdoptsLrnWhenBlank()
    {
        var ctx = TestDb.Create();
        var existing = new Student
        {
            StudentNumber = "2025-00001", LRN = null, FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "",
            Religion = "INC"
        };
        ctx.Students.Add(existing);
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var s = await ctx.Students.SingleAsync();
        Assert.Equal("INC", s.Religion);                 // kept
        Assert.Equal("Tagalog", s.MotherTongue);         // filled
        Assert.Equal("136512260001", s.LRN);             // adopted
        Assert.Equal("1381300001", s.HomeAddress.BarangayCode);
    }

    [Fact]
    public async Task LrnHeldByAnInactiveStudent_FailsCleanly_AndNothingPersists()
    {
        var dbName = $"approval-lrn-{Guid.NewGuid()}";
        Guid appId;
        using (var seed = TestDb.Create(dbName))
        {
            seed.Students.Add(new Student { StudentNumber = "2024-00007", LRN = "136512260001", FirstName = "Old", LastName = "Record", BirthDate = Dob, Address = "", IsActive = false });
            var app = App(first: "Zed");
            seed.AdmissionApplications.Add(app);
            await seed.SaveChangesAsync();
            appId = app.Id;
        }

        using (var ctx = TestDb.Create(dbName))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ApproveAsync(ctx, appId));
            Assert.Contains("2024-00007", ex.Message);
        }

        using var verify = TestDb.Create(dbName);
        Assert.Equal("Submitted", (await verify.AdmissionApplications.SingleAsync()).Status);
        Assert.Equal(1, await verify.Students.CountAsync());
    }

    [Fact]
    public async Task MatchedStudent_GetsTheirVerifiedPsaCarriedOver()
    {
        var ctx = TestDb.Create();
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, KeepOnFile = true });
        var existing = new Student { StudentNumber = "2025-00001", LRN = "136512260001", FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "" };
        ctx.Students.Add(existing);
        var prior = new Enrollment { StudentId = existing.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 10", Status = EnrollmentStatus.Enrolled };
        ctx.Enrollments.Add(prior);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = "PSA Birth Certificate", IsSubmitted = true, IsVerified = true,
            FileName = "psa.pdf", VerifiedAt = new DateTime(2025, 6, 3)
        });
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var created = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == "2026-2027");
        Assert.True((await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == created.Id)).IsVerified);
    }
}
```

In `StudentDedupTests.cs`, replace `Approval_MatchesByProvidedLrn_FromCustomField` with:

```csharp
    [Fact]
    public async Task Approval_MatchesByLrnColumn()
    {
        var ctx = TestDb.Create();
        // Different name and the LRN wins the match.
        var existing = ExistingStudent(lrn: "136512260077", first: "Maria Mikaela");
        ctx.Students.Add(existing);
        var app = ParentApp(Guid.NewGuid());
        app.Lrn = " 136512260077 ";
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        var dto = await ApproveAsync(ctx, app.Id);

        Assert.Equal(existing.Id, dto.StudentId);
        Assert.Equal(1, await ctx.Students.CountAsync());
    }
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~ApprovalLearnerRecordTests|FullyQualifiedName~StudentDedupTests"` → FAIL (fields not transcribed; LRN column not used).

- [ ] **Step 2: Matching by the LRN column**

In `ReviewApplicationCommand.cs` add `using Enrollify.Application.Features.Students;` (already present for `StudentNumbering`) and replace `FindMatchingActiveStudentAsync` and delete `ExtractProvidedLrn` entirely:

```csharp
    /// <summary>
    /// Matches an existing ACTIVE student for a re-applying family: the application's LRN first;
    /// otherwise FirstName + LastName + DateOfBirth (trimmed, case-insensitive) — but a name/DOB
    /// candidate that already carries a DIFFERENT LRN is a different learner and never matches.
    /// Inactive students never match — a deactivated record must not resurrect through admissions.
    /// </summary>
    private async Task<Student?> FindMatchingActiveStudentAsync(AdmissionApplication app, CancellationToken ct)
    {
        var providedLrn = LearnerProfileMapping.Clean(app.Lrn);
        if (providedLrn != null)
        {
            var byLrn = await _context.Students.FirstOrDefaultAsync(s => s.IsActive && s.LRN == providedLrn, ct);
            if (byLrn != null) return byLrn;
        }

        var first = app.FirstName.Trim().ToLower();
        var last = app.LastName.Trim().ToLower();
        var candidate = await _context.Students.FirstOrDefaultAsync(s => s.IsActive
            && s.FirstName.ToLower() == first
            && s.LastName.ToLower() == last
            && s.BirthDate == app.DateOfBirth, ct);

        if (candidate != null && providedLrn != null && candidate.LRN != null && candidate.LRN != providedLrn)
            return null;
        return candidate;
    }
```

- [ ] **Step 3: Transcription**

1. In the matched-student branch, after the two account-link lines, add:

```csharp
                    // New DepEd details fill blanks only — never overwrite the registrar's record.
                    LearnerProfileMapping.FillBlanks(app, existingStudent);
                    var appLrn = LearnerProfileMapping.Clean(app.Lrn);
                    if (existingStudent.LRN == null && appLrn != null
                        && !await _context.Students.AnyAsync(s => s.Id != existingStudent.Id && s.LRN == appLrn, ct))
                        existingStudent.LRN = appLrn;
```

2. In the new-student `else` branch, before `student = new Student { ... }`, add:

```csharp
                    var newLrn = LearnerProfileMapping.Clean(app.Lrn);
                    if (newLrn != null)
                    {
                        // Dedup only matches ACTIVE students; an inactive holder of this LRN would
                        // otherwise surface as a unique-index failure at save.
                        var holder = await _context.Students.FirstOrDefaultAsync(s => s.LRN == newLrn, ct);
                        if (holder != null)
                            throw new InvalidOperationException(
                                $"LRN {newLrn} already belongs to student {holder.StudentNumber}, who is inactive. " +
                                "Reactivate that student, then approve this application again.");
                    }
```

and in the initializer add `LRN = newLrn,`. After `_context.Students.Add(student);` add:

```csharp
                    LearnerProfileMapping.Apply(LearnerProfileMapping.ToDto(app), student);
```

3. In the `new Enrollment { ... }` initializer add:

```csharp
                    LearnerType = app.LearnerType ?? (existingStudent != null ? "Continuing" : "New"),
                    LastGradeLevelCompleted = app.LastGradeLevelCompleted,
                    LastSchoolYearCompleted = app.LastSchoolYearCompleted,
                    LastSchoolAttended = app.PreviousSchool,
                    LastSchoolAddress = app.PreviousSchoolAddress,
                    LastSchoolId = app.LastSchoolId,
                    Track = app.Track,
                    Strand = app.Strand,
```

- [ ] **Step 4: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~ApprovalLearnerRecordTests|FullyQualifiedName~StudentDedupTests|FullyQualifiedName~ReviewApplicationApprovalTests|FullyQualifiedName~AccountProvisioningTests"` → PASS. Full suite → all pass. Build → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add backend/
git commit -m "Learner record: approval transcribes LRN, BEEF fields, previous school, strand

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 10: Startup backfill for custom "lrn" fields

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Students/LearnerRecordBackfill.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs`
- Test: `backend/tests/Enrollify.Application.Tests/LearnerRecordBackfillTests.cs`

**Interfaces:**
- Consumes: `DefaultApplicationFormFields.LrnHelpText` (Task 8), `AdmissionApplication.Lrn` (Task 1).
- Produces: `LearnerRecordBackfill.RunForTenantAsync(IApplicationDbContext, Guid tenantId, CancellationToken = default) : Task<int>` (number of changes; caller saves; idempotent).

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/LearnerRecordBackfillTests.cs`:

```csharp
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class LearnerRecordBackfillTests
{
    private static AdmissionApplication App(string? customJson, string? lrn = null) => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8]}", FirstName = "A", LastName = "B", Email = "",
        Gender = "Female", GradeLevel = "Grade 7", SchoolYear = "2026-2027", Status = "Submitted",
        CustomFieldValues = customJson, Lrn = lrn
    };

    [Fact]
    public async Task ConvertsCustomLrnField_KeepingLabelVisibilityAndRequiredness()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField
        {
            FieldKey = "lrn", Label = "Learner Ref No.", FieldType = "Number", Section = "Enrollment",
            AppliesTo = "ParentMode", IsRequired = true, IsVisible = true, IsBuiltIn = false, Options = "[]"
        });
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        var f = await ctx.ApplicationFormFields.SingleAsync();
        Assert.True(f.IsBuiltIn);
        Assert.Equal("Text", f.FieldType);
        Assert.Equal("Student", f.Section);
        Assert.Equal("Both", f.AppliesTo);
        Assert.Equal("Learner Ref No.", f.Label);
        Assert.True(f.IsRequired);
        Assert.Null(f.Options);
    }

    [Fact]
    public async Task MovesCustomValuesIntoTheColumn_CaseInsensitively()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(App("{\"LRN\":\" 136512260001 \",\"allergies\":\"none\"}"));
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        Assert.Equal("136512260001", (await ctx.AdmissionApplications.SingleAsync()).Lrn);
    }

    [Fact]
    public async Task SecondRun_IsANoOp()
    {
        var ctx = TestDb.Create();
        ctx.ApplicationFormFields.Add(new ApplicationFormField { FieldKey = "lrn", Label = "LRN", FieldType = "Text", Section = "Student", AppliesTo = "Both" });
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"136512260001\"}"));
        await ctx.SaveChangesAsync();

        Assert.True(await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId) > 0);
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task ExistingColumnValue_IsLeftAlone_AndMalformedJsonIgnored()
    {
        var ctx = TestDb.Create();
        ctx.AdmissionApplications.Add(App("{\"lrn\":\"999999999999\"}", lrn: "136512260001"));
        ctx.AdmissionApplications.Add(App("{not json"));
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task NeverTouchesStudentLrnFlags()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(new Student { StudentNumber = "2024-00001", LRN = "136512260001", LrnNeedsReview = false, FirstName = "A", LastName = "B", Address = "" });
        await ctx.SaveChangesAsync();

        await LearnerRecordBackfill.RunForTenantAsync(ctx, TestDb.TenantId);
        await ctx.SaveChangesAsync();

        Assert.False((await ctx.Students.SingleAsync()).LrnNeedsReview);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~LearnerRecordBackfillTests"` → build FAILS.

- [ ] **Step 2: Implement the backfill**

`backend/src/Enrollify.Application/Features/Students/LearnerRecordBackfill.cs`:

```csharp
using System.Text.Json;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ApplicationFormFields;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Idempotent per-tenant conversion for schools that collected the LRN through a CUSTOM form
/// field keyed "lrn" before it was built in: the field becomes the built-in one (label,
/// visibility and requiredness kept) and saved answers move into AdmissionApplications.Lrn.
/// A second run changes nothing. It never touches Students.LrnNeedsReview — flagging is the
/// AddLearnerRecord migration's one-time job, so restarts can't re-flag confirmed LRNs.
/// Caller saves.
/// </summary>
public static class LearnerRecordBackfill
{
    public static async Task<int> RunForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var changes = 0;

        var field = await context.ApplicationFormFields.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.FieldKey == "lrn", ct);
        if (field != null && !field.IsBuiltIn)
        {
            field.IsBuiltIn = true;
            field.IsCore = false;
            field.FieldType = "Text";
            field.Section = "Student";
            field.AppliesTo = "Both";
            field.Options = null;
            field.HelpText ??= DefaultApplicationFormFields.LrnHelpText;
            changes++;
        }

        var candidates = await context.AdmissionApplications.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Lrn == null && a.CustomFieldValues != null)
            .ToListAsync(ct);
        foreach (var app in candidates)
        {
            var lrn = ExtractLrn(app.CustomFieldValues);
            if (lrn == null || lrn.Length > 20) continue; // column width; shape is checked on later writes
            app.Lrn = lrn;
            changes++;
        }

        return changes;
    }

    private static string? ExtractLrn(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(json);
            if (values == null) return null;
            foreach (var kv in values)
            {
                if (string.Equals(kv.Key, "lrn", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }
            return null;
        }
        catch (JsonException)
        {
            return null; // malformed legacy JSON is skipped, as everywhere else
        }
    }
}
```

- [ ] **Step 3: Run it at startup**

In `ApplicationDbContextSeed.SeedAsync`, add `await SeedLearnerRecordBackfillAsync(context);` right after `await SeedApplicationFormFieldsAsync(context);` (before `SeedShsStrandsAsync`), and add (with `using Enrollify.Application.Features.Students;`):

```csharp
    private static async Task SeedLearnerRecordBackfillAsync(ApplicationDbContext context)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync();
        var changes = 0;
        foreach (var tenantId in tenantIds)
            changes += await LearnerRecordBackfill.RunForTenantAsync(context, tenantId);
        if (changes > 0) await context.SaveChangesAsync();
    }
```

- [ ] **Step 4: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~LearnerRecordBackfillTests"` → PASS. Full suite → pass. Build → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add backend/
git commit -m "Learner record: idempotent startup backfill for custom lrn form fields

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 11: Enrollment learner info and the assessment gate

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentLearnerInfoMapping.cs`
- Create: `backend/src/Enrollify.Application/Features/Enrollments/Commands/UpdateEnrollmentLearnerInfoCommand.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Enrollments/*.cs` (the file holding `EnrollmentDto`)
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Queries/GetEnrollmentByIdQuery.cs`
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Commands/MoveEnrollmentStepCommand.cs`
- Modify: `backend/src/Enrollify.API/Controllers/EnrollmentsController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/EnrollmentLearnerInfoTests.cs`

**Interfaces:**
- Consumes: `EnrollmentLearnerInfoDto`, `EnrollmentLearnerInfoDtoValidator`, `LearnerProfileMapping.Clean` (Task 3), `ShsStrandRules` (Task 6).
- Produces:
  - `EnrollmentLearnerInfoMapping.ToDto(Enrollment) : EnrollmentLearnerInfoDto`
  - `EnrollmentDto` gains trailing `string? StudentNumber = null, string? Lrn = null, EnrollmentLearnerInfoDto? LearnerInfo = null` (populated by `GET /api/enrollments/{id}`)
  - `UpdateEnrollmentLearnerInfoCommand(Guid EnrollmentId, EnrollmentLearnerInfoDto Info) : IRequest<EnrollmentLearnerInfoDto>`; `PUT /api/enrollments/{id}/learner-info` [Admin, Registrar] with an `EnrollmentLearnerInfoDto` body. Editable only in Draft/Submitted.
  - Submitted→Assessed refuses a Grade 11/12 enrollment without a strand when the school offers strands: `"Set the learner's track/strand before assessment."`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/EnrollmentLearnerInfoTests.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentLearnerInfoTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment E)> SeedAsync(
        string grade = "Grade 11", EnrollmentStatus status = EnrollmentStatus.Draft, bool strandActive = true, bool withFees = true)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = strandActive });
        var s = new Student { StudentNumber = "2026-00001", LRN = "136512260001", FirstName = "Ana", LastName = "Cruz", Address = "" };
        ctx.Students.Add(s);
        var e = new Enrollment { StudentId = s.Id, SchoolYear = "2026-2027", GradeLevel = grade, Status = status };
        ctx.Enrollments.Add(e);
        if (withFees)
            ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 1000m, SchoolYear = "2026-2027", GradeLevel = grade, IsActive = true });
        await ctx.SaveChangesAsync();
        return (ctx, e);
    }

    private static UpdateEnrollmentLearnerInfoCommand Set(Guid id, string? track = "academic", string? strand = "stem") =>
        new(id, new EnrollmentLearnerInfoDto(LearnerType: "Transferee", LastSchoolId: "305123", Track: track, Strand: strand));

    [Fact]
    public async Task Draft_UpdatesFields_AndNormalizesStrand()
    {
        var (ctx, e) = await SeedAsync();
        var dto = await new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default);

        Assert.Equal("STEM", dto.Strand);
        var reloaded = await ctx.Enrollments.SingleAsync();
        Assert.Equal("Academic", reloaded.Track);
        Assert.Equal("Transferee", reloaded.LearnerType);
        Assert.Equal("305123", reloaded.LastSchoolId);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Enrolled)]
    [InlineData(EnrollmentStatus.Cancelled)]
    public async Task LockedOnceAssessed(EnrollmentStatus status)
    {
        var (ctx, e) = await SeedAsync(status: status);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default));
        Assert.Contains("locked", ex.Message);
    }

    [Fact]
    public async Task EditableAgain_AfterSendBack()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted); // send-back lands here
        await new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default);
        Assert.Equal("STEM", (await ctx.Enrollments.SingleAsync()).Strand);
    }

    [Fact]
    public async Task StrandOnNonShsGrade_IsRefused()
    {
        var (ctx, e) = await SeedAsync(grade: "Grade 7");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default));
    }

    [Fact]
    public async Task Assessment_RefusedWithoutStrand_WhenTheSchoolOffersStrands()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default));
        Assert.Equal("Set the learner's track/strand before assessment.", ex.Message);
    }

    [Fact]
    public async Task Assessment_AllowedWithoutStrand_WhenEveryStrandIsDeactivated()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted, strandActive: false);
        var dto = await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default);
        Assert.Equal(EnrollmentStatus.Assessed, dto.Status);
    }

    [Fact]
    public async Task Assessment_AllowedWithStrand()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted);
        e.Track = "Academic";
        e.Strand = "STEM";
        await ctx.SaveChangesAsync();

        var dto = await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default);
        Assert.Equal(EnrollmentStatus.Assessed, dto.Status);
    }

    [Fact]
    public async Task EnrollmentDetail_ExposesStudentNumberLrnAndLearnerInfo()
    {
        var (ctx, e) = await SeedAsync();
        e.LearnerType = "New";
        e.Strand = "STEM";
        await ctx.SaveChangesAsync();

        var dto = await new GetEnrollmentByIdQueryHandler(ctx).Handle(new GetEnrollmentByIdQuery(e.Id), default);

        Assert.Equal("2026-00001", dto.StudentNumber);
        Assert.Equal("136512260001", dto.Lrn);
        Assert.Equal("New", dto.LearnerInfo!.LearnerType);
        Assert.Equal("STEM", dto.LearnerInfo.Strand);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentLearnerInfoTests"` → build FAILS.

- [ ] **Step 2: DTO + mapping**

In the enrollments DTO file add `using Enrollify.Application.DTOs.Learners;` and change the end of `EnrollmentDto` from `int PendingPaymentsCount = 0);` to:

```csharp
    int PendingPaymentsCount = 0,
    string? StudentNumber = null,
    string? Lrn = null,
    EnrollmentLearnerInfoDto? LearnerInfo = null);
```

`backend/src/Enrollify.Application/Features/Enrollments/EnrollmentLearnerInfoMapping.cs`:

```csharp
using Enrollify.Application.DTOs.Learners;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Enrollments;

public static class EnrollmentLearnerInfoMapping
{
    public static EnrollmentLearnerInfoDto ToDto(Enrollment e) => new(
        e.LearnerType, e.LastGradeLevelCompleted, e.LastSchoolYearCompleted,
        e.LastSchoolAttended, e.LastSchoolAddress, e.LastSchoolId, e.Track, e.Strand);
}
```

In `GetEnrollmentByIdQuery`, change the `return new EnrollmentDto(...)` to pass the new named arguments after the requirements list:

```csharp
            e.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy, r.VerifiedAt, r.ReviewNotes)).ToList(),
            StudentNumber: e.Student.StudentNumber,
            Lrn: e.Student.LRN,
            LearnerInfo: EnrollmentLearnerInfoMapping.ToDto(e));
```

(add `using Enrollify.Application.Features.Enrollments;`).

- [ ] **Step 3: The update command**

`backend/src/Enrollify.Application/Features/Enrollments/Commands/UpdateEnrollmentLearnerInfoCommand.cs`:

```csharp
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Registrar edits an enrollment's DepEd fields (learner type, previous school, track/strand).
/// Editable only before assessment: assessment may price fees by strand (sub-project 2), so the
/// fields lock at Assessed. "Send back for re-assessment" returns the enrollment to Submitted,
/// which unlocks them again.
/// </summary>
public record UpdateEnrollmentLearnerInfoCommand(Guid EnrollmentId, EnrollmentLearnerInfoDto Info) : IRequest<EnrollmentLearnerInfoDto>;

public class UpdateEnrollmentLearnerInfoCommandValidator : AbstractValidator<UpdateEnrollmentLearnerInfoCommand>
{
    public UpdateEnrollmentLearnerInfoCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Info).NotNull().SetValidator(new EnrollmentLearnerInfoDtoValidator());
    }
}

public class UpdateEnrollmentLearnerInfoCommandHandler : IRequestHandler<UpdateEnrollmentLearnerInfoCommand, EnrollmentLearnerInfoDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateEnrollmentLearnerInfoCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentLearnerInfoDto> Handle(UpdateEnrollmentLearnerInfoCommand request, CancellationToken cancellationToken)
    {
        var e = await _context.Enrollments.FirstOrDefaultAsync(x => x.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (e.Status is not (EnrollmentStatus.Draft or EnrollmentStatus.Submitted))
            throw new InvalidOperationException(
                "Learner details are locked once the enrollment is assessed. Send it back for re-assessment to change them.");

        var info = request.Info;
        var (track, strand) = await ShsStrandRules.ResolveAsync(_context, e.GradeLevel, info.Track, info.Strand, cancellationToken);

        e.LearnerType = LearnerProfileMapping.Clean(info.LearnerType);
        e.LastGradeLevelCompleted = LearnerProfileMapping.Clean(info.LastGradeLevelCompleted);
        e.LastSchoolYearCompleted = LearnerProfileMapping.Clean(info.LastSchoolYearCompleted);
        e.LastSchoolAttended = LearnerProfileMapping.Clean(info.LastSchoolAttended);
        e.LastSchoolAddress = LearnerProfileMapping.Clean(info.LastSchoolAddress);
        e.LastSchoolId = LearnerProfileMapping.Clean(info.LastSchoolId);
        e.Track = track;
        e.Strand = strand;

        await _context.SaveChangesAsync(cancellationToken);
        return EnrollmentLearnerInfoMapping.ToDto(e);
    }
}
```

In `EnrollmentsController.cs` add (with `using Enrollify.Application.DTOs.Learners;`):

```csharp
    [HttpPut("{id:guid}/learner-info")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> UpdateLearnerInfo(Guid id, [FromBody] EnrollmentLearnerInfoDto request)
    {
        var result = await _sender.Send(new UpdateEnrollmentLearnerInfoCommand(id, request));
        return Ok(result);
    }
```

- [ ] **Step 4: The assessment gate**

In `MoveEnrollmentStepCommand.cs`, inside `if (enrollment.Status == EnrollmentStatus.Submitted)`, insert as the FIRST statement (add `using Enrollify.Application.Features.ShsStrands;`):

```csharp
            // SHS learners are assessed per strand (strand-based fees land in sub-project 2), so
            // a Grade 11/12 enrollment can't be assessed without one — unless the school offers no
            // strands at all. Families may still submit without; the registrar fills it in.
            if (string.IsNullOrWhiteSpace(enrollment.Strand)
                && await ShsStrandRules.IsStrandRequiredAsync(_context, enrollment.GradeLevel, cancellationToken))
                throw new InvalidOperationException("Set the learner's track/strand before assessment.");
```

- [ ] **Step 5: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentLearnerInfoTests|FullyQualifiedName~MoveEnrollmentStepCommandTests"` → PASS. Full suite → pass. Build → 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "Learner record: enrollment learner info (locked at assessment), strand gate

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 12: Enrollment creation paths set learner type and strand

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequestPlanner.cs`
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Commands/CreateEnrollmentCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Commands/RequestEnrollmentCommand.cs`
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Commands/StudentEnrollmentCommands.cs` (`StudentRequestEnrollmentCommand`)
- Modify: `backend/src/Enrollify.Application/Features/Enrollments/Commands/BulkReenrollCommand.cs`
- Modify: `backend/src/Enrollify.Application/DTOs/Enrollments/*.cs` (`CreateEnrollmentRequest`; add `RequestEnrollmentRequest`)
- Modify: `backend/src/Enrollify.API/Controllers/EnrollmentsController.cs`, `ParentController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/EnrollmentCreationLearnerTests.cs`

**Interfaces:**
- Consumes: `ShsStrandRules`, `DepEdLists`, `GradeLevels.Promote`, `EnrollmentRequirements` (Tasks 1, 6, 7).
- Produces:
  - `EnrollmentRequestPlanner.PlanAsync(IApplicationDbContext, Guid studentId, string schoolYear, string? requestedTrack, string? requestedStrand, CancellationToken) : Task<EnrollmentRequestPlanner.Plan>` where `Plan(string GradeLevel, string LearnerType, string? Track, string? Strand)`
  - `CreateEnrollmentCommand(Guid StudentId, string SchoolYear, string GradeLevel, string? LearnerType = null, string? Track = null, string? Strand = null)`; `CreateEnrollmentRequest` gains the same three trailing properties
  - `RequestEnrollmentCommand(Guid StudentId, Guid ParentUserId, string? Track = null, string? Strand = null)`; `StudentRequestEnrollmentCommand(Guid UserId, string? Track = null, string? Strand = null)`; `RequestEnrollmentRequest(string? Track, string? Strand)` as an OPTIONAL body on both request endpoints
  - `BulkReenrollResultDto(int Created, int SkippedExisting, int SkippedGraduates, int NeedsStrand = 0)`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/Enrollify.Application.Tests/EnrollmentCreationLearnerTests.cs`:

```csharp
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentCreationLearnerTests
{
    private const string Prev = "2025-2026";
    private const string Next = "2026-2027";

    private static async Task<(ApplicationDbContext Ctx, Student S, Guid ParentId)> SeedAsync(
        string? priorGrade = null, string? priorTrack = null, string? priorStrand = null)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "ABM", IsActive = true });
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var parentId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2025-00001", FirstName = "Ana", LastName = "Cruz", Address = "", ParentUserId = parentId, UserId = Guid.NewGuid() };
        ctx.Students.Add(s);
        if (priorGrade != null)
            ctx.Enrollments.Add(new Enrollment
            {
                StudentId = s.Id, SchoolYear = Prev, GradeLevel = priorGrade, Status = EnrollmentStatus.Enrolled,
                Track = priorTrack, Strand = priorStrand
            });
        await ctx.SaveChangesAsync();
        return (ctx, s, parentId);
    }

    private static Task<Enrollment> NewestAsync(ApplicationDbContext ctx) =>
        ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next);

    [Fact]
    public async Task StaffCreate_FirstEnrollment_IsNew_LaterOnes_AreContinuing()
    {
        var (ctx, s, _) = await SeedAsync();
        await new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(s.Id, Next, "Grade 7"), default);
        Assert.Equal("New", (await NewestAsync(ctx)).LearnerType);

        var (ctx2, s2, _) = await SeedAsync(priorGrade: "Grade 7");
        await new CreateEnrollmentCommandHandler(ctx2).Handle(new CreateEnrollmentCommand(s2.Id, Next, "Grade 8"), default);
        Assert.Equal("Continuing", (await NewestAsync(ctx2)).LearnerType);
    }

    [Fact]
    public async Task StaffCreate_ExplicitLearnerTypeAndStrand_AreKept()
    {
        var (ctx, s, _) = await SeedAsync();
        await new CreateEnrollmentCommandHandler(ctx).Handle(
            new CreateEnrollmentCommand(s.Id, Next, "Grade 11", "Transferee", "academic", "abm"), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Transferee", e.LearnerType);
        Assert.Equal("ABM", e.Strand);
    }

    [Fact]
    public async Task StaffCreate_StrandOnGrade8_IsRefused()
    {
        var (ctx, s, _) = await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(s.Id, Next, "Grade 8", null, "Academic", "STEM"), default));
    }

    [Fact]
    public void StaffCreateValidator_RejectsUnknownLearnerType()
    {
        var result = new CreateEnrollmentCommandValidator().Validate(new CreateEnrollmentCommand(Guid.NewGuid(), Next, "Grade 7", "Alumni"));
        Assert.Contains(result.Errors, e => e.PropertyName == "LearnerType");
    }

    [Fact]
    public async Task ParentRequest_Grade10To11_UsesTheRequestedStrand()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 10");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId, "Academic", "STEM"), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Grade 11", e.GradeLevel);
        Assert.Equal("STEM", e.Strand);
        Assert.Equal("Continuing", e.LearnerType);
    }

    [Fact]
    public async Task ParentRequest_Grade10To11_WithoutStrand_LeavesItForTheRegistrar()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 10");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId), default);
        Assert.Null((await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task ParentRequest_Grade11To12_CarriesTheStrand()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 11", priorTrack: "Academic", priorStrand: "STEM");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Grade 12", e.GradeLevel);
        Assert.Equal("STEM", e.Strand);
    }

    [Fact]
    public async Task StudentRequest_Grade11To12_CarriesTheStrand()
    {
        var (ctx, s, _) = await SeedAsync(priorGrade: "Grade 11", priorTrack: "Academic", priorStrand: "ABM");
        await new StudentRequestEnrollmentCommandHandler(ctx).Handle(new StudentRequestEnrollmentCommand(s.UserId!.Value), default);
        Assert.Equal("ABM", (await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task Bulk_CarriesStrand11To12_CountsNeedsStrand10To11_AndMarksContinuing()
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var g11 = new Student { StudentNumber = "2025-00001", FirstName = "Elf", LastName = "A", Address = "" };
        var g10 = new Student { StudentNumber = "2025-00002", FirstName = "Ten", LastName = "B", Address = "" };
        ctx.Students.AddRange(g11, g10);
        ctx.Enrollments.Add(new Enrollment { StudentId = g11.Id, SchoolYear = Prev, GradeLevel = "Grade 11", Status = EnrollmentStatus.Enrolled, Track = "Academic", Strand = "STEM" });
        ctx.Enrollments.Add(new Enrollment { StudentId = g10.Id, SchoolYear = Prev, GradeLevel = "Grade 10", Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var result = await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand(Prev, Next), default);

        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.NeedsStrand);
        var twelve = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next && e.StudentId == g11.Id);
        Assert.Equal("STEM", twelve.Strand);
        Assert.Equal("Continuing", twelve.LearnerType);
        Assert.Null((await ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next && e.StudentId == g10.Id)).Strand);
    }
}
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentCreationLearnerTests"` → build FAILS.

- [ ] **Step 2: The request planner**

`backend/src/Enrollify.Application/Features/Enrollments/EnrollmentRequestPlanner.cs`:

```csharp
using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments;

/// <summary>
/// Shared plan for a family-initiated enrollment request (parent for a child, student for
/// themselves). Grade: promote from the latest non-cancelled enrollment in another year; else
/// the approved application's grade; else Grade 7. Track/strand: the requested pair (validated)
/// when given; else carried from a previous SHS enrollment (11 → 12); else the approved
/// application's pair when the grade is SHS; else empty — the registrar sets it before assessment.
/// Learner type: Continuing when there's a previous enrollment, else the application's, else New.
/// </summary>
public static class EnrollmentRequestPlanner
{
    public record Plan(string GradeLevel, string LearnerType, string? Track, string? Strand);

    public static async Task<Plan> PlanAsync(
        IApplicationDbContext context, Guid studentId, string schoolYear,
        string? requestedTrack, string? requestedStrand, CancellationToken ct)
    {
        var previous = await context.Enrollments
            .Where(e => e.StudentId == studentId && e.SchoolYear != schoolYear && e.Status != EnrollmentStatus.Cancelled)
            .OrderByDescending(e => e.SchoolYear).ThenByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        AdmissionApplication? application = null;
        string grade;
        if (previous != null)
        {
            grade = GradeLevels.Promote(previous.GradeLevel);
        }
        else
        {
            application = await context.AdmissionApplications
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.Status == "Approved", ct);
            grade = application?.GradeLevel ?? "Grade 7";
        }

        var (track, strand) = await ShsStrandRules.ResolveAsync(context, grade, requestedTrack, requestedStrand, ct);
        if (strand == null && DepEdLists.IsShsGrade(grade))
        {
            if (previous != null && DepEdLists.IsShsGrade(previous.GradeLevel))
                (track, strand) = (previous.Track, previous.Strand);
            else if (application != null)
                (track, strand) = (application.Track, application.Strand);
        }

        var learnerType = previous != null ? "Continuing" : application?.LearnerType ?? "New";
        return new Plan(grade, learnerType, track, strand);
    }
}
```

- [ ] **Step 3: Parent and student requests use the planner**

In `RequestEnrollmentCommand.cs` change the record to `public record RequestEnrollmentCommand(Guid StudentId, Guid ParentUserId, string? Track = null, string? Strand = null) : IRequest<EnrollmentDto>;` and replace everything from the `// Grade resolution order:` comment through the closing `}` of the `else` that sets `gradeLevel` with:

```csharp
        var plan = await EnrollmentRequestPlanner.PlanAsync(_context, student.Id, schoolYear, request.Track, request.Strand, cancellationToken);
```

then in the `new Enrollment { ... }` use `GradeLevel = plan.GradeLevel,` and add `LearnerType = plan.LearnerType, Track = plan.Track, Strand = plan.Strand,`. Make the identical edit in `StudentEnrollmentCommands.cs` for `StudentRequestEnrollmentCommand` (record becomes `StudentRequestEnrollmentCommand(Guid UserId, string? Track = null, string? Strand = null)`), using `request.Track, request.Strand`.

Add to the enrollments DTO file:

```csharp
public record RequestEnrollmentRequest(string? Track, string? Strand);
```

`ParentController.RequestEnrollment` becomes (add `using Microsoft.AspNetCore.Mvc.ModelBinding;`):

```csharp
    [HttpPost("children/{studentId:guid}/enrollments/request")]
    public async Task<IActionResult> RequestEnrollment(Guid studentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RequestEnrollmentRequest? request)
    {
        var result = await _sender.Send(new RequestEnrollmentCommand(studentId, CurrentUserId(), request?.Track, request?.Strand));
        return Ok(result);
    }
```

In `EnrollmentsController`, change the student `RequestEnrollment()` action the same way: add the optional `[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RequestEnrollmentRequest? request` parameter and pass `request?.Track, request?.Strand` into `StudentRequestEnrollmentCommand` (keep its existing user-id lookup).

- [ ] **Step 4: Staff create**

In `CreateEnrollmentCommand.cs` (add `using Enrollify.Application.Common;`, `using Enrollify.Application.Features.ShsStrands;`):

1. Record: `public record CreateEnrollmentCommand(Guid StudentId, string SchoolYear, string GradeLevel, string? LearnerType = null, string? Track = null, string? Strand = null) : IRequest<EnrollmentDto>;`
2. Validator: add

```csharp
        RuleFor(x => x.LearnerType).Must(t => DepEdLists.LearnerTypes.Contains(t!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.LearnerType))
            .WithMessage("Learner type must be New, Continuing, Transferee, or Returning.");
```

3. Handler, after the duplicate check:

```csharp
        var (track, strand) = await ShsStrandRules.ResolveAsync(_context, request.GradeLevel, request.Track, request.Strand, cancellationToken);
        var learnerType = string.IsNullOrWhiteSpace(request.LearnerType)
            ? (await _context.Enrollments.AnyAsync(e => e.StudentId == request.StudentId && e.Status != EnrollmentStatus.Cancelled, cancellationToken)
                ? "Continuing" : "New")
            : request.LearnerType.Trim();
```

and add `LearnerType = learnerType, Track = track, Strand = strand,` to the `new Enrollment { ... }`.

4. `CreateEnrollmentRequest` (DTO file): append `, string? LearnerType = null, string? Track = null, string? Strand = null`. `EnrollmentsController.Create`: pass `request.LearnerType, request.Track, request.Strand`.

- [ ] **Step 5: Bulk re-enroll**

In `BulkReenrollCommand.cs` (add `using Enrollify.Application.Common;`, `using Enrollify.Application.Features.ShsStrands;`):

1. `public record BulkReenrollResultDto(int Created, int SkippedExisting, int SkippedGraduates, int NeedsStrand = 0);`
2. Declare `int needsStrand = 0;` beside the other counters (reset it inside the transaction lambda with the others), and before the loop: `var offersStrands = await _context.ShsStrands.AnyAsync(s => s.IsActive, ct);`
3. In the `new Enrollment { ... }` add:

```csharp
                    LearnerType = "Continuing",
                    // 11 → 12 keeps the strand; 10 → 11 leaves it for the registrar.
                    Track = DepEdLists.IsShsGrade(source.GradeLevel) ? source.Track : null,
                    Strand = DepEdLists.IsShsGrade(source.GradeLevel) ? source.Strand : null,
```

4. After adding the enrollment: `if (offersStrands && DepEdLists.IsShsGrade(newGrade) && string.IsNullOrWhiteSpace(enrollment.Strand)) needsStrand++;`
5. Return `new BulkReenrollResultDto(created, skippedExisting, skippedGraduates, needsStrand);`

- [ ] **Step 6: Run tests, build**

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentCreationLearnerTests|FullyQualifiedName~BulkReenrollTests|FullyQualifiedName~RequestEnrollmentPromotionTests|FullyQualifiedName~CreateEnrollmentTemplateTests"` → PASS. Full suite → pass. Build → 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "Learner record: creation paths set learner type and carry SHS strand

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 13: Register and roster show the student number

**Files:**
- Modify: `backend/src/Enrollify.Application/Features/Reports/Queries/GetEnrollmentRegisterQuery.cs`
- Modify: `backend/src/Enrollify.Application/Features/Reports/EnrollmentRegisterCsvBuilder.cs`
- Modify: `backend/src/Enrollify.Application/Features/Sections/Queries/GetSectionRosterQuery.cs`
- Modify: `backend/tests/Enrollify.Application.Tests/EnrollmentRegisterTests.cs`, `SectionRosterTests.cs`

**Interfaces:**
- Produces: `EnrollmentRegisterRowDto` gains trailing `string StudentNumber = ""`; `RosterStudentDto` gains trailing `string StudentNumber = ""`; CSV header becomes `LRN,Student No.,Student,Grade Level,Section,Status,Payment Plan,Assessed Total,Total Paid,Balance,Created` (LRN stays first so existing consumers that key on the first column keep working; a null LRN renders as an empty field).

- [ ] **Step 1: Write the failing tests**

Append to `EnrollmentRegisterTests.cs` (inside the class):

```csharp
    [Fact]
    public void Csv_IncludesStudentNumber_AndRendersMissingLrnAsEmpty()
    {
        var row = new EnrollmentRegisterRowDto(Guid.NewGuid(), Guid.NewGuid(), null, "Cruz, Ana", "Grade 7", null,
            "Draft", null, null, 0m, null, new DateTime(2026, 6, 1), StudentNumber: "2026-00042");

        var line = EnrollmentRegisterCsvBuilder.Build(new[] { row }).TrimEnd().Split(Environment.NewLine)[1];

        Assert.StartsWith(",2026-00042,\"Cruz, Ana\",", line);
    }
```

Append to `SectionRosterTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task Roster_IncludesStudentNumber_AndToleratesMissingLrn()
    {
        var ctx = TestDb.Create();
        var section = new Section { Name = "Curie", GradeLevel = "Grade 7", SchoolYear = "2026-2027", Capacity = 40 };
        ctx.Sections.Add(section);
        var kim = new Student { StudentNumber = "2026-00001", LRN = null, FirstName = "Kim", LastName = "Tan", Gender = "Female", Address = "" };
        ctx.Students.Add(kim);
        ctx.Enrollments.Add(new Enrollment { StudentId = kim.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 7", SectionId = section.Id, Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var dto = await new GetSectionRosterQueryHandler(ctx).Handle(new GetSectionRosterQuery(section.Id), default);

        var only = Assert.Single(dto.Students);
        Assert.Equal("2026-00001", only.StudentNumber);
        Assert.Null(only.Lrn);
    }
```

Run: `dotnet test tests/Enrollify.Application.Tests --filter "FullyQualifiedName~EnrollmentRegisterTests|FullyQualifiedName~SectionRosterTests"` → build FAILS (`StudentNumber` parameter missing).

- [ ] **Step 2: Implement**

- `GetEnrollmentRegisterQuery.cs`: append `, string StudentNumber = ""` to `EnrollmentRegisterRowDto`; in `ProjectAsync`'s anonymous projection add `e.Student.StudentNumber,`; in the `new EnrollmentRegisterRowDto(...)` add the named argument `StudentNumber: r.StudentNumber`.
- `EnrollmentRegisterCsvBuilder.cs`: `Header = "LRN,Student No.,Student,Grade Level,Section,Status,Payment Plan,Assessed Total,Total Paid,Balance,Created"`; insert `CollectionsCsvBuilder.Escape(row.StudentNumber),` right after `CollectionsCsvBuilder.Escape(row.Lrn),`.
- `GetSectionRosterQuery.cs`: append `, string StudentNumber = ""` to `RosterStudentDto`; add `e.Student.StudentNumber,` to the projection; pass `StudentNumber: r.StudentNumber` when constructing.

- [ ] **Step 3: Run tests, build**

Run the two test classes → PASS; full suite → pass; build → 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add backend/
git commit -m "Learner record: register and roster show Enrollify student numbers

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 14: Frontend models, constants, API service

**Files:**
- Create: `enrollify.client/src/app/core/learner.ts`
- Modify: `enrollify.client/src/app/core/models/index.ts`
- Modify: `enrollify.client/src/app/core/constants.ts`
- Modify: `enrollify.client/src/app/core/services/api.service.ts`
- Modify (compile fixes only): `pages/students/student-form/student-form.component.ts`, `pages/apply/apply.component.ts`, `pages/settings/settings.component.ts`

**Interfaces:**
- Consumes: the backend JSON shapes from Tasks 3–13 (System.Text.Json camelCase: `is4PsBeneficiary`, `fourPsHouseholdId`, `homeAddress`, `learnerInfo`, `needsStrand`, …).
- Produces (TypeScript):
  - Models: `PsgcAddress`, `LearnerProfile`, `EnrollmentLearnerInfo`, `ShsStrand`; `Student.lrn: string | null`, `Student.studentNumber`, `Student.lrnNeedsReview`, `Student.profile`; `CreateStudentRequest.lrn: string | null`, `.profile: LearnerProfile | null`; `Enrollment.studentNumber? / lrn? / learnerInfo?`; `CreateEnrollmentRequest.learnerType? / track? / strand?`; `ApplicantData` gains `lrn, learner, learnerType, lastGradeLevelCompleted, lastSchoolYearCompleted, lastSchoolId, track, strand`; `FormFieldSection` gains `'Family'`; `RequirementTemplate.keepOnFile`; `ApplicationDetailDto.lrn? / learner? / enrollmentInfo?`; `ProfileSelfUpdate`.
  - `core/learner.ts`: `emptyPsgcAddress()`, `emptyLearnerProfile()`, `emptyLearnerInfo()`, `isPsgcMatched(a)`, `formatPsgcAddress(street, a)`, `FieldConfigView`, `ALL_FIELDS_VISIBLE`.
  - `core/constants.ts`: `LEARNER_TYPES`, `DISABILITY_TYPES`, `EXTENSION_NAME_SUGGESTIONS`, `isShsGrade()`.
  - `ApiService`: `getStudents(search, page, pageSize, includeInactive, flags)`, `confirmLrn(id)`, `updateEnrollmentLearnerInfo(id, info)`, `getShsStrands(includeInactive)`, `createShsStrand(data)`, `updateShsStrand(id, data)`, `getSchoolShsStrands(slug)`, `requestEnrollment(strand?)`, `requestEnrollmentForChild(studentId, strand?)`, `bulkReenroll` result `needsStrand`, requirement-template calls carry `keepOnFile`, `updateMyProfile/updateChildProfile(data: ProfileSelfUpdate)`.

- [ ] **Step 1: Models**

In `core/models/index.ts`:

1. Replace the `Student` and `CreateStudentRequest` interfaces with:

```ts
// PSGC-resolved address. Codes are 10-digit strings (never numbers); names travel with them.
export interface PsgcAddress {
  regionCode: string | null;
  regionName: string | null;
  provinceCode: string | null;     // null for NCR / independent cities
  provinceName: string | null;
  cityMunicipalityCode: string | null;
  cityMunicipalityName: string | null;
  barangayCode: string | null;
  barangayName: string | null;
  zipCode: string | null;
}

// DepEd Basic Education Enrollment Form learner fields (LRN travels separately).
export interface LearnerProfile {
  extensionName: string | null;
  psaBirthCertificateNumber: string | null;
  placeOfBirth: string | null;
  motherTongue: string | null;
  religion: string | null;
  isIndigenousPeople: boolean;
  ipCommunity: string | null;
  is4PsBeneficiary: boolean;
  fourPsHouseholdId: string | null;
  hasDisability: boolean;
  disabilityTypes: string[] | null;
  fatherLastName: string | null;
  fatherFirstName: string | null;
  fatherMiddleName: string | null;
  fatherContactNumber: string | null;
  motherMaidenLastName: string | null;
  motherMaidenFirstName: string | null;
  motherMaidenMiddleName: string | null;
  motherContactNumber: string | null;
  homeAddress: PsgcAddress | null;
}

// Per-enrollment DepEd fields.
export interface EnrollmentLearnerInfo {
  learnerType: string | null;
  lastGradeLevelCompleted: string | null;
  lastSchoolYearCompleted: string | null;
  lastSchoolAttended: string | null;
  lastSchoolAddress: string | null;
  lastSchoolId: string | null;
  track: string | null;
  strand: string | null;
}

export interface ShsStrand {
  id: string;
  track: string;
  strand: string;
  isActive: boolean;
  displayOrder: number;
}

export interface Student {
  id: string;
  // Enrollify-issued "YYYY-NNNNN". Never confuse with the DepEd LRN.
  studentNumber: string;
  // DepEd LRN (12 digits); null until known.
  lrn: string | null;
  // Pre-migration LRN awaiting registrar confirmation ("Check LRN").
  lrnNeedsReview: boolean;
  firstName: string;
  middleName: string;
  lastName: string;
  birthDate: string;
  gender: string | null;
  // House no. / street / sitio line; the structured part is profile.homeAddress.
  address: string;
  contactNumber: string | null;
  email: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  fullName: string;
  // Login linkage: the student's own account, and/or the managing parent's.
  userId: string | null;
  parentUserId: string | null;
  isActive: boolean;
  profile: LearnerProfile | null;
}

export interface CreateStudentRequest {
  lrn: string | null;
  firstName: string;
  middleName: string;
  lastName: string;
  birthDate: string;
  gender: string | null;
  address: string;
  contactNumber: string | null;
  email: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  profile: LearnerProfile | null;
}

// What a parent or student may change on their own profile (null/omitted = unchanged).
export interface ProfileSelfUpdate {
  contactNumber?: string;
  email?: string;
  address?: string;
  guardianName?: string;
  guardianContact?: string;
  homeAddress?: PsgcAddress;
  fatherContactNumber?: string;
  motherContactNumber?: string;
}
```

2. In `Enrollment` add after `pendingPaymentsCount?: number;`:

```ts
  // Populated on GET /enrollments/{id} (detail + print pages).
  studentNumber?: string | null;
  lrn?: string | null;
  learnerInfo?: EnrollmentLearnerInfo | null;
```

3. In `CreateEnrollmentRequest` add `learnerType?: string | null; track?: string | null; strand?: string | null;`.
4. In `ApplicantData` add after `customFieldValues`:

```ts
  lrn: string | null;
  learner: LearnerProfile;
  learnerType: string | null;
  lastGradeLevelCompleted: string | null;
  lastSchoolYearCompleted: string | null;
  lastSchoolId: string | null;
  track: string | null;
  strand: string | null;
```

5. `export type FormFieldSection = 'Parent' | 'Student' | 'Enrollment' | 'Guardian' | 'Family';`
6. `RequirementTemplate` gains `keepOnFile: boolean;`.
7. `ApplicationDetailDto` gains `lrn?: string | null; learner?: LearnerProfile | null; enrollmentInfo?: EnrollmentLearnerInfo | null;`.

- [ ] **Step 2: `core/learner.ts`**

```ts
import { EnrollmentLearnerInfo, LearnerProfile, PsgcAddress } from './models';

export function emptyPsgcAddress(): PsgcAddress {
  return {
    regionCode: null, regionName: null, provinceCode: null, provinceName: null,
    cityMunicipalityCode: null, cityMunicipalityName: null, barangayCode: null, barangayName: null, zipCode: null,
  };
}

export function emptyLearnerProfile(): LearnerProfile {
  return {
    extensionName: null, psaBirthCertificateNumber: null, placeOfBirth: null, motherTongue: null, religion: null,
    isIndigenousPeople: false, ipCommunity: null, is4PsBeneficiary: false, fourPsHouseholdId: null,
    hasDisability: false, disabilityTypes: null,
    fatherLastName: null, fatherFirstName: null, fatherMiddleName: null, fatherContactNumber: null,
    motherMaidenLastName: null, motherMaidenFirstName: null, motherMaidenMiddleName: null, motherContactNumber: null,
    homeAddress: emptyPsgcAddress(),
  };
}

/** A profile safe to bind in forms: never null, address always present. */
export function editableProfile(p: LearnerProfile | null | undefined): LearnerProfile {
  const base = emptyLearnerProfile();
  return { ...base, ...(p ?? {}), homeAddress: { ...emptyPsgcAddress(), ...(p?.homeAddress ?? {}) } };
}

export function emptyLearnerInfo(): EnrollmentLearnerInfo {
  return {
    learnerType: null, lastGradeLevelCompleted: null, lastSchoolYearCompleted: null,
    lastSchoolAttended: null, lastSchoolAddress: null, lastSchoolId: null, track: null, strand: null,
  };
}

export function isPsgcMatched(a: PsgcAddress | null | undefined): boolean {
  return !!a?.cityMunicipalityCode && !!a?.barangayCode;
}

/** "12 Mabini St, Brgy. Alicia, Quezon City, Metro Manila" — for read-only display. */
export function formatPsgcAddress(street: string | null | undefined, a: PsgcAddress | null | undefined): string {
  const parts = [
    street?.trim() || null,
    a?.barangayName ? `Brgy. ${a.barangayName}` : null,
    a?.cityMunicipalityName ?? null,
    a?.provinceName ?? a?.regionName ?? null,
  ].filter((p): p is string => !!p);
  return parts.join(', ');
}

/** How a form decides whether a configurable field shows, is required, and what it's called. */
export interface FieldConfigView {
  visible(key: string): boolean;
  required(key: string): boolean;
  label(key: string, fallback: string): string;
}

/** Staff forms show every learner field, none forced by school form settings. */
export const ALL_FIELDS_VISIBLE: FieldConfigView = {
  visible: () => true,
  required: () => false,
  label: (_key, fallback) => fallback,
};
```

- [ ] **Step 3: Constants**

Append to `core/constants.ts` (spellings must match `DepEdLists` in the backend):

```ts
export const LEARNER_TYPES: { value: string; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'Continuing', label: 'Continuing' },
  { value: 'Transferee', label: 'Transferee' },
  { value: 'Returning', label: 'Returning (Balik-Aral)' },
];

export const DISABILITY_TYPES: string[] = [
  'Visual Impairment', 'Hearing Impairment', 'Learning Disability', 'Intellectual Disability',
  'Autism Spectrum Disorder', 'Emotional-Behavioral Disorder', 'Orthopedic/Physical Handicap',
  'Speech/Language Disorder', 'Cerebral Palsy', 'Special Health Problem/Chronic Disease',
  'Multiple Disorder', 'Cancer',
];

export const EXTENSION_NAME_SUGGESTIONS: string[] = ['Jr.', 'Sr.', 'II', 'III', 'IV'];

export function isShsGrade(grade: string | null | undefined): boolean {
  const g = (grade ?? '').trim().toLowerCase();
  return g === 'grade 11' || g === 'grade 12';
}
```

- [ ] **Step 4: ApiService**

In `core/services/api.service.ts` import `EnrollmentLearnerInfo, ShsStrand, ProfileSelfUpdate` from `../models`, then:

1. Replace `getStudents` with:

```ts
  getStudents(search?: string, page = 1, pageSize = 20, includeInactive = false,
              flags: { missingLrn?: boolean; lrnNeedsReview?: boolean } = {}): Observable<PagedResult<Student>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (includeInactive) params = params.set('includeInactive', true);
    if (flags.missingLrn) params = params.set('missingLrn', true);
    if (flags.lrnNeedsReview) params = params.set('lrnNeedsReview', true);
    return this.http.get<PagedResult<Student>>(`${this.baseUrl}/students`, { params });
  }

  confirmLrn(studentId: string): Observable<Student> {
    return this.http.post<Student>(`${this.baseUrl}/students/${studentId}/confirm-lrn`, {});
  }
```

2. Add after `getEnrollment`:

```ts
  updateEnrollmentLearnerInfo(id: string, info: EnrollmentLearnerInfo): Observable<EnrollmentLearnerInfo> {
    return this.http.put<EnrollmentLearnerInfo>(`${this.baseUrl}/enrollments/${id}/learner-info`, info);
  }
```

3. Change `bulkReenroll`'s type to `Observable<{ created: number; skippedExisting: number; skippedGraduates: number; needsStrand: number }>` (both the return type and the `post<...>` generic).
4. Replace `requestEnrollment()` and `requestEnrollmentForChild(studentId)`:

```ts
  // Optional SHS strand chosen when moving up into Grade 11; omitted otherwise.
  requestEnrollment(strand?: { track: string; strand: string } | null): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/request`, strand ?? {});
  }
```

```ts
  requestEnrollmentForChild(studentId: string, strand?: { track: string; strand: string } | null): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/parent/children/${studentId}/enrollments/request`, strand ?? {});
  }
```

5. Change `updateMyProfile(data: ProfileSelfUpdate)` and `updateChildProfile(studentId: string, data: ProfileSelfUpdate)` parameter types (bodies unchanged).
6. Requirement templates: `createRequirementTemplate(data: { documentName: string; gradeLevel: string | null; displayOrder: number; keepOnFile: boolean })` and `updateRequirementTemplate(id: string, data: { documentName: string; gradeLevel: string | null; isActive: boolean; displayOrder: number; keepOnFile: boolean })`.
7. Add an SHS strands block (after the requirement-template methods) and the public slug call (after `getSchoolSchoolYears`):

```ts
  // SHS strands (tenant catalog). Inactive rows are returned to staff only.
  getShsStrands(includeInactive = false): Observable<ShsStrand[]> {
    const params = includeInactive ? new HttpParams().set('includeInactive', true) : undefined;
    return this.http.get<ShsStrand[]>(`${this.baseUrl}/shs-strands`, { params });
  }

  createShsStrand(data: { track: string; strand: string; displayOrder: number }): Observable<ShsStrand> {
    return this.http.post<ShsStrand>(`${this.baseUrl}/shs-strands`, data);
  }

  updateShsStrand(id: string, data: { track: string; strand: string; isActive: boolean; displayOrder: number }): Observable<ShsStrand> {
    return this.http.put<ShsStrand>(`${this.baseUrl}/shs-strands/${id}`, data);
  }
```

```ts
  getSchoolShsStrands(slug: string): Observable<ShsStrand[]> {
    return this.http.get<ShsStrand[]>(`${this.baseUrl}/schools/${slug}/shs-strands`);
  }
```

- [ ] **Step 5: Keep the build green**

- `student-form.component.ts`: add `profile: null` to both object literals assigned to `form` (initializer and the `getStudent` callback).
- `apply.component.ts`: import `emptyLearnerProfile` from `../../core/learner` and add to `blankApplicant()`'s return object: `lrn: null, learner: emptyLearnerProfile(), learnerType: 'New', lastGradeLevelCompleted: null, lastSchoolYearCompleted: null, lastSchoolId: null, track: null, strand: null`.
- `settings.component.ts`: in `addRequirementTemplate` pass `keepOnFile: false`; in `toggleRequirementTemplate` pass `keepOnFile: t.keepOnFile`; in `bulkReenroll`'s success handler nothing changes yet (Task 21).

Run (from `enrollify.client/`): `npm run build` → succeeds. Fix any remaining type errors caused by `lrn` now being `string | null` by rendering `{{ x.lrn || '—' }}` where it's displayed.

- [ ] **Step 6: Commit**

```bash
git add enrollify.client/src/app
git commit -m "Learner record: frontend models, DepEd constants, API calls

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 15: PSGC dataset, PsgcService, address picker

**Files:**
- Create: `enrollify.client/scripts/psgc/build_psgc.py`
- Create: `enrollify.client/scripts/psgc/README.md`
- Create (generated, committed): `enrollify.client/public/psgc/index.json`, `enrollify.client/public/psgc/barangays/*.json`
- Create: `enrollify.client/src/app/core/services/psgc.service.ts`
- Create: `enrollify.client/src/app/core/components/psgc-address.component.ts`
- Test: `enrollify.client/src/app/core/components/psgc-address.component.spec.ts`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs` (demo addresses)

**Interfaces:**
- Consumes: `PsgcAddress`, `emptyPsgcAddress` (Task 14).
- Produces:
  - Files: `psgc/index.json` = `{ version: string, regions: {code,name}[], provinces: {code,name,regionCode}[], cities: {code,name,regionCode,provinceCode|null}[] }`; `psgc/barangays/<provinceCode or regionCode>.json` = `{ [cityCode]: {code,name}[] }`.
  - `PsgcService.index(): Observable<PsgcIndex>`, `PsgcService.barangaysOf(city: PsgcCity): Observable<PsgcPlace[]>`; exported types `PsgcPlace`, `PsgcProvince`, `PsgcCity`, `PsgcIndex`.
  - `<app-psgc-address [address] [idPrefix] [required] [disabled] (changed)>` — mutates `address` in place; public members used by tests: `selectRegion/selectProvince/selectCity/selectBarangay`, signals `cities()`, `barangays()`, `provinceChoice()`, `showProvinceStep()`, `hasIndependentCities()`, constant `NO_PROVINCE`.

- [ ] **Step 1: Get the official workbook**

Download the latest **PSGC Publication** datafile (`.xlsx`) from the Philippine Statistics Authority: https://psa.gov.ph/classification/psgc (look for "PSGC Publication" → the most recent quarter's datafile). Save it outside the repo (e.g. `%TEMP%`). If the download cannot be done from this environment, stop and ask the human partner to download it and give you the path — do **not** substitute a third-party dataset or hand-type codes.

- [ ] **Step 2: Write the converter**

`enrollify.client/scripts/psgc/build_psgc.py`:

```python
"""
Build Enrollify's PSGC lookup files from the PSA's official PSGC publication workbook.

    python scripts/psgc/build_psgc.py <PSGC-publication.xlsx>      (run from enrollify.client/)

Writes public/psgc/index.json and public/psgc/barangays/<code>.json. Python 3 standard library
only (zipfile + ElementTree read the .xlsx directly). Codes are 10-digit STRINGS: Excel stores
them as numbers, which drops the leading zero of regions 01-09 — normalize_code() restores it.
"""
import json
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

MAIN = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
DOC_REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
PKG_REL = 'http://schemas.openxmlformats.org/package/2006/relationships'
OUT = Path('public/psgc')


def q(tag):
    return '{%s}%s' % (MAIN, tag)


def read_rows(xlsx, sheet_name='PSGC'):
    with zipfile.ZipFile(xlsx) as z:
        shared = []
        if 'xl/sharedStrings.xml' in z.namelist():
            for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall(q('si')):
                shared.append(''.join(t.text or '' for t in si.iter(q('t'))))
        wb = ET.fromstring(z.read('xl/workbook.xml'))
        rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
        targets = {r.get('Id'): r.get('Target') for r in rels.findall('{%s}Relationship' % PKG_REL)}
        sheets = list(wb.find(q('sheets')))
        sheet = next((s for s in sheets if s.get('name', '').strip().upper() == sheet_name), None)
        if sheet is None:
            sys.exit(f'Sheet "{sheet_name}" not found; sheets are: {[s.get("name") for s in sheets]}')
        target = targets[sheet.get('{%s}id' % DOC_REL)].lstrip('/')
        path = target if target.startswith('xl/') else 'xl/' + target
        for row in ET.fromstring(z.read(path)).iter(q('row')):
            cells = {}
            for c in row.findall(q('c')):
                col = re.match(r'[A-Z]+', c.get('r')).group(0)
                kind, v = c.get('t'), c.find(q('v'))
                if kind == 's' and v is not None:
                    val = shared[int(v.text)]
                elif kind == 'inlineStr':
                    val = ''.join(t.text or '' for t in c.iter(q('t')))
                else:
                    val = v.text if v is not None else ''
                cells[col] = (val or '').strip()
            yield cells


def normalize_code(raw):
    s = (raw or '').strip()
    if re.fullmatch(r'\d+\.0+', s):
        s = s.split('.')[0]
    if not re.fullmatch(r'\d{1,10}', s):
        return None
    return s.zfill(10)


def main(xlsx):
    rows = read_rows(xlsx)
    header = None
    for cells in rows:
        texts = {k: v.lower() for k, v in cells.items()}
        if any('psgc' in v for v in texts.values()) and any('geographic level' in v for v in texts.values()):
            header = cells
            break
    if header is None:
        sys.exit('Header row (with "PSGC" and "Geographic Level" columns) not found.')
    code_col = next(k for k, v in header.items() if '10-digit' in v.lower()) if any('10-digit' in v.lower() for v in header.values()) \
        else next(k for k, v in header.items() if 'psgc' in v.lower())
    name_col = next(k for k, v in header.items() if v.strip().lower() == 'name')
    level_col = next(k for k, v in header.items() if 'geographic level' in v.lower())

    places = {}  # code -> (name, level)
    for cells in rows:
        code = normalize_code(cells.get(code_col))
        level = (cells.get(level_col) or '').strip().lower()
        name = (cells.get(name_col) or '').strip()
        if code and level and name:
            places[code] = (name, level)

    def of(*levels):
        return {c: n for c, (n, lv) in places.items() if lv in levels}

    regions, provinces = of('reg'), of('prov')
    cities = of('city', 'mun', 'sgu')        # SGU: BARMM special geographic areas behave like municipalities
    submuns, barangays = of('submun'), of('bgy')

    region_of = lambda c: c[:2] + '0' * 8
    province_slot = lambda c: c[:5] + '0' * 5
    city_slot = lambda c: c[:7] + '000'

    city_rows = []
    for code, name in cities.items():
        p = province_slot(code)
        province = p if p in provinces else None
        city_rows.append({'code': code, 'name': name, 'regionCode': region_of(code), 'provinceCode': province})

    by_file = defaultdict(lambda: defaultdict(list))
    city_index = {c['code']: c for c in city_rows}
    unresolved = []
    for code, name in barangays.items():
        parent = city_slot(code)
        if parent in submuns:                 # e.g. Manila's districts: roll up to the city itself
            parent = province_slot(parent)
        if parent not in city_index:
            parent = province_slot(code) if province_slot(code) in city_index else None
        if parent is None:
            unresolved.append((code, name))
            continue
        city = city_index[parent]
        by_file[city['provinceCode'] or city['regionCode']][parent].append({'code': code, 'name': name})

    missing_regions = {c['regionCode'] for c in city_rows} - set(regions)
    print(f'regions={len(regions)} provinces={len(provinces)} cities/municipalities={len(city_rows)} '
          f'barangays={len(barangays)} unresolved={len(unresolved)} files={len(by_file)}')
    if unresolved:
        print('First unresolved barangays:', unresolved[:10])
        sys.exit('Some barangays could not be placed under a city/municipality — inspect the codes above.')
    if missing_regions or len(regions) < 15 or len(barangays) < 40000:
        sys.exit(f'Sanity check failed (missing regions: {sorted(missing_regions)}).')

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'barangays').mkdir(exist_ok=True)
    for old in (OUT / 'barangays').glob('*.json'):
        old.unlink()
    index = {
        'version': Path(xlsx).name,
        'regions': sorted(({'code': c, 'name': n} for c, n in regions.items()), key=lambda r: r['code']),
        'provinces': sorted(({'code': c, 'name': n, 'regionCode': region_of(c)} for c, n in provinces.items()), key=lambda r: r['name']),
        'cities': sorted(city_rows, key=lambda r: r['name']),
    }
    (OUT / 'index.json').write_text(json.dumps(index, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    for key, cities_in_file in by_file.items():
        payload = {c: sorted(bs, key=lambda b: b['name']) for c, bs in cities_in_file.items()}
        (OUT / 'barangays' / f'{key}.json').write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
    print(f'Wrote {OUT / "index.json"} and {len(by_file)} barangay files.')


if __name__ == '__main__':
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
```

`enrollify.client/scripts/psgc/README.md`:

```markdown
# PSGC lookup data

`public/psgc/` holds Enrollify's copy of the Philippine Standard Geographic Code (PSGC),
generated from the Philippine Statistics Authority's official publication. Address pickers
load `index.json` (regions, provinces, cities/municipalities) up front and one
`barangays/<provinceCode or regionCode>.json` file when a city is chosen.

## Refreshing (PSA republishes several times a year)

1. Download the latest PSGC Publication datafile (.xlsx) from https://psa.gov.ph/classification/psgc
2. From `enrollify.client/`: `python scripts/psgc/build_psgc.py <path-to.xlsx>`
3. Check the printed counts (about 17 regions, 80+ provinces, 1,600+ cities/municipalities,
   42,000+ barangays, 0 unresolved) and commit `public/psgc/`.

Records store codes AND names, so a refresh never changes how existing addresses read.
```

- [ ] **Step 3: Generate and spot-check**

Run (from `enrollify.client/`): `python scripts/psgc/build_psgc.py <path-to.xlsx>`
Expected: a summary line with `unresolved=0`, then `Wrote public\psgc\index.json and N barangay files.`

If the script exits on the header or the unresolved check, open the workbook's `PSGC` sheet, compare its header names and the `Geographic Level` values against the constants in the script (`reg`, `prov`, `city`, `mun`, `sgu`, `submun`, `bgy`), adjust the script to the real values, and re-run. Do not loosen the sanity thresholds to make it pass.

Spot checks (each must print sensible data):

```bash
python -c "import json;ix=json.load(open('public/psgc/index.json',encoding='utf-8'));print(len(ix['regions']),len(ix['provinces']),len(ix['cities']));print([c for c in ix['cities'] if c['name'] in ('City of Manila','Quezon City','Cebu City')])"
```

Expected: the three cities appear; NCR cities and Cebu City have `"provinceCode": null`. Then, using the City of Manila entry's `code` and `regionCode`:

```bash
python -c "import json;b=json.load(open('public/psgc/barangays/<regionCode>.json',encoding='utf-8'));print(len(b['<manilaCode>']), b['<manilaCode>'][:2])"
```

Expected: several hundred barangays for Manila. Check total size: the `public/psgc` folder should be a few MB at most.

- [ ] **Step 4: `PsgcService`**

`enrollify.client/src/app/core/services/psgc.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map, shareReplay } from 'rxjs';

export interface PsgcPlace { code: string; name: string; }
export interface PsgcProvince extends PsgcPlace { regionCode: string; }
export interface PsgcCity extends PsgcPlace { regionCode: string; provinceCode: string | null; }
export interface PsgcIndex { version: string; regions: PsgcPlace[]; provinces: PsgcProvince[]; cities: PsgcCity[]; }

/**
 * Static PSGC lookup files shipped under public/psgc (generated by scripts/psgc/build_psgc.py).
 * The index loads once per session; barangay files load per province (or per region for
 * province-less cities) the first time a city in them is chosen.
 */
@Injectable({ providedIn: 'root' })
export class PsgcService {
  private index$?: Observable<PsgcIndex>;
  private barangayFiles = new Map<string, Observable<Record<string, PsgcPlace[]>>>();

  constructor(private http: HttpClient) {}

  index(): Observable<PsgcIndex> {
    // Relative URL: resolves against <base href>, so it works under any deployment path.
    this.index$ ??= this.http.get<PsgcIndex>('psgc/index.json').pipe(shareReplay(1));
    return this.index$;
  }

  barangaysOf(city: PsgcCity): Observable<PsgcPlace[]> {
    const key = city.provinceCode ?? city.regionCode;
    let file$ = this.barangayFiles.get(key);
    if (!file$) {
      file$ = this.http.get<Record<string, PsgcPlace[]>>(`psgc/barangays/${key}.json`).pipe(shareReplay(1));
      this.barangayFiles.set(key, file$);
    }
    return file$.pipe(map(byCity => byCity[city.code] ?? []));
  }
}
```

- [ ] **Step 5: Write the failing component spec**

`enrollify.client/src/app/core/components/psgc-address.component.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PsgcAddressComponent } from './psgc-address.component';
import { PsgcIndex, PsgcService } from '../services/psgc.service';
import { emptyPsgcAddress } from '../learner';
import { PsgcAddress } from '../models';

const INDEX: PsgcIndex = {
  version: 'test',
  regions: [{ code: '1300000000', name: 'NCR' }, { code: '0700000000', name: 'Region VII' }],
  provinces: [{ code: '0702200000', name: 'Cebu', regionCode: '0700000000' }],
  cities: [
    { code: '1381300000', name: 'Quezon City', regionCode: '1300000000', provinceCode: null },
    { code: '0702201000', name: 'Alcantara', regionCode: '0700000000', provinceCode: '0702200000' },
    { code: '0730600000', name: 'Cebu City', regionCode: '0700000000', provinceCode: null },
  ],
};

const BARANGAYS: Record<string, { code: string; name: string }[]> = {
  '1381300000': [{ code: '1381300001', name: 'Alicia' }],
  '0702201000': [{ code: '0702201001', name: 'Cabadiangan' }],
  '0730600000': [{ code: '0730600001', name: 'Adlaon' }],
};

describe('PsgcAddressComponent', () => {
  function create(address: PsgcAddress = emptyPsgcAddress()): PsgcAddressComponent {
    TestBed.configureTestingModule({
      imports: [PsgcAddressComponent],
      providers: [{
        provide: PsgcService,
        useValue: { index: () => of(INDEX), barangaysOf: (c: { code: string }) => of(BARANGAYS[c.code] ?? []) },
      }],
    });
    const fixture = TestBed.createComponent(PsgcAddressComponent);
    fixture.componentRef.setInput('address', address);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('skips the province step for a region with no provinces (NCR)', () => {
    const c = create();
    c.selectRegion('1300000000');
    expect(c.showProvinceStep()).toBeFalse();
    expect(c.cities().map(x => x.name)).toEqual(['Quezon City']);
  });

  it('changing the region clears province, city and barangay', () => {
    const c = create();
    c.selectRegion('0700000000');
    c.selectProvince('0702200000');
    c.selectCity('0702201000');
    c.selectBarangay('0702201001');
    expect(c.address.barangayName).toBe('Cabadiangan');

    c.selectRegion('1300000000');
    expect(c.address.provinceCode).toBeNull();
    expect(c.address.cityMunicipalityCode).toBeNull();
    expect(c.address.barangayCode).toBeNull();
    expect(c.barangays()).toEqual([]);
  });

  it('changing the province clears city and barangay', () => {
    const c = create();
    c.selectRegion('0700000000');
    c.selectProvince('0702200000');
    c.selectCity('0702201000');
    c.selectProvince(c.NO_PROVINCE);
    expect(c.address.cityMunicipalityCode).toBeNull();
    expect(c.address.barangayCode).toBeNull();
  });

  it('offers independent cities in a region that has provinces', () => {
    const c = create();
    c.selectRegion('0700000000');
    expect(c.hasIndependentCities()).toBeTrue();
    c.selectProvince(c.NO_PROVINCE);
    expect(c.cities().map(x => x.name)).toEqual(['Cebu City']);
    c.selectCity('0730600000');
    expect(c.address.provinceCode).toBeNull();
    expect(c.barangays().map(b => b.name)).toEqual(['Adlaon']);
  });

  it('restores selections from an existing address', () => {
    const c = create({ ...emptyPsgcAddress(), regionCode: '0700000000', cityMunicipalityCode: '0730600000', barangayCode: '0730600001' });
    expect(c.provinceChoice()).toBe(c.NO_PROVINCE);
    expect(c.barangays().length).toBe(1);
  });

  it('emits changed after every selection', () => {
    const c = create();
    const emitted: PsgcAddress[] = [];
    c.changed.subscribe(a => emitted.push({ ...a }));
    c.selectRegion('1300000000');
    c.selectCity('1381300000');
    c.selectBarangay('1381300001');
    expect(emitted.length).toBe(3);
    expect(emitted[2].barangayName).toBe('Alicia');
  });
});
```

Run (from `enrollify.client/`): `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/components/psgc-address.component.spec.ts`
Expected: FAIL (component missing).

- [ ] **Step 6: Implement the component**

`enrollify.client/src/app/core/components/psgc-address.component.ts`:

```ts
import { Component, EventEmitter, Input, OnChanges, Output, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PsgcAddress } from '../models';
import { PsgcCity, PsgcIndex, PsgcPlace, PsgcService } from '../services/psgc.service';

/**
 * Region → (province) → city/municipality → barangay → ZIP, from the shipped PSGC files.
 * Mutates `address` in place (codes + names) and emits (changed) after every edit. Regions with
 * no provinces (NCR) skip the province step; regions that have provinces but also independent
 * cities offer an "Independent / highly urbanized city" choice in the province list.
 */
@Component({
  selector: 'app-psgc-address',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-region'">Region <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-region'" [disabled]="disabled || !index()"
                [ngModel]="address.regionCode" (ngModelChange)="selectRegion($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">{{ index() ? 'Select region' : 'Loading…' }}</option>
          @for (r of index()?.regions ?? []; track r.code) { <option [ngValue]="r.code">{{ r.name }}</option> }
        </select>
      </div>
      @if (showProvinceStep()) {
        <div>
          <label class="form-label" [attr.for]="idPrefix + '-province'">Province <span *ngIf="required">*</span></label>
          <select class="form-input" [id]="idPrefix + '-province'" [disabled]="disabled"
                  [ngModel]="provinceChoice()" (ngModelChange)="selectProvince($event)" [ngModelOptions]="{ standalone: true }">
            <option [ngValue]="null">Select province</option>
            @for (p of provinces(); track p.code) { <option [ngValue]="p.code">{{ p.name }}</option> }
            @if (hasIndependentCities()) { <option [ngValue]="NO_PROVINCE">Independent / highly urbanized city</option> }
          </select>
        </div>
      }
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-city'">City / Municipality <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-city'" [disabled]="disabled || cities().length === 0"
                [ngModel]="address.cityMunicipalityCode" (ngModelChange)="selectCity($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">Select city / municipality</option>
          @for (c of cities(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
        </select>
      </div>
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-barangay'">Barangay <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-barangay'" [disabled]="disabled || barangays().length === 0"
                [ngModel]="address.barangayCode" (ngModelChange)="selectBarangay($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">{{ loadingBarangays() ? 'Loading…' : 'Select barangay' }}</option>
          @for (b of barangays(); track b.code) { <option [ngValue]="b.code">{{ b.name }}</option> }
        </select>
      </div>
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-zip'">ZIP code</label>
        <input class="form-input folio-mono" [id]="idPrefix + '-zip'" inputmode="numeric" maxlength="10" [disabled]="disabled"
               [ngModel]="address.zipCode" (ngModelChange)="setZip($event)" [ngModelOptions]="{ standalone: true }" />
      </div>
    </div>
  `
})
export class PsgcAddressComponent implements OnChanges {
  @Input({ required: true }) address!: PsgcAddress;
  @Input() idPrefix = 'addr';
  @Input() required = false;
  @Input() disabled = false;
  @Output() changed = new EventEmitter<PsgcAddress>();

  readonly NO_PROVINCE = '__none__';

  index = signal<PsgcIndex | null>(null);
  provinceChoice = signal<string | null>(null);
  barangays = signal<PsgcPlace[]>([]);
  loadingBarangays = signal(false);
  private regionCode = signal<string | null>(null);

  provinces = computed(() => (this.index()?.provinces ?? []).filter(p => p.regionCode === this.regionCode()));
  private regionCities = computed(() => (this.index()?.cities ?? []).filter(c => c.regionCode === this.regionCode()));
  hasIndependentCities = computed(() => this.regionCities().some(c => c.provinceCode === null));
  showProvinceStep = computed(() => this.provinces().length > 0);
  cities = computed(() => {
    const all = this.regionCities();
    if (!this.showProvinceStep()) return all;
    const choice = this.provinceChoice();
    if (choice === this.NO_PROVINCE) return all.filter(c => c.provinceCode === null);
    return choice ? all.filter(c => c.provinceCode === choice) : [];
  });

  constructor(private psgc: PsgcService) {
    this.psgc.index().subscribe(ix => { this.index.set(ix); this.syncFromAddress(); });
  }

  ngOnChanges(): void { this.syncFromAddress(); }

  selectRegion(code: string | null): void {
    const r = this.index()?.regions.find(x => x.code === code) ?? null;
    Object.assign(this.address, {
      regionCode: r?.code ?? null, regionName: r?.name ?? null,
      provinceCode: null, provinceName: null,
      cityMunicipalityCode: null, cityMunicipalityName: null,
      barangayCode: null, barangayName: null,
    });
    this.regionCode.set(this.address.regionCode);
    this.provinceChoice.set(null);
    this.barangays.set([]);
    this.changed.emit(this.address);
  }

  selectProvince(choice: string | null): void {
    const p = this.provinces().find(x => x.code === choice) ?? null;
    Object.assign(this.address, {
      provinceCode: p?.code ?? null, provinceName: p?.name ?? null,
      cityMunicipalityCode: null, cityMunicipalityName: null,
      barangayCode: null, barangayName: null,
    });
    this.provinceChoice.set(choice);
    this.barangays.set([]);
    this.changed.emit(this.address);
  }

  selectCity(code: string | null): void {
    const c = this.cityByCode(code);
    Object.assign(this.address, {
      cityMunicipalityCode: c?.code ?? null, cityMunicipalityName: c?.name ?? null,
      barangayCode: null, barangayName: null,
    });
    if (c && c.provinceCode === null) Object.assign(this.address, { provinceCode: null, provinceName: null });
    this.barangays.set([]);
    if (c) this.loadBarangays(c);
    this.changed.emit(this.address);
  }

  selectBarangay(code: string | null): void {
    const b = this.barangays().find(x => x.code === code) ?? null;
    Object.assign(this.address, { barangayCode: b?.code ?? null, barangayName: b?.name ?? null });
    this.changed.emit(this.address);
  }

  setZip(value: string | null): void {
    this.address.zipCode = value?.trim() || null;
    this.changed.emit(this.address);
  }

  private syncFromAddress(): void {
    const a = this.address;
    if (!a || !this.index()) return;
    this.regionCode.set(a.regionCode);
    this.provinceChoice.set(a.provinceCode ?? (a.cityMunicipalityCode ? this.NO_PROVINCE : null));
    const city = this.cityByCode(a.cityMunicipalityCode);
    if (city) this.loadBarangays(city);
    else this.barangays.set([]);
  }

  private cityByCode(code: string | null): PsgcCity | undefined {
    return code ? this.index()?.cities.find(c => c.code === code) : undefined;
  }

  private loadBarangays(city: PsgcCity): void {
    this.loadingBarangays.set(true);
    this.psgc.barangaysOf(city).subscribe({
      next: list => { this.barangays.set(list); this.loadingBarangays.set(false); },
      error: () => { this.barangays.set([]); this.loadingBarangays.set(false); },
    });
  }
}
```

Run the spec again → PASS. Run `npm run build` → succeeds.

- [ ] **Step 7: Demo addresses in the seeder**

Using the codes printed in Step 3, set `HomeAddress` on the demo students in `ApplicationDbContextSeed.cs`: Juan (City of Manila + one of its barangays) and Jose (Quezon City + one of its barangays), each with region code/name, city code/name, barangay code/name — exactly as they appear in the generated JSON (names included). Leave Maria without a PSGC address (she demonstrates the "Address not matched to PSGC yet" hint). Add `using Enrollify.Domain.ValueObjects;`. Run `dotnet build Enrollify.sln` (from `backend/`) → 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add enrollify.client/scripts enrollify.client/public/psgc enrollify.client/src/app/core backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs
git commit -m "Learner record: PSGC dataset from PSA, PsgcService, address picker

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 16: Shared form components; apply form custom fields de-duplicated

**Files:**
- Create: `enrollify.client/src/app/core/components/dynamic-field.component.ts`
- Create: `enrollify.client/src/app/core/components/learner-background-fields.component.ts`
- Create: `enrollify.client/src/app/core/components/learner-family-fields.component.ts`
- Create: `enrollify.client/src/app/core/components/strand-select.component.ts`
- Create: `enrollify.client/src/app/core/components/learner-summary.component.ts`
- Modify: `enrollify.client/src/app/pages/apply/apply.component.ts` (custom-field blocks only — behavior unchanged)

**Interfaces:**
- Consumes: `ApplicationFormField`, `LearnerProfile`, `ShsStrand`, `FieldConfigView`, `DISABILITY_TYPES` (Task 14).
- Produces:
  - `<app-dynamic-field [field] [name] [value] (valueChange)>` — renders one admin-defined field (`valueChange` emits a string; checkboxes emit `'true'`/`'false'`).
  - `<app-learner-background-fields [profile] [config] [namePrefix]>` — PSA no., place of birth, mother tongue, religion, IP, 4Ps, disability; mutates `profile`.
  - `<app-learner-family-fields [profile] [config] [namePrefix]>` — father / mother's maiden name + contacts; mutates `profile`.
  - `<app-strand-select [strands] [track] [strand] [required] [disabled] [label] [id] (changed)>` — emits `{ track, strand }` (both null to clear); shows a saved-but-no-longer-offered pair as a disabled option.
  - `<app-learner-summary [student]>` — read-only DepEd details for family pages.

- [ ] **Step 1: `DynamicFieldComponent`**

`enrollify.client/src/app/core/components/dynamic-field.component.ts`:

```ts
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApplicationFormField } from '../models';

/** One admin-defined (custom) application-form field. Values are strings; checkboxes use 'true'/'false'. */
@Component({
  selector: 'app-dynamic-field',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <label class="form-label" [attr.for]="name">{{ field.label }} <span *ngIf="field.isRequired">*</span></label>
    @switch (field.fieldType) {
      @case ('TextArea') {
        <textarea [id]="name" [name]="name" rows="3" class="form-input" [required]="field.isRequired"
                  [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }"></textarea>
      }
      @case ('Number') {
        <input type="number" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
      @case ('Date') {
        <input type="date" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
      @case ('Checkbox') {
        <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700">
          <input type="checkbox" [id]="name" [name]="name"
                 [ngModel]="value === 'true'" (ngModelChange)="emit($event ? 'true' : 'false')" [ngModelOptions]="{ standalone: true }" />
          Yes
        </label>
      }
      @case ('Dropdown') {
        <select [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
                [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }">
          <option value="">Select</option>
          @for (opt of options(); track opt) { <option [value]="opt">{{ opt }}</option> }
        </select>
      }
      @default {
        <input type="text" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
    }
    @if (field.helpText) { <p class="mt-1 text-xs text-gray-400">{{ field.helpText }}</p> }
  `
})
export class DynamicFieldComponent {
  @Input({ required: true }) field!: ApplicationFormField;
  @Input({ required: true }) name!: string;
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();

  options(): string[] {
    if (!this.field.options) return [];
    try {
      const parsed = JSON.parse(this.field.options);
      return Array.isArray(parsed) ? parsed.map(String) : [];
    } catch { return []; }
  }

  emit(v: unknown): void {
    this.valueChange.emit(v === null || v === undefined ? '' : String(v));
  }
}
```

- [ ] **Step 2: Learner field components**

`enrollify.client/src/app/core/components/learner-background-fields.component.ts`:

```ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LearnerProfile } from '../models';
import { DISABILITY_TYPES } from '../constants';
import { FieldConfigView } from '../learner';

/**
 * DepEd background fields (BEEF): PSA no., place of birth, mother tongue, religion, and the
 * IP / 4Ps / disability yes-no questions with their follow-ups. Mutates `profile` in place.
 * Visibility/labels come from `config` (school form settings on /apply, everything on staff forms).
 */
@Component({
  selector: 'app-learner-background-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      @if (config.visible('psaBirthCertificateNumber')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-psa'">{{ config.label('psaBirthCertificateNumber', 'PSA Birth Certificate No.') }} <span *ngIf="config.required('psaBirthCertificateNumber')">*</span></label>
          <input type="text" maxlength="30" class="form-input folio-mono" [id]="namePrefix + '-psa'"
                 [ngModel]="profile.psaBirthCertificateNumber" (ngModelChange)="profile.psaBirthCertificateNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('placeOfBirth')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-pob'">{{ config.label('placeOfBirth', 'Place of Birth (City/Municipality)') }} <span *ngIf="config.required('placeOfBirth')">*</span></label>
          <input type="text" maxlength="200" class="form-input" [id]="namePrefix + '-pob'"
                 [ngModel]="profile.placeOfBirth" (ngModelChange)="profile.placeOfBirth = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('motherTongue')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-mt'">{{ config.label('motherTongue', 'Mother Tongue') }} <span *ngIf="config.required('motherTongue')">*</span></label>
          <input type="text" maxlength="100" class="form-input" [id]="namePrefix + '-mt'"
                 [ngModel]="profile.motherTongue" (ngModelChange)="profile.motherTongue = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('religion')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-rel'">{{ config.label('religion', 'Religion') }} <span *ngIf="config.required('religion')">*</span></label>
          <input type="text" maxlength="100" class="form-input" [id]="namePrefix + '-rel'"
                 [ngModel]="profile.religion" (ngModelChange)="profile.religion = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
    </div>

    <div class="mt-4 space-y-3">
      @if (config.visible('indigenousPeople')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.isIndigenousPeople" (ngModelChange)="profile.isIndigenousPeople = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('indigenousPeople', 'Belongs to an Indigenous Peoples (IP) community') }}
          </label>
          @if (profile.isIndigenousPeople) {
            <input type="text" maxlength="100" class="form-input mt-2" placeholder="Which community?"
                   [ngModel]="profile.ipCommunity" (ngModelChange)="profile.ipCommunity = $event || null" [ngModelOptions]="{ standalone: true }" />
          }
        </div>
      }
      @if (config.visible('fourPs')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.is4PsBeneficiary" (ngModelChange)="profile.is4PsBeneficiary = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('fourPs', '4Ps beneficiary household') }}
          </label>
          @if (profile.is4PsBeneficiary) {
            <input type="text" maxlength="30" class="form-input folio-mono mt-2" placeholder="4Ps household ID"
                   [ngModel]="profile.fourPsHouseholdId" (ngModelChange)="profile.fourPsHouseholdId = $event || null" [ngModelOptions]="{ standalone: true }" />
          }
        </div>
      }
      @if (config.visible('disability')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.hasDisability" (ngModelChange)="profile.hasDisability = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('disability', 'Learner with disability') }}
          </label>
          @if (profile.hasDisability) {
            <div class="mt-2 grid grid-cols-1 sm:grid-cols-2 gap-1.5 rounded-md border border-[#E2D9C2] p-3">
              @for (t of disabilityTypes; track t) {
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [checked]="hasType(t)" (change)="toggleType(t, $any($event.target).checked)" />
                  {{ t }}
                </label>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class LearnerBackgroundFieldsComponent {
  @Input({ required: true }) profile!: LearnerProfile;
  @Input({ required: true }) config!: FieldConfigView;
  @Input() namePrefix = 'learner';

  readonly disabilityTypes = DISABILITY_TYPES;

  hasType(t: string): boolean {
    return (this.profile.disabilityTypes ?? []).includes(t);
  }

  toggleType(t: string, on: boolean): void {
    const current = new Set(this.profile.disabilityTypes ?? []);
    if (on) current.add(t); else current.delete(t);
    this.profile.disabilityTypes = DISABILITY_TYPES.filter(x => current.has(x));
  }
}
```

`enrollify.client/src/app/core/components/learner-family-fields.component.ts`:

```ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LearnerProfile } from '../models';
import { FieldConfigView } from '../learner';

/** Father's name and mother's maiden name (last / first / middle + contact). Mutates `profile`. */
@Component({
  selector: 'app-learner-family-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (config.visible('fatherName')) {
      <p class="text-sm font-medium text-gray-700 mb-2">{{ config.label('fatherName', "Father's Name") }} <span *ngIf="config.required('fatherName')">*</span></p>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3 mb-4">
        <input type="text" maxlength="100" class="form-input" placeholder="Last name" [attr.aria-label]="'Father last name'"
               [ngModel]="profile.fatherLastName" (ngModelChange)="profile.fatherLastName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="First name" [attr.aria-label]="'Father first name'"
               [ngModel]="profile.fatherFirstName" (ngModelChange)="profile.fatherFirstName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="Middle name" [attr.aria-label]="'Father middle name'"
               [ngModel]="profile.fatherMiddleName" (ngModelChange)="profile.fatherMiddleName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="50" class="form-input" placeholder="Contact number" [attr.aria-label]="'Father contact number'"
               [ngModel]="profile.fatherContactNumber" (ngModelChange)="profile.fatherContactNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
      </div>
    }
    @if (config.visible('motherMaidenName')) {
      <p class="text-sm font-medium text-gray-700 mb-2">{{ config.label('motherMaidenName', "Mother's Maiden Name") }} <span *ngIf="config.required('motherMaidenName')">*</span></p>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
        <input type="text" maxlength="100" class="form-input" placeholder="Last name" [attr.aria-label]="'Mother maiden last name'"
               [ngModel]="profile.motherMaidenLastName" (ngModelChange)="profile.motherMaidenLastName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="First name" [attr.aria-label]="'Mother first name'"
               [ngModel]="profile.motherMaidenFirstName" (ngModelChange)="profile.motherMaidenFirstName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="Middle name" [attr.aria-label]="'Mother maiden middle name'"
               [ngModel]="profile.motherMaidenMiddleName" (ngModelChange)="profile.motherMaidenMiddleName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="50" class="form-input" placeholder="Contact number" [attr.aria-label]="'Mother contact number'"
               [ngModel]="profile.motherContactNumber" (ngModelChange)="profile.motherContactNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
      </div>
    }
  `
})
export class LearnerFamilyFieldsComponent {
  @Input({ required: true }) profile!: LearnerProfile;
  @Input({ required: true }) config!: FieldConfigView;
  @Input() namePrefix = 'family';
}
```

`enrollify.client/src/app/core/components/strand-select.component.ts`:

```ts
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ShsStrand } from '../models';

/** SHS track/strand picker grouped by track. Emits { track, strand } (both null to clear). */
@Component({
  selector: 'app-strand-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <label class="form-label" [attr.for]="id">{{ label }} <span *ngIf="required">*</span></label>
    <select class="form-input" [id]="id" [disabled]="disabled"
            [ngModel]="selectedId()" (ngModelChange)="select($event)" [ngModelOptions]="{ standalone: true }">
      <option [ngValue]="null">Select track / strand</option>
      @if (selectedId() === LEGACY) {
        <option [ngValue]="LEGACY" disabled>{{ track }} – {{ strand }} (no longer offered)</option>
      }
      @for (g of groups(); track g.track) {
        <optgroup [label]="g.track">
          @for (s of g.items; track s.id) { <option [ngValue]="s.id">{{ s.strand }}</option> }
        </optgroup>
      }
    </select>
  `
})
export class StrandSelectComponent {
  @Input() strands: ShsStrand[] = [];
  @Input() track: string | null = null;
  @Input() strand: string | null = null;
  @Input() required = false;
  @Input() disabled = false;
  @Input() label = 'Track / Strand';
  @Input() id = 'strand';
  @Output() changed = new EventEmitter<{ track: string | null; strand: string | null }>();

  readonly LEGACY = '__legacy__';

  groups(): { track: string; items: ShsStrand[] }[] {
    const out: { track: string; items: ShsStrand[] }[] = [];
    for (const s of this.strands) {
      let g = out.find(x => x.track === s.track);
      if (!g) { g = { track: s.track, items: [] }; out.push(g); }
      g.items.push(s);
    }
    return out;
  }

  selectedId(): string | null {
    if (!this.track || !this.strand) return null;
    const eq = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();
    const m = this.strands.find(s => eq(s.track, this.track!) && eq(s.strand, this.strand!));
    return m ? m.id : this.LEGACY;
  }

  select(id: string | null): void {
    const s = this.strands.find(x => x.id === id);
    this.changed.emit({ track: s?.track ?? null, strand: s?.strand ?? null });
  }
}
```

`enrollify.client/src/app/core/components/learner-summary.component.ts`:

```ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Student } from '../models';

/** Read-only DepEd details for parent/student profile pages. Families can't edit these. */
@Component({
  selector: 'app-learner-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <dl class="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-3 text-sm">
      <div><dt class="text-xs text-gray-500">Student No.</dt><dd class="folio-mono text-gray-900">{{ student.studentNumber || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">LRN</dt><dd class="folio-mono text-gray-900">{{ student.lrn || 'No LRN yet' }}</dd></div>
      <div><dt class="text-xs text-gray-500">PSA Birth Certificate No.</dt><dd class="folio-mono text-gray-900">{{ p?.psaBirthCertificateNumber || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Place of Birth</dt><dd class="text-gray-900">{{ p?.placeOfBirth || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Mother Tongue</dt><dd class="text-gray-900">{{ p?.motherTongue || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Religion</dt><dd class="text-gray-900">{{ p?.religion || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Indigenous Peoples community</dt><dd class="text-gray-900">{{ p?.isIndigenousPeople ? (p?.ipCommunity || 'Yes') : 'No' }}</dd></div>
      <div><dt class="text-xs text-gray-500">4Ps beneficiary</dt><dd class="text-gray-900">{{ p?.is4PsBeneficiary ? 'Yes' : 'No' }}</dd></div>
      <div class="md:col-span-2"><dt class="text-xs text-gray-500">Learner with disability</dt><dd class="text-gray-900">{{ p?.hasDisability ? (p?.disabilityTypes?.join(', ') || 'Yes') : 'No' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Father</dt><dd class="text-gray-900">{{ fullName(p?.fatherLastName, p?.fatherFirstName, p?.fatherMiddleName) }}</dd></div>
      <div><dt class="text-xs text-gray-500">Mother (maiden name)</dt><dd class="text-gray-900">{{ fullName(p?.motherMaidenLastName, p?.motherMaidenFirstName, p?.motherMaidenMiddleName) }}</dd></div>
    </dl>
    <p class="mt-4 text-xs text-gray-500">Contact the registrar to correct these details.</p>
  `
})
export class LearnerSummaryComponent {
  @Input({ required: true }) student!: Student;

  get p() { return this.student.profile; }

  fullName(last?: string | null, first?: string | null, middle?: string | null): string {
    if (!last && !first) return '—';
    return `${last ?? ''}, ${first ?? ''} ${middle ?? ''}`.trim();
  }
}
```

- [ ] **Step 3: Apply form uses `DynamicFieldComponent`**

In `apply.component.ts`: add `DynamicFieldComponent` to `imports`. Replace the four custom-field `@switch` blocks:

- Parent section block (the `@for (cf of customFieldsFor('Parent'); ...)` body) with:

```html
                  @for (cf of customFieldsFor('Parent'); track cf.id) {
                    <div class="mt-4">
                      <app-dynamic-field [field]="cf" [name]="'parent_' + cf.fieldKey"
                                         [value]="parentCustomValue(cf.fieldKey)" (valueChange)="setParentCustomValue(cf.fieldKey, $event)" />
                    </div>
                  }
```

- The Student, Enrollment and Guardian blocks with (keeping each block's `customFieldsFor('<Section>')`):

```html
                  @for (cf of customFieldsFor('Student'); track cf.id) {
                    <div class="mt-4">
                      <app-dynamic-field [field]="cf" [name]="'cf_' + i + '_' + cf.fieldKey"
                                         [value]="customValue(i, cf.fieldKey)" (valueChange)="setCustomValue(i, cf.fieldKey, $event)" />
                    </div>
                  }
```

Delete the now-unused methods `customDropdownOptions`, `customCheckboxValue`, `setCustomCheckboxValue`, `parentCustomCheckboxValue`, `setParentCustomCheckboxValue`.

- [ ] **Step 4: Build and smoke**

Run: `npm run build` → succeeds. Run the PSGC spec again → still PASS.
Smoke (dev server, `npm start`, backend running): open `/mshs/apply`, add a custom Checkbox and a Dropdown field in Settings → Application Form, confirm both render, can be filled, and submit (the application detail in Admissions shows the values exactly as before).

- [ ] **Step 5: Commit**

```bash
git add enrollify.client/src/app
git commit -m "Learner record: shared learner form components; apply custom fields de-duplicated

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 17: Apply form collects the learner record

**Files:**
- Modify: `enrollify.client/src/app/pages/apply/apply.component.ts`

**Interfaces:**
- Consumes: `PsgcAddressComponent` (Task 15); `LearnerBackgroundFieldsComponent`, `LearnerFamilyFieldsComponent`, `StrandSelectComponent`, `DynamicFieldComponent` (Task 16); `ApiService.getSchoolShsStrands/getShsStrands` (Task 14); constants `LEARNER_TYPES`, `EXTENSION_NAME_SUGGESTIONS`, `isShsGrade`, `GRADE_LEVELS`.
- Produces: the public `/apply` and authenticated `/apply` forms submit `lrn`, `learner` (incl. `homeAddress`), `learnerType`, `lastGradeLevelCompleted`, `lastSchoolYearCompleted`, `lastSchoolId`, `track`, `strand` per applicant.

- [ ] **Step 1: Component state**

In `apply.component.ts`:

1. Imports: add `PsgcAddressComponent`, `LearnerBackgroundFieldsComponent`, `LearnerFamilyFieldsComponent`, `StrandSelectComponent` to the component `imports`; import `ShsStrand` from models; `LEARNER_TYPES, EXTENSION_NAME_SUGGESTIONS, isShsGrade` from constants; `FieldConfigView` from `../../core/learner`.
2. Class members:

```ts
  strands = signal<ShsStrand[]>([]);
  learnerTypes = LEARNER_TYPES;
  extensionNames = EXTENSION_NAME_SUGGESTIONS;

  // School form settings, in the shape the shared learner field components expect.
  fieldView: FieldConfigView = {
    visible: k => this.isFieldVisible(k),
    required: k => this.isFieldRequired(k),
    label: (k, f) => this.fieldLabel(k, f),
  };

  fieldHelp(key: string): string {
    return this.getField(key)?.helpText ?? '';
  }

  showStrand(a: ApplicantData): boolean {
    return isShsGrade(a.gradeLevel) && this.strands().length > 0;
  }

  onGradeChange(a: ApplicantData, grade: string): void {
    a.gradeLevel = grade;
    if (!isShsGrade(grade)) { a.track = null; a.strand = null; }
  }
```

3. In the constructor: in the slug branch add `this.api.getSchoolShsStrands(this.slug).subscribe(list => this.strands.set(list));`; in the authenticated branch add `this.api.getShsStrands().subscribe(list => this.strands.set(list));`.

- [ ] **Step 2: Template — student identity, address, background**

Inside the applicant card:

1. Directly after the first name grid (`First / Middle / Last`), insert:

```html
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
                    @if (isFieldVisible('lrn')) {
                      <div>
                        <label class="form-label" [attr.for]="'lrn_' + i">{{ fieldLabel('lrn', 'LRN (Learner Reference Number)') }} <span *ngIf="isFieldRequired('lrn')">*</span></label>
                        <input type="text" inputmode="numeric" maxlength="12" class="form-input folio-mono" [id]="'lrn_' + i" [name]="'lrn_' + i"
                               [ngModel]="a.lrn" (ngModelChange)="a.lrn = $event || null" />
                        @if (fieldHelp('lrn')) { <p class="mt-1 text-xs text-gray-400">{{ fieldHelp('lrn') }}</p> }
                      </div>
                    }
                    @if (isFieldVisible('extensionName')) {
                      <div>
                        <label class="form-label" [attr.for]="'ext_' + i">{{ fieldLabel('extensionName', 'Name Extension (Jr., III)') }}</label>
                        <input type="text" maxlength="10" class="form-input" list="extension-names" [id]="'ext_' + i" [name]="'ext_' + i"
                               [ngModel]="a.learner.extensionName" (ngModelChange)="a.learner.extensionName = $event || null" />
                      </div>
                    }
                  </div>
```

and once, just inside the `<form>` element (before the mode toggle): `<datalist id="extension-names">@for (x of extensionNames; track x) { <option [value]="x"></option> }</datalist>`.

2. Replace the `@if (isFieldVisible('address')) { ... }` block (inside the contact/address grid) with nothing, and add after that grid:

```html
                  @if (isFieldVisible('address')) {
                    <div class="mt-4">
                      <p class="form-label">{{ fieldLabel('address', 'Home Address') }} <span *ngIf="isFieldRequired('address')">*</span></p>
                      <app-psgc-address [address]="a.learner.homeAddress!" [idPrefix]="'addr-' + i" [required]="isFieldRequired('address')" />
                      <div class="mt-4">
                        <label class="form-label" [attr.for]="'street_' + i">House no. / Street / Sitio / Purok</label>
                        <input type="text" maxlength="500" class="form-input" [id]="'street_' + i" [name]="'address_' + i"
                               [ngModel]="a.address" (ngModelChange)="a.address = $event || null" />
                      </div>
                    </div>
                  }

                  <div class="mt-4">
                    <app-learner-background-fields [profile]="a.learner" [config]="fieldView" [namePrefix]="'bg-' + i" />
                  </div>
```

(If the contact grid is left with only `contactNumber`, keep its wrapper — an empty grid cell is fine.)

- [ ] **Step 3: Template — Family section**

Before the `<hr>` + "Enrollment details" eyebrow, insert:

```html
                  @if (isFieldVisible('fatherName') || isFieldVisible('motherMaidenName') || customFieldsFor('Family').length > 0) {
                    <hr class="my-5 border-[#EFE9D8]" />
                    <p class="folio-eyebrow mb-3">Family</p>
                    <app-learner-family-fields [profile]="a.learner" [config]="fieldView" [namePrefix]="'fam-' + i" />
                    @for (cf of customFieldsFor('Family'); track cf.id) {
                      <div class="mt-4">
                        <app-dynamic-field [field]="cf" [name]="'cf_' + i + '_' + cf.fieldKey"
                                           [value]="customValue(i, cf.fieldKey)" (valueChange)="setCustomValue(i, cf.fieldKey, $event)" />
                      </div>
                    }
                  }
```

- [ ] **Step 4: Template — Enrollment section**

1. Change the grade `<select [(ngModel)]="a.gradeLevel" ...>` to `<select [ngModel]="a.gradeLevel" (ngModelChange)="onGradeChange(a, $event)" [name]="'gradeLevel_' + i" required class="form-input">`.
2. After the grade / school-year grid, insert:

```html
                  @if (showStrand(a)) {
                    <div class="mt-4">
                      <app-strand-select [strands]="strands()" [track]="a.track" [strand]="a.strand" [required]="true"
                                         [id]="'strand_' + i" [label]="fieldLabel('strand', 'Track / Strand')"
                                         (changed)="a.track = $event.track; a.strand = $event.strand" />
                    </div>
                  }
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                    @if (isFieldVisible('learnerType')) {
                      <div>
                        <label class="form-label" [attr.for]="'ltype_' + i">{{ fieldLabel('learnerType', 'Learner Type') }} <span *ngIf="isFieldRequired('learnerType')">*</span></label>
                        <select class="form-input" [id]="'ltype_' + i" [name]="'ltype_' + i" [ngModel]="a.learnerType" (ngModelChange)="a.learnerType = $event || null">
                          @for (t of learnerTypes; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
                        </select>
                      </div>
                    }
                    @if (isFieldVisible('lastGradeLevelCompleted')) {
                      <div>
                        <label class="form-label" [attr.for]="'lastgrade_' + i">{{ fieldLabel('lastGradeLevelCompleted', 'Last Grade Level Completed') }} <span *ngIf="isFieldRequired('lastGradeLevelCompleted')">*</span></label>
                        <select class="form-input" [id]="'lastgrade_' + i" [name]="'lastgrade_' + i" [ngModel]="a.lastGradeLevelCompleted" (ngModelChange)="a.lastGradeLevelCompleted = $event || null">
                          <option [ngValue]="null">Not applicable</option>
                          @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                        </select>
                      </div>
                    }
                    @if (isFieldVisible('lastSchoolYearCompleted')) {
                      <div>
                        <label class="form-label" [attr.for]="'lastsy_' + i">{{ fieldLabel('lastSchoolYearCompleted', 'Last School Year Completed') }} <span *ngIf="isFieldRequired('lastSchoolYearCompleted')">*</span></label>
                        <input type="text" maxlength="9" placeholder="2025-2026" class="form-input folio-mono" [id]="'lastsy_' + i" [name]="'lastsy_' + i"
                               [ngModel]="a.lastSchoolYearCompleted" (ngModelChange)="a.lastSchoolYearCompleted = $event || null" />
                      </div>
                    }
                    @if (isFieldVisible('lastSchoolId')) {
                      <div>
                        <label class="form-label" [attr.for]="'lastsid_' + i">{{ fieldLabel('lastSchoolId', "Last School's DepEd School ID") }} <span *ngIf="isFieldRequired('lastSchoolId')">*</span></label>
                        <input type="text" inputmode="numeric" maxlength="6" class="form-input folio-mono" [id]="'lastsid_' + i" [name]="'lastsid_' + i"
                               [ngModel]="a.lastSchoolId" (ngModelChange)="a.lastSchoolId = $event || null" />
                      </div>
                    }
                  </div>
```

3. Change the two previous-school fallback labels to `fieldLabel('previousSchool', 'Last School Attended')` and `fieldLabel('previousSchoolAddress', 'Last School Address')`.

- [ ] **Step 5: Build and verify in the browser**

Run: `npm run build` → succeeds.
With backend + `npm start` running, open `/mshs/apply` and check:
- A Grade 3 child shows no strand picker; switching that child to Grade 11 shows it; switching back clears it.
- NCR → Quezon City → barangay list loads; Region VII → "Independent / highly urbanized city" → Cebu City works.
- Ticking "Learner with disability" reveals the type checklist; unticking hides it.
- Submit a parent application with a Grade 11 child (strand chosen) and a Grade 3 child: success screen shows two application numbers. Submitting the Grade 11 child without a strand shows the server message naming that child.
- At 375px width the form has no horizontal scroll.

- [ ] **Step 6: Commit**

```bash
git add enrollify.client/src/app/pages/apply
git commit -m "Learner record: apply form collects LRN, DepEd fields, PSGC address, strand

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 18: Staff student form and students list

**Files:**
- Modify: `enrollify.client/src/app/pages/students/student-form/student-form.component.ts` (rewrite)
- Modify: `enrollify.client/src/app/pages/students/student-list/student-list.component.ts`

**Interfaces:**
- Consumes: `ApiService.getStudent/createStudent/updateStudent/confirmLrn/getStudents(…, flags)` (Task 14); `PsgcAddressComponent` (Task 15); `LearnerBackgroundFieldsComponent`, `LearnerFamilyFieldsComponent` (Task 16); `ALL_FIELDS_VISIBLE`, `editableProfile`, `emptyLearnerProfile`, `isPsgcMatched` (Task 14).
- Produces: UI only.

- [ ] **Step 1: Rewrite the student form**

Replace `student-form.component.ts` with:

```ts
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CreateStudentRequest, LearnerProfile } from '../../../core/models';
import { ALL_FIELDS_VISIBLE, editableProfile, emptyLearnerProfile, isPsgcMatched } from '../../../core/learner';
import { EXTENSION_NAME_SUGGESTIONS } from '../../../core/constants';
import { formatApiError } from '../../../core/utils/format-api-error';
import { PsgcAddressComponent } from '../../../core/components/psgc-address.component';
import { LearnerBackgroundFieldsComponent } from '../../../core/components/learner-background-fields.component';
import { LearnerFamilyFieldsComponent } from '../../../core/components/learner-family-fields.component';

type StudentForm = Omit<CreateStudentRequest, 'profile'> & { profile: LearnerProfile };

@Component({
  selector: 'app-student-form',
  standalone: true,
  imports: [CommonModule, FormsModule, PsgcAddressComponent, LearnerBackgroundFieldsComponent, LearnerFamilyFieldsComponent],
  template: `
    <div class="max-w-3xl">
      <h2 class="text-2xl font-bold text-gray-900">{{ isEdit() ? 'Edit' : 'New' }} Student</h2>
      @if (isEdit() && studentNumber()) {
        <p class="mt-1 text-sm text-gray-500">Student No. <span class="folio-mono text-gray-800">{{ studentNumber() }}</span></p>
      }

      <div class="mt-6 space-y-4">
        @if (error()) {
          <div class="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-sm whitespace-pre-line">{{ error() }}</div>
        }
        @if (lrnNeedsReview()) {
          <div class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
            <span><strong>Check LRN.</strong> This LRN was recorded before Enrollify tracked DepEd-issued LRNs. Confirm it matches the learner's LIS record, or correct it below and save.</span>
            <button type="button" (click)="confirmLrn()" [disabled]="confirming()" class="btn btn-secondary">{{ confirming() ? 'Confirming…' : 'Confirm LRN' }}</button>
          </div>
        }
      </div>

      <form (ngSubmit)="onSubmit()" class="mt-4 space-y-6">
        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Identity</h3>
          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label class="form-label" for="firstName">First Name *</label>
              <input id="firstName" name="firstName" type="text" required maxlength="100" class="form-input" [(ngModel)]="form.firstName" />
            </div>
            <div>
              <label class="form-label" for="middleName">Middle Name</label>
              <input id="middleName" name="middleName" type="text" maxlength="100" class="form-input" [(ngModel)]="form.middleName" />
            </div>
            <div>
              <label class="form-label" for="lastName">Last Name *</label>
              <input id="lastName" name="lastName" type="text" required maxlength="100" class="form-input" [(ngModel)]="form.lastName" />
            </div>
            <div>
              <label class="form-label" for="extensionName">Extension</label>
              <input id="extensionName" name="extensionName" type="text" maxlength="10" list="staff-extension-names" class="form-input"
                     [ngModel]="form.profile.extensionName" (ngModelChange)="form.profile.extensionName = $event || null" />
            </div>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="form-label" for="lrn">LRN</label>
              <input id="lrn" name="lrn" type="text" inputmode="numeric" maxlength="20" class="form-input folio-mono" [(ngModel)]="form.lrn" />
              <p class="mt-1 text-xs text-gray-400">12 digits from DepEd LIS. Leave blank if none yet.</p>
            </div>
            <div>
              <label class="form-label" for="birthDate">Birth Date *</label>
              <input id="birthDate" name="birthDate" type="date" required class="form-input" [(ngModel)]="form.birthDate" />
            </div>
            <div>
              <label class="form-label" for="gender">Sex</label>
              <select id="gender" name="gender" class="form-input" [(ngModel)]="form.gender">
                <option [ngValue]="null">Select</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </select>
            </div>
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Background</h3>
          <app-learner-background-fields [profile]="form.profile" [config]="allFields" namePrefix="staff-bg" />
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Address</h3>
          @if (showLegacyAddressHint()) {
            <p class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-3">
              Address not matched to PSGC yet. On file: <strong>{{ form.address }}</strong>. Choose the region, city/municipality and barangay below.
            </p>
          }
          <app-psgc-address [address]="form.profile.homeAddress!" idPrefix="staff-addr" [required]="!isEdit()" />
          <div>
            <label class="form-label" for="street">House no. / Street / Sitio / Purok</label>
            <input id="street" name="address" type="text" maxlength="500" class="form-input" [(ngModel)]="form.address" />
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Family</h3>
          <app-learner-family-fields [profile]="form.profile" [config]="allFields" namePrefix="staff-fam" />
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="guardianName">Guardian Name</label>
              <input id="guardianName" name="guardianName" type="text" maxlength="200" class="form-input" [(ngModel)]="form.guardianName" />
            </div>
            <div>
              <label class="form-label" for="guardianContact">Guardian Contact</label>
              <input id="guardianContact" name="guardianContact" type="text" maxlength="50" class="form-input" [(ngModel)]="form.guardianContact" />
            </div>
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Contact</h3>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="contactNumber">Contact Number</label>
              <input id="contactNumber" name="contactNumber" type="text" maxlength="50" class="form-input" [(ngModel)]="form.contactNumber" />
            </div>
            <div>
              <label class="form-label" for="email">Email</label>
              <input id="email" name="email" type="email" maxlength="200" class="form-input" [(ngModel)]="form.email" />
            </div>
          </div>
        </section>

        <div class="flex gap-3">
          <button type="submit" [disabled]="saving()" class="btn btn-primary">
            {{ saving() ? 'Saving...' : (isEdit() ? 'Update' : 'Create') }} Student
          </button>
          <button type="button" (click)="onCancel()" class="btn btn-secondary">Cancel</button>
        </div>
      </form>
      <datalist id="staff-extension-names">@for (x of extensionNames; track x) { <option [value]="x"></option> }</datalist>
    </div>
  `
})
export class StudentFormComponent implements OnInit {
  isEdit = signal(false);
  saving = signal(false);
  confirming = signal(false);
  error = signal('');
  studentNumber = signal('');
  lrnNeedsReview = signal(false);
  studentId = '';
  readonly allFields = ALL_FIELDS_VISIBLE;
  readonly extensionNames = EXTENSION_NAME_SUGGESTIONS;

  form: StudentForm = this.blankForm();

  constructor(private api: ApiService, private route: ActivatedRoute, private router: Router, private notify: NotificationService) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.studentId = id;
      this.api.getStudent(id).subscribe(s => {
        this.studentNumber.set(s.studentNumber);
        this.lrnNeedsReview.set(s.lrnNeedsReview);
        this.form = {
          lrn: s.lrn, firstName: s.firstName, middleName: s.middleName, lastName: s.lastName,
          birthDate: s.birthDate.substring(0, 10), gender: s.gender, address: s.address,
          contactNumber: s.contactNumber, email: s.email, guardianName: s.guardianName, guardianContact: s.guardianContact,
          profile: editableProfile(s.profile),
        };
      });
    }
  }

  showLegacyAddressHint(): boolean {
    return this.isEdit() && !!this.form.address?.trim() && !isPsgcMatched(this.form.profile.homeAddress);
  }

  confirmLrn(): void {
    this.confirming.set(true);
    this.api.confirmLrn(this.studentId).subscribe({
      next: s => { this.confirming.set(false); this.lrnNeedsReview.set(s.lrnNeedsReview); this.notify.success('LRN confirmed.'); },
      error: err => { this.confirming.set(false); this.notify.error(formatApiError(err, 'Could not confirm the LRN.')); },
    });
  }

  onSubmit(): void {
    this.saving.set(true);
    this.error.set('');
    const payload: CreateStudentRequest = { ...this.form, lrn: this.form.lrn?.trim() || null };
    const obs = this.isEdit()
      ? this.api.updateStudent(this.studentId, payload)
      : this.api.createStudent(payload);
    obs.subscribe({
      next: () => this.router.navigate(['/students']),
      error: (err) => { this.saving.set(false); this.error.set(formatApiError(err, 'Save failed')); },
    });
  }

  onCancel(): void { this.router.navigate(['/students']); }

  private blankForm(): StudentForm {
    return {
      lrn: null, firstName: '', middleName: '', lastName: '', birthDate: '', gender: null, address: '',
      contactNumber: null, email: null, guardianName: null, guardianContact: null,
      profile: emptyLearnerProfile(),
    };
  }
}
```

- [ ] **Step 2: Students list — Student No., LRN states, filters**

In `student-list.component.ts`:

1. Search placeholder → `"Search by name, student no., or LRN..."`.
2. After the "Show inactive" label add:

```html
          <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="missingLrn" (ngModelChange)="onSearch()" />
            Missing LRN
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="lrnNeedsReview" (ngModelChange)="onSearch()" />
            Check LRN
          </label>
```

3. Header: replace the `LRN` `<th>` with two: `Student No.` and `LRN` (same classes). Change `colspan="7"` on the empty row to `colspan="8"`.
4. Row: replace `<td class="px-6 py-4 text-sm folio-mono text-gray-700">{{ student.lrn }}</td>` with:

```html
                  <td class="px-6 py-4 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ student.studentNumber }}</td>
                  <td class="px-6 py-4 text-sm whitespace-nowrap">
                    @if (!student.lrn) {
                      <span class="text-gray-400">No LRN yet</span>
                    } @else {
                      <span class="folio-mono text-gray-700">{{ student.lrn }}</span>
                      @if (student.lrnNeedsReview) {
                        <span class="ml-2 inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">Check LRN</span>
                      }
                    }
                  </td>
```

5. Class: add `missingLrn = false;` and `lrnNeedsReview = false;`; in `load()` call `this.api.getStudents(this.search, this.page(), 20, this.showInactive, { missingLrn: this.missingLrn, lrnNeedsReview: this.lrnNeedsReview })`.

- [ ] **Step 3: Build and verify**

Run: `npm run build` → succeeds. In the running app (admin login): the students list shows Student No. and the three LRN states; each filter narrows the list; editing a flagged demo student shows the Check LRN notice and Confirm LRN clears it; a new student cannot be created without a barangay (server message shown); editing Maria shows the "Address not matched to PSGC yet" hint and still saves.

- [ ] **Step 4: Commit**

```bash
git add enrollify.client/src/app/pages/students
git commit -m "Learner record: staff student form sections, Confirm LRN, list filters

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 19: Enrollment detail Learner tab, wizard, printouts

**Files:**
- Modify: `enrollify.client/src/app/pages/enrollments/enrollment-detail/enrollment-detail.component.ts`
- Modify: `enrollify.client/src/app/pages/enrollments/enrollment-wizard/enrollment-wizard.component.ts`
- Modify: `enrollify.client/src/app/pages/print/print-enrollment.component.ts`

**Interfaces:**
- Consumes: `Enrollment.studentNumber/lrn/learnerInfo`, `ApiService.updateEnrollmentLearnerInfo/getShsStrands/createEnrollment` (Task 14); `StrandSelectComponent` (Task 16); `LEARNER_TYPES`, `GRADE_LEVELS`, `isShsGrade`, `emptyLearnerInfo`.
- Produces: UI only.

- [ ] **Step 1: Enrollment detail — header and Learner tab**

In `enrollment-detail.component.ts`:

1. Imports: add `StrandSelectComponent` to `imports`; import `ShsStrand, EnrollmentLearnerInfo` from models; `LEARNER_TYPES, GRADE_LEVELS, isShsGrade` from constants; `emptyLearnerInfo` from `../../../core/learner`.
2. Header: replace `<p class="text-sm text-gray-500">{{ enrollment()!.schoolYear }} - {{ enrollment()!.gradeLevel }}</p>` with:

```html
            <p class="text-sm text-gray-500">{{ enrollment()!.schoolYear }} - {{ enrollment()!.gradeLevel }}@if (enrollment()!.learnerInfo?.strand) { · {{ enrollment()!.learnerInfo!.track }} – {{ enrollment()!.learnerInfo!.strand }} }</p>
            <p class="mt-0.5 text-xs text-gray-500">Student No. <span class="folio-mono text-gray-700">{{ enrollment()!.studentNumber || '—' }}</span> · LRN <span class="folio-mono text-gray-700">{{ enrollment()!.lrn || '—' }}</span></p>
```

3. Tabs: add `overflow-x-auto whitespace-nowrap` to the tab `<nav class="flex gap-6">`, give each tab button `shrink-0`, and add as the FIRST tab:

```html
            <button (click)="activeTab.set('learner')"
                    class="pb-3 text-sm font-medium border-b-2 transition-colors inline-flex items-center gap-2 shrink-0"
                    [class]="activeTab() === 'learner' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              Learner
              @if (missingStrand()) {
                <span class="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">Needs strand</span>
              }
            </button>
```

4. Tab content — add before `@if (activeTab() === 'requirements') {`:

```html
        @if (activeTab() === 'learner') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6 space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-semibold text-gray-900">Learner details</h3>
            <a [href]="'/students/' + enrollment()!.studentId + '/edit'" class="text-xs text-[#0038A8] hover:underline">Edit student record</a>
          </div>
          @if (learnerLocked()) {
            <p class="text-sm text-gray-600 bg-gray-50 border border-gray-200 rounded-lg p-3">Locked after assessment. Use “Send back for re-assessment” to change these.</p>
          } @else if (missingStrand()) {
            <p class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-3">Set the learner's track/strand before assessing this enrollment.</p>
          }
          @if (isShs() && (strands().length > 0 || learnerForm.strand)) {
            <app-strand-select [strands]="strands()" [track]="learnerForm.track" [strand]="learnerForm.strand"
                               [disabled]="learnerLocked()" id="learner-strand"
                               (changed)="learnerForm.track = $event.track; learnerForm.strand = $event.strand" />
          }
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="learner-type">Learner Type</label>
              <select id="learner-type" class="form-input" [disabled]="learnerLocked()"
                      [ngModel]="learnerForm.learnerType" (ngModelChange)="learnerForm.learnerType = $event" [ngModelOptions]="{ standalone: true }">
                <option [ngValue]="null">Not set</option>
                @for (t of learnerTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
              </select>
            </div>
            <div>
              <label class="form-label" for="last-grade">Last Grade Level Completed</label>
              <select id="last-grade" class="form-input" [disabled]="learnerLocked()"
                      [ngModel]="learnerForm.lastGradeLevelCompleted" (ngModelChange)="learnerForm.lastGradeLevelCompleted = $event" [ngModelOptions]="{ standalone: true }">
                <option [ngValue]="null">Not applicable</option>
                @for (g of gradeLevels; track g) { <option [ngValue]="g">{{ g }}</option> }
              </select>
            </div>
            <div>
              <label class="form-label" for="last-sy">Last School Year Completed</label>
              <input id="last-sy" type="text" maxlength="9" placeholder="2025-2026" class="form-input folio-mono" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolYearCompleted" (ngModelChange)="learnerForm.lastSchoolYearCompleted = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school-id">Last School's DepEd School ID</label>
              <input id="last-school-id" type="text" inputmode="numeric" maxlength="6" class="form-input folio-mono" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolId" (ngModelChange)="learnerForm.lastSchoolId = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school">Last School Attended</label>
              <input id="last-school" type="text" maxlength="200" class="form-input" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolAttended" (ngModelChange)="learnerForm.lastSchoolAttended = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school-address">Last School Address</label>
              <input id="last-school-address" type="text" maxlength="500" class="form-input" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolAddress" (ngModelChange)="learnerForm.lastSchoolAddress = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
          </div>
          @if (!learnerLocked()) {
            <div class="flex justify-end">
              <button (click)="saveLearnerInfo()" [disabled]="savingLearner()" class="btn btn-primary">{{ savingLearner() ? 'Saving…' : 'Save learner details' }}</button>
            </div>
          }
        </div>
        }
```

5. Class:
   - `activeTab = signal<'learner' | 'requirements' | 'payments' | 'ledger' | 'history'>('payments');`
   - Add members:

```ts
  strands = signal<ShsStrand[]>([]);
  learnerForm: EnrollmentLearnerInfo = emptyLearnerInfo();
  savingLearner = signal(false);
  readonly learnerTypes = LEARNER_TYPES;
  readonly gradeLevels = GRADE_LEVELS;
  isShs = computed(() => isShsGrade(this.enrollment()?.gradeLevel));
  learnerLocked = computed(() => !['Draft', 'Submitted'].includes(this.getStatusName()));
  missingStrand = computed(() => this.isShs() && this.strands().length > 0 && !this.enrollment()?.learnerInfo?.strand);
```

   - In `ngOnInit` add `this.api.getShsStrands().subscribe(s => this.strands.set(s));`.
   - In `loadAll`'s `getEnrollment` callback, right after `this.enrollment.set(e);`: `this.learnerForm = { ...emptyLearnerInfo(), ...(e.learnerInfo ?? {}) };`.
   - Add:

```ts
  saveLearnerInfo(): void {
    this.savingLearner.set(true);
    this.api.updateEnrollmentLearnerInfo(this.enrollmentId, this.learnerForm).subscribe({
      next: () => { this.savingLearner.set(false); this.notify.success('Learner details saved.'); this.loadAll(); },
      error: err => { this.savingLearner.set(false); this.notify.error(formatApiError(err, 'Could not save learner details.')); },
    });
  }
```

- [ ] **Step 2: Wizard — learner type and strand**

In `enrollment-wizard.component.ts`:

1. Imports: `StrandSelectComponent`; `ShsStrand`; `LEARNER_TYPES, isShsGrade`.
2. Step 0 result row: replace `<span class="text-gray-400 text-sm ml-2">LRN: <span class="folio-mono">{{ s.lrn }}</span></span>` with `<span class="text-gray-400 text-sm ml-2"><span class="folio-mono">{{ s.studentNumber }}</span> · LRN <span class="folio-mono">{{ s.lrn || '—' }}</span></span>`; change the search placeholder to `"Type name, student no., or LRN..."`.
3. Step 1: change the grade `<select [(ngModel)]="gradeLevel" ...>` to `<select [ngModel]="gradeLevel" (ngModelChange)="onGradeChange($event)" class="form-input">` and add after it:

```html
            @if (showStrand()) {
              <app-strand-select [strands]="strands()" [track]="track" [strand]="strand" id="wizard-strand"
                                 (changed)="track = $event.track; strand = $event.strand" />
            }
            <div>
              <label class="form-label" for="wizard-learner-type">Learner Type</label>
              <select id="wizard-learner-type" [(ngModel)]="learnerType" class="form-input">
                <option [ngValue]="null">Automatic (New, or Continuing if previously enrolled)</option>
                @for (t of learnerTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
              </select>
            </div>
```

4. Step 2 (confirm): replace the student line's LRN span with `<span class="folio-mono text-sm">{{ selectedStudent()?.studentNumber }}</span>`, and add a block after Grade Level: `@if (strand) { <div class="bg-gray-50 p-4 rounded-lg border border-gray-200"><p class="text-sm text-gray-500">Track / Strand</p><p class="font-medium text-gray-900">{{ track }} – {{ strand }}</p></div> }`.
5. Class:

```ts
  strands = signal<ShsStrand[]>([]);
  learnerType: string | null = null;
  track: string | null = null;
  strand: string | null = null;
  readonly learnerTypes = LEARNER_TYPES;

  showStrand(): boolean { return isShsGrade(this.gradeLevel) && this.strands().length > 0; }

  onGradeChange(grade: string): void {
    this.gradeLevel = grade;
    if (!isShsGrade(grade)) { this.track = null; this.strand = null; }
  }
```

   In the constructor add `this.api.getShsStrands().subscribe(s => this.strands.set(s));`. In `submit()` add `learnerType: this.learnerType, track: this.track, strand: this.strand` to the `createEnrollment` payload.

- [ ] **Step 3: Printouts**

In `print-enrollment.component.ts`:

1. COR table: after the "Student Name" row add:

```html
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">Student No.</td>
                    <td class="py-2.5 font-medium text-gray-900 folio-mono">{{ e.studentNumber || '—' }}</td>
                  </tr>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">LRN</td>
                    <td class="py-2.5 font-medium text-gray-900 folio-mono">{{ e.lrn || '—' }}</td>
                  </tr>
```

   and after the "Grade Level" row:

```html
                  @if (e.learnerInfo?.strand) {
                    <tr class="border-b border-gray-200">
                      <td class="py-2.5 text-gray-500">Track / Strand</td>
                      <td class="py-2.5 font-medium text-gray-900">{{ e.learnerInfo!.track }} – {{ e.learnerInfo!.strand }}</td>
                    </tr>
                  }
```

2. Assessment slip grid: after the Student block add `<div><p class="text-xs text-gray-500">Student No. / LRN</p><p class="font-medium text-gray-900 folio-mono">{{ e.studentNumber || '—' }} / {{ e.lrn || '—' }}</p></div>`.

- [ ] **Step 4: Build and verify**

Run: `npm run build` → succeeds. In the running app: open a Draft Grade 11 enrollment → Learner tab shows "Needs strand"; set a strand and save; move to Submitted → Assessed works; an Assessed enrollment's Learner tab is read-only with the locked note; "Send back for re-assessment" makes it editable again. Create an enrollment through the wizard for Grade 11 with a strand. Print COR / assessment slip show Student No. and LRN (no folder tabs/stamps on paper).

- [ ] **Step 5: Commit**

```bash
git add enrollify.client/src/app/pages/enrollments enrollify.client/src/app/pages/print
git commit -m "Learner record: enrollment Learner tab, wizard strand, Student No. on printouts

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 20: Family profile pages and the re-enrollment strand picker

**Files:**
- Modify: `enrollify.client/src/app/pages/parent/child-profile/child-profile.component.ts`
- Modify: `enrollify.client/src/app/pages/student/my-profile.component.ts`
- Modify: `enrollify.client/src/app/pages/parent/child-enrollment/child-enrollment.component.ts`
- Modify: `enrollify.client/src/app/pages/student/my-enrollment.component.ts`

**Interfaces:**
- Consumes: `PsgcAddressComponent`, `LearnerSummaryComponent`, `StrandSelectComponent`; `ApiService.updateChildProfile/updateMyProfile(ProfileSelfUpdate)`, `requestEnrollmentForChild/requestEnrollment(strand?)`, `getShsStrands`; `emptyPsgcAddress`, `isShsGrade`.
- Produces: UI only.

- [ ] **Step 1: Child profile (parent)**

In `child-profile.component.ts`:

1. `imports`: add `PsgcAddressComponent, LearnerSummaryComponent`; import `PsgcAddress` from models and `emptyPsgcAddress` from `../../../core/learner`.
2. Replace the `LRN` read-only `<div>` (label + disabled input) with a `Student No.` read-only field: `<label class="form-label">Student No.</label><input type="text" [value]="student()!.studentNumber" disabled class="form-input folio-mono bg-gray-50 cursor-not-allowed" />`.
3. Replace the single `Address` input `<div>` with:

```html
                <div class="space-y-4">
                  <p class="form-label">Home Address</p>
                  <app-psgc-address [address]="editHome" idPrefix="child-addr" />
                  <div>
                    <label class="form-label" for="child-street">House no. / Street / Sitio / Purok</label>
                    <input id="child-street" type="text" maxlength="500" [(ngModel)]="editAddress" class="form-input" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label" for="child-father-contact">Father's Contact Number</label>
                    <input id="child-father-contact" type="text" maxlength="50" [(ngModel)]="editFatherContact" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label" for="child-mother-contact">Mother's Contact Number</label>
                    <input id="child-mother-contact" type="text" maxlength="50" [(ngModel)]="editMotherContact" class="form-input" />
                  </div>
                </div>
```

4. After the "Personal Information" card's closing `</div>` (still inside `lg:col-span-2`), add a second card:

```html
          @if (student()) {
            <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
              <h2 class="text-lg font-semibold text-gray-900 mb-4">DepEd Learner Details</h2>
              <app-learner-summary [student]="student()!" />
            </div>
          }
```

5. Class: add `editHome: PsgcAddress = emptyPsgcAddress(); editFatherContact = ''; editMotherContact = '';`. In the `getChildProfile` callback add:

```ts
      this.editHome = { ...emptyPsgcAddress(), ...(s.profile?.homeAddress ?? {}) };
      this.editFatherContact = s.profile?.fatherContactNumber || '';
      this.editMotherContact = s.profile?.motherContactNumber || '';
```

   and in `saveProfile`'s payload add `homeAddress: this.editHome, fatherContactNumber: this.editFatherContact, motherContactNumber: this.editMotherContact,`.

- [ ] **Step 2: My profile (student)**

Apply the identical changes to `my-profile.component.ts` (its template and fields use the same names: `student()`, `editContact`, `editEmail`, `editAddress`, `editGuardianName`, `editGuardianContact`, `saveProfile()`), with these ids: `my-addr` for the picker's `idPrefix`, `my-street`, `my-father-contact`, `my-mother-contact`; the profile load callback is the `getMyProfile` subscription and the save call is `updateMyProfile`.

- [ ] **Step 3: Re-enrollment strand picker (parent)**

In `child-enrollment.component.ts`:

1. `imports`: add `StrandSelectComponent`; import `ShsStrand` and `isShsGrade`.
2. Class:

```ts
  strands = signal<ShsStrand[]>([]);
  priorGrade = signal<string | null>(null);
  pick: { track: string | null; strand: string | null } = { track: null, strand: null };
  // Moving up from Grade 10 lands in Grade 11 — the family may choose a strand now (optional;
  // the registrar can set it later). 11 → 12 carries the strand automatically.
  offerStrand = computed(() => this.priorGrade() === 'Grade 10' && this.strands().length > 0);
```

3. In `ngOnInit` add `this.api.getShsStrands().subscribe(s => this.strands.set(s));`. In `load()`'s `getChildEnrollments` callback, after `this.enrollment.set(match);` add:

```ts
      const prior = enrollments
        .filter(e => e.schoolYear !== activeSY && enrollmentStatusName(e.status) !== 'Cancelled')
        .sort((a, b) => b.schoolYear.localeCompare(a.schoolYear))[0];
      this.priorGrade.set(prior?.gradeLevel ?? null);
```

4. In the "No enrollment found" block, before the Request button add:

```html
          @if (offerStrand()) {
            <div class="mx-auto mb-4 max-w-sm text-left">
              <app-strand-select [strands]="strands()" [track]="pick.track" [strand]="pick.strand" id="child-reenroll-strand"
                                 label="Senior High track / strand (optional)" (changed)="pick = $event" />
              <p class="mt-1 text-xs text-gray-400">You can leave this blank — the registrar can set it later.</p>
            </div>
          }
```

5. `requestEnrollment()`: call `this.api.requestEnrollmentForChild(this.studentId, this.offerStrand() && this.pick.track && this.pick.strand ? { track: this.pick.track, strand: this.pick.strand } : null)`.

- [ ] **Step 4: Re-enrollment strand picker (student)**

In `my-enrollment.component.ts` make the same four changes as Step 3 (same member names, `id="my-reenroll-strand"`, data from the `getMyEnrollments` callback), and in `requestEnrollment()` call `this.api.requestEnrollment(this.offerStrand() && this.pick.track && this.pick.strand ? { track: this.pick.track, strand: this.pick.strand } : null)`.

- [ ] **Step 5: Build and verify**

Run: `npm run build` → succeeds. As the demo parent: child profile shows Student No., the DepEd summary (read-only), and saves a PSGC address + parent contacts. As a parent whose child finished Grade 10 last year: the request card offers the optional strand picker; requesting with a strand creates a Grade 11 enrollment with that strand. At 375px no horizontal scroll on either page.

- [ ] **Step 6: Commit**

```bash
git add enrollify.client/src/app/pages/parent enrollify.client/src/app/pages/student
git commit -m "Learner record: family profile address + DepEd summary, re-enroll strand picker

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 21: Settings — SHS strands, keep on file, scrollable tabs

**Files:**
- Modify: `enrollify.client/src/app/pages/settings/settings.component.ts`

**Interfaces:**
- Consumes: `ApiService.getShsStrands(true)/createShsStrand/updateShsStrand`, requirement-template calls with `keepOnFile`, `bulkReenroll` `needsStrand` (Task 14).
- Produces: UI only.

- [ ] **Step 1: Scrollable tab bar + new tab**

1. Tab `<nav class="flex gap-6">` → `<nav class="flex gap-6 overflow-x-auto whitespace-nowrap">`; add `shrink-0` to the tab button classes.
2. In `tabs`, insert `{ id: 'strands', label: 'SHS Strands' },` right after the `requirements` entry.

- [ ] **Step 2: SHS Strands tab**

Add before `<!-- Workflows Tab -->`:

```html
      <!-- SHS Strands Tab -->
      @if (activeTab === 'strands') {
        <div class="mt-6 space-y-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-1">Add Strand</h2>
            <p class="text-sm text-gray-500 mb-4">The Senior High tracks and strands your school offers. Grade 11–12 applicants and enrollments choose from the active ones.</p>
            <div class="grid grid-cols-1 sm:grid-cols-12 gap-4 items-end">
              <div class="sm:col-span-4">
                <label class="form-label" for="new-track">Track *</label>
                <input id="new-track" type="text" maxlength="100" [(ngModel)]="newStrand.track" class="form-input" placeholder="e.g. Academic" />
              </div>
              <div class="sm:col-span-4">
                <label class="form-label" for="new-strand">Strand *</label>
                <input id="new-strand" type="text" maxlength="100" [(ngModel)]="newStrand.strand" class="form-input" placeholder="e.g. STEM" />
              </div>
              <div class="sm:col-span-2">
                <label class="form-label" for="new-strand-order">Order</label>
                <input id="new-strand-order" type="number" [(ngModel)]="newStrand.displayOrder" class="form-input" />
              </div>
              <div class="sm:col-span-2">
                <button (click)="addStrand()" [disabled]="!newStrand.track.trim() || !newStrand.strand.trim()" class="btn btn-primary w-full disabled:opacity-50">Add</button>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-xl border border-[#E2D9C2] overflow-x-auto">
            <table class="w-full">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-20">Order</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Track</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Strand</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                @for (s of strands(); track s.id) {
                  <tr class="hover:bg-gray-50" [class.opacity-60]="!s.isActive">
                    @if (editingStrandId() === s.id) {
                      <td class="px-6 py-3"><input type="number" [(ngModel)]="strandEdit.displayOrder" class="form-input" /></td>
                      <td class="px-6 py-3"><input type="text" maxlength="100" [(ngModel)]="strandEdit.track" class="form-input" /></td>
                      <td class="px-6 py-3"><input type="text" maxlength="100" [(ngModel)]="strandEdit.strand" class="form-input" /></td>
                      <td class="px-6 py-3 text-sm text-gray-500">{{ s.isActive ? 'Active' : 'Inactive' }}</td>
                      <td class="px-6 py-3 text-right text-sm whitespace-nowrap">
                        <button (click)="saveStrand(s)" class="text-[#0038A8] hover:underline mr-3">Save</button>
                        <button (click)="editingStrandId.set(null)" class="text-gray-500 hover:underline">Cancel</button>
                      </td>
                    } @else {
                      <td class="px-6 py-4 text-sm text-gray-500">{{ s.displayOrder }}</td>
                      <td class="px-6 py-4 text-sm text-gray-900">{{ s.track }}</td>
                      <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ s.strand }}</td>
                      <td class="px-6 py-4 text-sm">
                        @if (s.isActive) { <span class="badge badge-success">Active</span> } @else { <span class="badge bg-gray-100 text-gray-600">Inactive</span> }
                      </td>
                      <td class="px-6 py-4 text-right text-sm whitespace-nowrap">
                        <button (click)="startEditStrand(s)" class="text-[#0038A8] hover:underline mr-3">Edit</button>
                        <button (click)="toggleStrand(s)" class="text-[#0038A8] hover:underline">{{ s.isActive ? 'Deactivate' : 'Activate' }}</button>
                      </td>
                    }
                  </tr>
                }
                @if (strands().length === 0) {
                  <tr><td colspan="5" class="px-6 py-8 text-center text-sm text-gray-500">No strands yet. Add the ones your school offers above.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
```

Class members (import `ShsStrand`):

```ts
  strands = signal<ShsStrand[]>([]);
  newStrand = { track: '', strand: '', displayOrder: 0 };
  editingStrandId = signal<string | null>(null);
  strandEdit = { track: '', strand: '', displayOrder: 0 };

  loadStrands() {
    this.api.getShsStrands(true).subscribe(list => this.strands.set(list));
  }

  addStrand() {
    this.api.createShsStrand({
      track: this.newStrand.track.trim(), strand: this.newStrand.strand.trim(), displayOrder: this.newStrand.displayOrder || 0,
    }).subscribe({
      next: () => { this.newStrand = { track: '', strand: '', displayOrder: 0 }; this.loadStrands(); this.notify.success('Strand added.'); },
      error: err => this.notify.error(formatApiError(err, 'Failed to add the strand.')),
    });
  }

  startEditStrand(s: ShsStrand) {
    this.strandEdit = { track: s.track, strand: s.strand, displayOrder: s.displayOrder };
    this.editingStrandId.set(s.id);
  }

  saveStrand(s: ShsStrand) {
    this.api.updateShsStrand(s.id, { ...this.strandEdit, isActive: s.isActive }).subscribe({
      next: () => { this.editingStrandId.set(null); this.loadStrands(); },
      error: err => this.notify.error(formatApiError(err, 'Failed to save the strand.')),
    });
  }

  toggleStrand(s: ShsStrand) {
    this.api.updateShsStrand(s.id, { track: s.track, strand: s.strand, isActive: !s.isActive, displayOrder: s.displayOrder }).subscribe({
      next: () => this.loadStrands(),
      error: err => this.notify.error(formatApiError(err, 'Failed to update the strand.')),
    });
  }
```

Call `this.loadStrands();` in `ngOnInit` next to `this.loadRequirementTemplates();`.

- [ ] **Step 3: Keep on file**

1. `newRequirement = { documentName: '', gradeLevel: '', displayOrder: 0, keepOnFile: false };` (and reset to the same after add).
2. In the Add Requirement grid, add before the Add button column (and shrink the name column to `sm:col-span-4`):

```html
              <div class="sm:col-span-2">
                <label class="inline-flex items-center gap-2 text-sm text-gray-700 pb-2.5">
                  <input type="checkbox" [(ngModel)]="newRequirement.keepOnFile" />
                  Keep on file
                </label>
              </div>
```

3. `addRequirementTemplate` passes `keepOnFile: this.newRequirement.keepOnFile`; `toggleRequirementTemplate` keeps passing `keepOnFile: t.keepOnFile`; add:

```ts
  toggleKeepOnFile(t: RequirementTemplate) {
    this.api.updateRequirementTemplate(t.id, {
      documentName: t.documentName, gradeLevel: t.gradeLevel, isActive: t.isActive,
      displayOrder: t.displayOrder, keepOnFile: !t.keepOnFile,
    }).subscribe({
      next: () => this.loadRequirementTemplates(),
      error: err => this.notify.error(formatApiError(err, 'Failed to update the requirement.')),
    });
  }
```

4. Table: add a `<th>` "Kept on file" after Grade Level, a cell `<td class="px-6 py-4 text-sm text-gray-500">{{ t.keepOnFile ? 'Once per learner' : 'Every enrollment' }}</td>`, an action `<button (click)="toggleKeepOnFile(t)" class="text-[#0038A8] hover:underline mr-3">{{ t.keepOnFile ? "Don't keep on file" : 'Keep on file' }}</button>`, and change the empty-row `colspan="5"` to `colspan="6"`. Under the table header text add: `<p class="text-sm text-gray-500 mt-1">"Kept on file" documents (like the PSA birth certificate) are submitted once: a verified copy carries forward to the learner's later enrollments.</p>`.

- [ ] **Step 4: Bulk re-enroll message and the Family section**

1. `runBulkReenroll` success: `this.notify.success(\`Created ${res.created} enrollments (${res.skippedExisting} already enrolled, ${res.skippedGraduates} graduates skipped)${res.needsStrand ? \`. ${res.needsStrand} Grade 11 learner(s) need a track/strand before assessment.\` : '.'}\`);`
2. Both section arrays (`order` inside `formFieldsBySection` and `ffSections`) become `['Parent', 'Student', 'Family', 'Enrollment', 'Guardian']`.

- [ ] **Step 5: Build and verify**

Run: `npm run build` → succeeds. In the app as Admin: the tab bar scrolls at 375px; SHS Strands lists the 10 defaults, add/edit/deactivate work and a duplicate shows the server message; Requirements shows PSA as "Once per learner"; Application Form tab shows the new fields, with fatherName/motherMaidenName under Family; a bulk re-enroll that promotes a Grade 10 learner reports the needs-strand count.

- [ ] **Step 6: Commit**

```bash
git add enrollify.client/src/app/pages/settings
git commit -m "Learner record: Settings SHS strands, keep-on-file requirements, scrollable tabs

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

---

### Task 22: End-to-end verification and backlog entry

**Files:**
- Modify: `MVP-BACKLOG.md`

- [ ] **Step 1: Full automated suite**

From `backend/`: `dotnet build Enrollify.sln` → 0 warnings, 0 errors; `dotnet test tests/Enrollify.Application.Tests` → all pass (record the before/after counts from Task 0).
From `enrollify.client/`: `npm run build` → success; `npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/components/psgc-address.component.spec.ts` → pass.

- [ ] **Step 2: Migration on a real database**

Point the API at a copy of a database created BEFORE this branch (or the local dev DB if it predates the migration) and start it (`dotnet run --project src/Enrollify.API` from `backend/`): startup applies `AddLearnerRecord`, then seeds. Verify with `sqlcmd` or SSMS: every student has a unique `StudentNumber` in `YYYY-NNNNN` form; every pre-existing non-null LRN has `LrnNeedsReview = 1`; PSA templates have `KeepOnFile = 1`; `ShsStrands` has 10 rows per tenant. Restart the API once more and confirm nothing changes (no duplicate strands, flags unchanged). If no pre-migration database is available, say so in the report.

- [ ] **Step 3: End-to-end walkthrough (running app)**

Run backend + `npm start`. As an anonymous parent on `/mshs/apply`: one Grade 11 child (LRN, PSGC address, IP + 4Ps + disability, parents' names, strand) and one Grade 3 child → submit. As Admin: Admissions shows the application; approve → Students lists the child with a Student No. and LRN; the enrollment's Learner tab shows the strand and previous school; requirements include PSA. Verify the PSA requirement, mark the enrollment Enrolled through the normal steps, bulk re-enroll into the next year → the new Grade 12 enrollment keeps STEM and its PSA requirement arrives already verified with the "On file" note. Confirm the "Check LRN" flow on a migrated student. Note anything that fails and fix it before continuing.

- [ ] **Step 4: Backlog entry**

Add at the top of `MVP-BACKLOG.md` (above "Batch D"), replacing `<date>` with today's date and `<before>` / `<after>` with the test counts from Task 0 and Step 1:

```markdown
## Sub-project 1a — Philippine learner record (delivered <date>)

Gap-analysis roadmap item 1a (spec: docs/superpowers/specs/2026-10-12-learner-record-design.md):
- Real DepEd LRN (optional, 12 digits, unique when present) + Enrollify student numbers
  (`YYYY-NNNNN`); every pre-migration LRN flagged "Check LRN" with a Confirm action.
- BEEF learner fields on Student/AdmissionApplication (`ILearnerProfile`), PSGC-structured home
  address (shipped PSA dataset + picker), per-enrollment learner type / previous school / SHS
  track+strand; strand required at assessment when the school offers strands.
- Per-school SHS strand catalog (Settings); KeepOnFile requirements carry a verified PSA forward.
- Apply form, staff student form, family profiles, enrollment Learner tab, printouts updated.
  Tests <before> → <after>.

Follow-ups (non-blocking):
- [ ] Sub-project 7 file cleanup must check every requirement that references a FileDocument
  (carried-over documents share files).
- [ ] Demo seed has no Grade 11 enrollment (spec §5 asked for one); add when the demo seed
  gains enrollments.
```

- [ ] **Step 5: Commit**

```bash
git add MVP-BACKLOG.md
git commit -m "Learner record: backlog entry for sub-project 1a

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

