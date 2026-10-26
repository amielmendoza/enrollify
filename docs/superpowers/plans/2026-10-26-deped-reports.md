# DepEd Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give registrars three DepEd-aligned reports (LIS Worklist, Quick Count, SF1 working copy) plus the data they need — a school DepEd profile, a withdrawal mark, an "encoded in LIS" tracker, repeater/first-attendance and guardian-relationship fields — so they can encode enrolment into DepEd's LIS quickly and catch data problems first.

**Architecture:** Follows the existing Clean Architecture + CQRS: new `TenantEntity` columns and a migration with a SQL backfill (mirrored as a C# seeder helper so it runs under the in-memory tests); a shared `DepEdRules` static class holding every rule the three reports share (written and tested once); report queries under `Features/Reports/DepEd` using the established shared-report-class + injection-safe CSV-builder pattern; thin additions to `ReportsController`; and Angular 18 standalone report/print pages, a Settings sub-component, and enrollment-detail additions following existing conventions.

**Tech Stack:** .NET 8, EF Core (SQL Server for migrations, InMemory for tests), MediatR, FluentValidation, xUnit. Angular 18 standalone components with signals, Tailwind, RxJS.

**Spec:** `docs/superpowers/specs/2026-10-15-deped-reports-design.md` (read it alongside this plan; this plan argues from it).

## Global Constraints

- Migration name is exactly `AddDepEdReports`. Verify it on a **throwaway** SQL Server database (fresh seed, smoke-test the API on a separate port, then drop); never run it against `EnrollifyDb`.
- Handlers inject only `IApplicationDbContext`. The acting user's display name flows **controller → command as a `string` field**, read from `User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "Admin"`. Handlers never inject `ICurrentUserService`.
- Tenant isolation is automatic via the global query filter; **never** add manual `where TenantId == …` and never use `.IgnoreQueryFilters()` in these per-tenant reports.
- **Enrolled** = status `Approved`, `Paid`, or `Enrolled`. `Draft`, `Submitted`, `Assessed`, `Cancelled` are not.
- Manila time = fixed `TimeSpan.FromHours(8)` offset (PH has no DST). Pure helpers take `DateTime utcNow` as a parameter; mirror `StudentNumbering`. Never call `DateTime.UtcNow` inside pure rule helpers.
- Dates on printouts and CSVs: `MM/dd/yyyy` with `CultureInfo.InvariantCulture`.
- CSV cells: free text through `CollectionsCsvBuilder.EscapeText` (formula-injection guard), money/number through `CollectionsCsvBuilder.Escape` only (negatives must not be apostrophe-prefixed).
- Report DTOs are declared **inline in the query file** under `namespace Enrollify.Application.Features.Reports.DepEd`, not under `DTOs/`.
- All new endpoints are `[Authorize(Roles = "Admin,Registrar")]`, except `PUT api/school-profile` which is `[Authorize(Roles = "Admin")]`.
- `Enrollment.SchoolYear` and `Enrollment.GradeLevel` are **strings** ("2026-2027", "Grade 7"), not FKs. Match a `SchoolYear` row by `Name`. Canonical grade ordering comes from `GradeLevels.All` (`Enrollify.Application.Common`).
- Frontend: standalone components, signals, `formatApiError` on every error path, `<app-loading>` on every list, `NotificationService` (`notify`) for all toasts/confirm/prompt. No native `alert/confirm/prompt`.
- Frontend builds clean (`npm run build`); Karma suite stays green.

## Review Focus

These are inputs the spec implies but that a naive implementation could get wrong; each is pinned to a test in the owning task.

- **Manila day boundary** — an enrollment approved at 23:30 Manila counts that day; at 00:30 the next day it does not. (Task 2)
- **Enrollment with no status history at all** — its current `Status` must apply from `CreatedAt` onward, not be treated as "no status ever". (Task 2)
- **Counted vs. withdrawn/cancelled on a day** — a learner withdrawn on day X counts on X−1 but not X; one cancelled after approval counts on days before the cancellation. (Task 2, Task 9)
- **`SetLisEncodedCommand` all-or-nothing** — if any id in the batch is missing or not listed, the whole request is rejected and nothing changes; already-marked rows keep their original stamp. (Task 6)
- **Withdrawal date bounds** — a date earlier than effective first attendance, or later than today (Manila), is rejected; `Dropped` requires a reason, `TransferredOut` requires a destination. (Task 5)

---

## File Structure

**Backend — new files**
- `backend/src/Enrollify.Application/Features/Reports/DepEd/DepEdRules.cs` — shared rule helpers.
- `backend/src/Enrollify.Application/Features/SchoolProfile/Queries/GetSchoolProfileQuery.cs`
- `backend/src/Enrollify.Application/Features/SchoolProfile/Commands/UpdateSchoolProfileCommand.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/Commands/UpdateEnrollmentLisDetailsCommand.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/Commands/MarkEnrollmentWithdrawnCommand.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/Commands/UndoEnrollmentWithdrawalCommand.cs`
- `backend/src/Enrollify.Application/Features/Enrollments/Commands/SetLisEncodedCommand.cs`
- `backend/src/Enrollify.Application/Features/Reports/DepEd/GetLisWorklistQuery.cs`
- `backend/src/Enrollify.Application/Features/Reports/DepEd/GetQuickCountQuery.cs` (+ export query in same file)
- `backend/src/Enrollify.Application/Features/Reports/DepEd/QuickCountCsvBuilder.cs`
- `backend/src/Enrollify.Application/Features/Reports/DepEd/GetSf1Query.cs` (+ export query in same file)
- `backend/src/Enrollify.Application/Features/Reports/DepEd/Sf1CsvBuilder.cs`
- `backend/src/Enrollify.Application/Features/SchoolProfile/DefaultDepEdProfile.cs` — per-tenant seed helper.
- `backend/src/Enrollify.Application/Features/Students/GuardianRelationshipBackfill.cs` — C# mirror of the migration backfill, for InMemory seeding/tests.
- `backend/src/Enrollify.API/Controllers/SchoolProfileController.cs`
- `backend/src/Enrollify.Infrastructure/Persistence/Migrations/<timestamp>_AddDepEdReports.cs` (via `dotnet ef`).
- Tests: `backend/tests/Enrollify.Application.Tests/DepEdRulesTests.cs`, `QuickCountReportTests.cs`, `LisWorklistReportTests.cs`, `Sf1ReportTests.cs`, `WithdrawalTests.cs`, `LisEncodedTests.cs`, `LisDetailsTests.cs`, `SchoolProfileTests.cs`, `GuardianRelationshipApprovalTests.cs`, and an addition to the existing bulk-reenroll test.

**Backend — modified files**
- `Enrollify.Domain/Entities/Tenant.cs` (DepEd profile fields), `Student.cs` (`GuardianRelationship`), `Enrollment.cs` (LIS/withdrawal/encoded fields).
- `Enrollify.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`, `StudentConfiguration.cs`, `EnrollmentConfiguration.cs` (max-lengths).
- `Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs` (add `SeedDepEdProfileAsync`).
- `Enrollify.Application/Features/Admissions/Commands/ReviewApplicationCommand.cs` (copy guardian relationship).
- `Enrollify.Application/Features/Enrollments/Commands/BulkReenrollCommand.cs` + its result DTO (`SkippedWithdrawn`).
- `Enrollify.Application/DTOs/Enrollments/EnrollmentDto.cs` (new fields + request records).
- `Enrollify.API/Controllers/EnrollmentsController.cs` (new actions), `ReportsController.cs` (three reports + exports).

**Frontend — new files**
- `enrollify.client/src/app/core/utils/manila-today.ts`
- `enrollify.client/src/app/pages/settings/school-profile-settings.component.ts`
- `enrollify.client/src/app/pages/reports/deped/lis-worklist.component.ts`
- `enrollify.client/src/app/pages/reports/deped/quick-count.component.ts`
- `enrollify.client/src/app/pages/reports/deped/sf1-register.component.ts`
- `enrollify.client/src/app/pages/print/print-sf1.component.ts`
- `enrollify.client/src/app/pages/print/print-quick-count.component.ts`
- `enrollify.client/src/app/core/utils/manila-today.spec.ts`

**Frontend — modified files**
- `core/models/index.ts` (new types), `core/services/api.service.ts` (new methods).
- `pages/settings/settings.component.ts` (new tab), `app.routes.ts` (new routes), `layout/main-layout/main-layout.component.ts` (Reports/DepEd nav).
- `pages/enrollments/enrollment-detail/enrollment-detail.component.ts` (DepEd/LIS card, withdrawal panel, LIS status).
- Guardian-relationship field: `pages/students/student-form/student-form.component.ts`, `pages/student/my-profile.component.ts`, `pages/parent/child-profile/child-profile.component.ts` (and the `ProfileSelfUpdate`/`StudentForm` types).

---

## Phase A — Data model

### Task 1: DepEd data model, migration, and seed

**Files:**
- Modify: `backend/src/Enrollify.Domain/Entities/Tenant.cs`, `Student.cs`, `Enrollment.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/Configurations/{TenantConfiguration,StudentConfiguration,EnrollmentConfiguration}.cs`
- Create: `backend/src/Enrollify.Application/Features/SchoolProfile/DefaultDepEdProfile.cs`
- Create: `backend/src/Enrollify.Application/Features/Students/GuardianRelationshipBackfill.cs`
- Modify: `backend/src/Enrollify.Infrastructure/Persistence/ApplicationDbContextSeed.cs`
- Create (via `dotnet ef`): `…/Migrations/<timestamp>_AddDepEdReports.cs`
- Test: `backend/tests/Enrollify.Application.Tests/GuardianRelationshipApprovalTests.cs` (backfill helper portion)

**Interfaces (Produces):**
- `Tenant`: `DepEdSchoolId`, `DepEdRegionCode`, `DepEdRegionName`, `DepEdDivision`, `DepEdDistrict`, `SchoolHeadName` — all `string?`.
- `Student.GuardianRelationship` — `string?` (max 50).
- `Enrollment`: `IsRepeater` (`bool`, default false), `FirstAttendanceDate` (`DateTime?`), `WithdrawalType` (`string?`), `WithdrawalDate` (`DateTime?`), `WithdrawalReason` (`string?`), `TransferredToSchool` (`string?`), `LisEncodedAt` (`DateTime?`), `LisEncodedBy` (`string?`).
- `DefaultDepEdProfile.EnsureForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct) -> Task<int>` — sets the demo placeholder profile when empty; returns rows touched.
- `GuardianRelationshipBackfill.RunForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct) -> Task<int>` — for each Student with null `GuardianRelationship`, copy it from the most recently updated `Approved` application (`AdmissionApplications.StudentId`) with a non-blank value.

- [ ] **Step 1: Add the fields** to `Tenant`, `Student`, `Enrollment` with the exact names/types/defaults above. Follow the existing property style (`string?`, `bool` with `= false` default via column default in config).

- [ ] **Step 2: Configure max-lengths** in the three configurations: `DepEdSchoolId` ≤ 6, `DepEdRegionCode` ≤ 10, `DepEdRegionName`/`DepEdDivision`/`DepEdDistrict`/`SchoolHeadName` ≤ 150; `GuardianRelationship` ≤ 50; `WithdrawalType` ≤ 20, `WithdrawalReason` ≤ 500, `TransferredToSchool` ≤ 200, `LisEncodedBy` ≤ 200. `IsRepeater` gets `.HasDefaultValue(false)`.

- [ ] **Step 3: Write the backfill helper test** in `GuardianRelationshipApprovalTests.cs`:

```csharp
[Fact]
public async Task Backfill_copies_relationship_from_latest_approved_application()
{
    var ctx = TestDb.Create();
    var student = /* Student with GuardianRelationship = null */;
    ctx.Students.Add(student);
    ctx.AdmissionApplications.Add(/* Approved app, StudentId = student.Id, GuardianRelationship = "Mother", UpdatedAt newest */);
    ctx.AdmissionApplications.Add(/* older Approved app, "Father" */);
    await ctx.SaveChangesAsync();

    await GuardianRelationshipBackfill.RunForTenantAsync(ctx, TestDb.TenantId, default);

    Assert.Equal("Mother", (await ctx.Students.SingleAsync()).GuardianRelationship);
}
```

- [ ] **Step 4: Run it — verify it fails** (`RunForTenantAsync` not defined). `dotnet test --filter GuardianRelationshipApprovalTests`.

- [ ] **Step 5: Implement `GuardianRelationshipBackfill.RunForTenantAsync` and `DefaultDepEdProfile.EnsureForTenantAsync`** following the `LearnerRecordBackfill` / `DefaultShsStrands.EnsureForTenantAsync` idempotent per-tenant shape. Demo profile values: School ID `000000`, region `1300000000`/`"National Capital Region (NCR)"`, division `"Demo Division"`, district `"Demo District"`, school head `"Demo School Head"`.

- [ ] **Step 6: Run the test — verify it passes.**

- [ ] **Step 7: Wire the seeder** — add `SeedDepEdProfileAsync` to `ApplicationDbContextSeed` (loop `Tenants.IgnoreQueryFilters()`, call `DefaultDepEdProfile.EnsureForTenantAsync`, SaveChanges if any), and call the guardian backfill the same way (mirrors the migration SQL so InMemory gets it too).

- [ ] **Step 8: Create the migration.** From `backend/`:

```bash
dotnet ef migrations add AddDepEdReports --project src/Enrollify.Infrastructure --startup-project src/Enrollify.API
```

Then edit the generated `Up()` to add the guardian-relationship SQL backfill **after** the `AddColumn` calls (T-SQL, per `AddLearnerRecord.cs`):

```csharp
migrationBuilder.Sql(@"
UPDATE s SET GuardianRelationship = a.GuardianRelationship
FROM Students s
CROSS APPLY (
    SELECT TOP 1 GuardianRelationship FROM AdmissionApplications a
    WHERE a.StudentId = s.Id AND a.Status = 'Approved'
      AND a.GuardianRelationship IS NOT NULL AND LTRIM(RTRIM(a.GuardianRelationship)) <> ''
    ORDER BY a.UpdatedAt DESC, a.CreatedAt DESC
) a
WHERE s.GuardianRelationship IS NULL;");
```

- [ ] **Step 9: Verify the migration on a throwaway DB.** Point a scratch connection string at a temp database, `dotnet ef database update`, run the API on a spare port briefly, confirm startup + seed succeed, then drop the database. Do **not** touch `EnrollifyDb`.

- [ ] **Step 10: Build + commit.**

```bash
dotnet build Enrollify.sln
git add -A && git commit -m "DepEd reports: data model, AddDepEdReports migration, profile seed"
```

---

## Phase B — Shared rules

### Task 2: DepEdRules

**Files:**
- Create: `backend/src/Enrollify.Application/Features/Reports/DepEd/DepEdRules.cs`
- Test: `backend/tests/Enrollify.Application.Tests/DepEdRulesTests.cs`

**Interfaces (Produces):** a `public static class DepEdRules` with at least —
- `DateTime ManilaDate(DateTime utcNow)` and `DateTime ManilaEndOfDayUtc(DateTime manilaDate)`.
- `EnrollmentStatus? StatusOn(Enrollment e, IReadOnlyList<EnrollmentStatusHistory> history, DateTime manilaDay)` — last history row with `TransitionDate` before end of `manilaDay`; if no rows at all, current `Status` from `CreatedAt`; null if it had no status that day.
- `bool CountedOn(Enrollment e, IReadOnlyList<EnrollmentStatusHistory> history, DateTime manilaDay)` — status on day ∈ {Approved,Paid,Enrolled} **and** not withdrawn on that day (`WithdrawalDate` null or later than `manilaDay`).
- `bool Listed(Enrollment e)` — current `Status` ∈ {Approved,Paid,Enrolled}.
- `string Sex(string? gender)` → `"Male"`/`"Female"`/`"NotSet"`.
- `int StartYear(string schoolYearName, SchoolYear? row)`.
- `int? AgeOn(DateTime? birthDate, int startYear)` — whole years to the first Friday of June of `startYear`; birthday counts on its own day; null birthdate → null.
- `string LearnerName(string last, string first, string? ext, string? middle)` and parent/mother variants.
- `string? Contact(string? guardian, string? mother, string? father)`.
- `DateTime EffectiveFirstAttendance(Enrollment e, SchoolYear? row, string schoolYearName)`.
- `string Remarks(Enrollment e, Student s, SchoolYear? row, string schoolYearName)` — codes in spec order joined with `"; "`.

- [ ] **Step 1: Write the failing tests** in `DepEdRulesTests.cs` covering every Review-Focus and spec §5 case:

```csharp
// Day boundary
[Fact] public void Approved_2330_Manila_counts_that_day() { … }       // utc 15:30 same date
[Fact] public void Approved_0030_next_Manila_day_does_not_count() { … }
// No history
[Fact] public void No_history_uses_current_status_from_CreatedAt() { … }
// Cancelled after approval
[Fact] public void Approved_then_cancelled_counts_before_not_after() { … }
// Withdrawn on X
[Fact] public void Withdrawn_on_X_counts_on_X_minus_1_not_X() { … }
// Sex
[Theory][InlineData("male","Male")][InlineData(" M ","Male")][InlineData("Female","Female")]
[InlineData("f","Female")][InlineData("Other","NotSet")][InlineData(null,"NotSet")]
public void Sex_maps(string? g, string expected) => Assert.Equal(expected, DepEdRules.Sex(g));
// Age: birthday on the first Friday itself, the day after, Feb 29, no birthdate (blank)
// Start year without a SchoolYear row -> first 4 chars of name
// Remarks: each code alone, all together in order, LE not raised when first attendance == start date
// Name with and without extension/middle; contact fallback order
```

- [ ] **Step 2: Run — verify they fail** (class not defined). `dotnet test --filter DepEdRulesTests`.

- [ ] **Step 3: Implement `DepEdRules`** — pure static methods, `TimeSpan.FromHours(8)` offset, `CultureInfo.InvariantCulture` for the `MM/dd/yyyy` dates inside remark codes. First Friday of June: iterate `new DateTime(startYear,6,1)` forward to `DayOfWeek.Friday`.

- [ ] **Step 4: Run — verify all pass.**

- [ ] **Step 5: Commit.** `git commit -m "DepEd reports: shared DepEdRules with unit tests"`

---

## Phase C — Mutations & profile

### Task 3: School profile query + command + controller

**Files:**
- Create: `…/Features/SchoolProfile/Queries/GetSchoolProfileQuery.cs`, `…/Commands/UpdateSchoolProfileCommand.cs`
- Create: `backend/src/Enrollify.API/Controllers/SchoolProfileController.cs`
- Test: `backend/tests/Enrollify.Application.Tests/SchoolProfileTests.cs`

**Interfaces:**
- Consumes: `Tenant` DepEd fields (Task 1).
- Produces: `SchoolProfileDto(string SchoolName, string? DepEdSchoolId, string? DepEdRegionCode, string? DepEdRegionName, string? DepEdDivision, string? DepEdDistrict, string? SchoolHeadName)`; `GetSchoolProfileQuery() : IRequest<SchoolProfileDto>`; `UpdateSchoolProfileCommand(string? DepEdSchoolId, string? DepEdRegionCode, string? DepEdRegionName, string? DepEdDivision, string? DepEdDistrict, string? SchoolHeadName, string UpdatedBy) : IRequest<SchoolProfileDto>`.

- [ ] **Step 1: Write failing tests** — School ID null or `^\d{6}$` (else reject); region code null or `^\d{2}0{8}$`, region name required when code set; division/district/head ≤ 150; blank strings stored as null; the query returns `Tenant.Name` as `SchoolName`. The Tenant is read via `_context.Tenants` (the global filter scopes to the current tenant — load the single row with `.FirstAsync()`).

- [ ] **Step 2: Run — verify fail.**

- [ ] **Step 3: Implement query + command + `UpdateSchoolProfileCommandValidator`** (inline). Map blank → null before validation length checks apply.

- [ ] **Step 4: Run — verify pass.**

- [ ] **Step 5: Add `SchoolProfileController`** — `GET api/school-profile` `[Authorize(Roles="Admin,Registrar")]`, `PUT api/school-profile` `[Authorize(Roles="Admin")]`, `updatedBy` from the Name/Email claim.

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: school DepEd profile endpoints"`

### Task 4: Enrollment LIS details

**Files:** Create `…/Features/Enrollments/Commands/UpdateEnrollmentLisDetailsCommand.cs`; Modify `DTOs/Enrollments/EnrollmentDto.cs` and `EnrollmentsController.cs`; Test `LisDetailsTests.cs`.

**Interfaces:**
- Produces: `UpdateEnrollmentLisDetailsCommand(Guid EnrollmentId, bool IsRepeater, DateTime? FirstAttendanceDate) : IRequest<EnrollmentDto>`; `EnrollmentDto` gains `IsRepeater`, `FirstAttendanceDate`, `WithdrawalType`, `WithdrawalDate`, `WithdrawalReason`, `TransferredToSchool`, `LisEncodedAt`, `LisEncodedBy` (appended as defaulted params so existing constructions still compile).

- [ ] **Step 1: Write failing tests** — editable in any status except `Cancelled` (Cancelled → `InvalidOperationException`); `FirstAttendanceDate`, when set, must be ≥ `SchoolYear.StartDate`, ≤ `EndDate`, and ≤ `WithdrawalDate` when withdrawn (match the `SchoolYear` row by `Name`).

- [ ] **Step 2: Run — verify fail.**

- [ ] **Step 3: Implement** the command/handler (load enrollment, guard status, validate date against the matched `SchoolYear`, set fields, save, return the mapped `EnrollmentDto`). Add the eight fields to `EnrollmentDto` and populate them in **every** place the DTO is built (reuse the existing mapper if there is one; otherwise `GetEnrollmentByIdQuery`'s mapping is the source of truth).

- [ ] **Step 4: Run — verify pass.**

- [ ] **Step 5: Add `PUT api/enrollments/{id}/lis-details`** `[Authorize(Roles="Admin,Registrar")]` with request record `UpdateLisDetailsRequest(bool IsRepeater, DateTime? FirstAttendanceDate)`.

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: enrollment LIS details (repeater, first attendance)"`

### Task 5: Withdrawal mark + bulk-reenroll skip

**Files:** Create `MarkEnrollmentWithdrawnCommand.cs`, `UndoEnrollmentWithdrawalCommand.cs`; Modify `BulkReenrollCommand.cs` (+ result DTO) and `EnrollmentsController.cs`; Test `WithdrawalTests.cs` + an addition to the existing bulk-reenroll test.

**Interfaces:**
- Produces: `MarkEnrollmentWithdrawnCommand(Guid EnrollmentId, string Type, DateTime Date, string? Reason, string? TransferredToSchool, string MarkedBy) : IRequest<EnrollmentDto>`; `UndoEnrollmentWithdrawalCommand(Guid EnrollmentId, string UndoneBy) : IRequest<EnrollmentDto>`; `BulkReenrollResultDto` gains `int SkippedWithdrawn`.

- [ ] **Step 1: Write failing tests:**
  - Status must be Approved/Paid/Enrolled, else rejected (each other status).
  - `Date` ≥ effective first attendance (`DepEdRules.EffectiveFirstAttendance`) and ≤ today (Manila) — earlier and future both rejected.
  - `Dropped` requires a reason (≤500), no destination; `TransferredOut` requires a destination (≤200), reason optional.
  - Already withdrawn → rejected.
  - A history row is written (`FromStatus == ToStatus == current`, remark `Marked dropped as of MM/dd/yyyy: <reason>` / `Marked transferred out as of MM/dd/yyyy to <school>`, `CreatedBy == MarkedBy`); status and balance unchanged.
  - Undo: must be withdrawn; clears all four fields; writes a history row `Withdrawal mark removed (was dropped|transferred out as of MM/dd/yyyy)`.
  - Bulk re-enroll skips a source enrollment carrying a withdrawal mark and counts it in `SkippedWithdrawn`.

- [ ] **Step 2: Run — verify fail.**

- [ ] **Step 3: Implement** both commands + inline validators (static-shape checks; the date-vs-first-attendance and today checks live in the handler, which needs the `SchoolYear` row and `DepEdRules`). Add the `SkippedWithdrawn` skip to `BulkReenrollCommand` (skip when `WithdrawalType != null`).

- [ ] **Step 4: Run — verify pass.**

- [ ] **Step 5: Add `POST api/enrollments/{id}/withdrawal` and `DELETE api/enrollments/{id}/withdrawal`** `[Authorize(Roles="Admin,Registrar")]`; request record `MarkWithdrawnRequest(string Type, DateTime Date, string? Reason, string? TransferredToSchool)`; `markedBy`/`undoneBy` from claims.

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: withdrawal mark + bulk re-enroll skip"`

### Task 6: LIS encoded mark

**Files:** Create `SetLisEncodedCommand.cs`; Modify `EnrollmentsController.cs`; Test `LisEncodedTests.cs`.

**Interfaces:**
- Produces: `SetLisEncodedCommand(IReadOnlyList<Guid> EnrollmentIds, bool Encoded, string EncodedBy) : IRequest<int>` (returns the number changed).

- [ ] **Step 1: Write failing tests** — 1–500 distinct ids; every id must exist in-tenant and be `Listed`, else the whole request is rejected and nothing changes; `Encoded=true` stamps `LisEncodedAt = UtcNow` + `LisEncodedBy` only on rows not yet marked (already-marked keep original stamp); `Encoded=false` clears both; returns the count changed.

- [ ] **Step 2: Run — verify fail.**

- [ ] **Step 3: Implement** command + inline validator (count 1–500, distinct). The existence/listed check and all mutation happen in one `SaveChangesAsync` (load all ids, reject if any missing or not listed before mutating).

- [ ] **Step 4: Run — verify pass.**

- [ ] **Step 5: Add `POST api/enrollments/lis-encoded`** `[Authorize(Roles="Admin,Registrar")]`; request record `SetLisEncodedRequest(IReadOnlyList<Guid> EnrollmentIds, bool Encoded)`.

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: LIS-encoded tracker"`

### Task 7: Guardian relationship on approval

**Files:** Modify `ReviewApplicationCommand.cs`; Test in `GuardianRelationshipApprovalTests.cs`.

**Interfaces:** Consumes `Student.GuardianRelationship` (Task 1), `AdmissionApplication.GuardianRelationship`.

- [ ] **Step 1: Write failing tests** — approving copies `app.GuardianRelationship` into a **new** Student; into a **reused** Student only when the student's own value is empty (non-empty preserved).

- [ ] **Step 2: Run — verify fail.**

- [ ] **Step 3: Implement** — in the new-student construction set `GuardianRelationship = app.GuardianRelationship`; in the reuse branch set it only when the existing value is null/blank.

- [ ] **Step 4: Run — verify pass + commit.** `git commit -m "DepEd reports: approval copies guardian relationship"`

---

## Phase D — Report queries

### Task 8: LIS worklist query

**Files:** Create `GetLisWorklistQuery.cs`; Modify `ReportsController.cs`; Test `LisWorklistReportTests.cs`.

**Interfaces (Produces, inline DTOs):**
- `LisWorklistDto(SchoolHeaderDto Header, IReadOnlyList<WorklistSectionDto> Sections, WorklistSectionDetailDto? Selected)`.
- `SchoolHeaderDto(string? SchoolName, string? SchoolId, string? RegionName, string? Division, string? District, string SchoolYear)` — **shared by all three reports**; declare it in `GetLisWorklistQuery.cs` and reuse.
- `WorklistSectionDto(Guid? SectionId, string GradeLevel, string SectionName, string? Adviser, int Listed, int Encoded, int Pending)` (a `null SectionId` row is the no-section group).
- `WorklistSectionDetailDto(…header…, IReadOnlyList<WorklistLearnerDto> Continuing, IReadOnlyList<WorklistLearnerDto> SearchOrRegister)`.
- `WorklistLearnerDto(Guid EnrollmentId, Guid StudentId, string? Lrn, bool LrnNeedsReview, string Name, string Sex, DateTime? BirthDate, string? LearnerType, DateTime FirstAttendance, IReadOnlyList<string> Tags, string? WithdrawalType, DateTime? WithdrawalDate, DateTime? LisEncodedAt, string? LisEncodedBy, IReadOnlyList<string> FixReasons)`.
- `GetLisWorklistQuery(string? SchoolYear, Guid? SectionId, bool NoSection) : IRequest<LisWorklistDto>`.

- [ ] **Step 1: Write failing tests** — section counts (including the no-section group); `schoolYear` defaults to active (reuse `EnrollmentRegisterReport.ResolveSchoolYearAsync` pattern / a shared resolver); group split (Continuing vs New/Transferee/Returning/none); each fix reason (`Sex not set`, `No birth date`, `LRN needs checking`, `No LRN` when learner type ≠ New, `Learner type not set`); a `New` learner with no LRN raises **no** reason; withdrawn learners still listed; cancelled ones not listed; both groups sorted by last/first/middle.

- [ ] **Step 2–4: Run fail → implement → run pass.** Load the school year's enrollments (+ Student, Section, StatusHistory) into memory and apply `DepEdRules`. Encoded/Pending counts over `Listed` rows: encoded = `LisEncodedAt != null`.

- [ ] **Step 5: Add `GET api/reports/deped/lis-worklist`** `[FromQuery] schoolYear, sectionId, noSection`.

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: LIS worklist"`

### Task 9: Quick count query + export + CSV

**Files:** Create `GetQuickCountQuery.cs` (+ `GetQuickCountExportQuery` in the same file), `QuickCountCsvBuilder.cs`; Modify `ReportsController.cs`; Test `QuickCountReportTests.cs`.

**Interfaces (Produces):**
- `QuickCountDto(SchoolHeaderDto Header, DateOnly AsOf, IReadOnlyList<QuickCountRowDto> Rows, QuickCountRowDto Total, IReadOnlyList<NotSetLearnerDto> NotSetLearners)`.
- `QuickCountRowDto(string GradeLevel, int Male, int Female, int NotSet, int Total, int MaleChange, int FemaleChange, int NotSetChange, int TotalChange)`.
- `NotSetLearnerDto(Guid EnrollmentId, Guid StudentId, string Name, string GradeLevel)`.
- `GetQuickCountQuery(string? SchoolYear, DateOnly? AsOf) : IRequest<QuickCountDto>`; `GetQuickCountExportQuery(string? SchoolYear, DateOnly? AsOf) : IRequest<QuickCountDto>` (same shape, reuses the builder internally).
- `QuickCountCsvBuilder`: `public const string Header = "Grade Level,Male,Female,Sex Not Set,Total,Male Change,Female Change,Sex Not Set Change,Total Change";` `public static string Build(QuickCountDto)` — one row per grade then a `Total` row, numbers via `CollectionsCsvBuilder.Escape`, grade via `EscapeText`.

- [ ] **Step 1: Write failing tests** — `asOf` defaults to today (Manila); one row per grade with anyone counted on `asOf` **or** the day before, `GradeLevels.All` order then unknown grades alphabetically, then a Total row; columns are `CountedOn(asOf)` with the change vs `asOf − 1`; `NotSetLearners` lists every counted learner with sex not set; CSV header + total row match.

- [ ] **Step 2–4: Run fail → implement → run pass.** Compute `CountedOn` for `asOf` and `asOf-1` from in-memory rows via `DepEdRules`.

- [ ] **Step 5: Add `GET api/reports/deped/quick-count` and `…/quick-count/export`** (CSV via `return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"quick-count_{asOf:yyyyMMdd}.csv")`).

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: quick count + CSV"`

### Task 10: SF1 query + export + CSV

**Files:** Create `GetSf1Query.cs` (+ export query), `Sf1CsvBuilder.cs`; Modify `ReportsController.cs`; Test `Sf1ReportTests.cs`.

**Interfaces (Produces):**
- `Sf1Dto(Sf1HeaderDto Header, IReadOnlyList<Sf1RowDto> Learners, int Male, int Female, int NotSet, int Total)`.
- `Sf1HeaderDto(SchoolHeaderDto School, string GradeLevel, string SectionName, string? Adviser, string? SchoolHeadName)`.
- `Sf1RowDto(string? Lrn, string Name, string Sex /* "M"/"F"/"" */, DateTime? BirthDate, int? Age, string? MotherTongue, string? IpGroup, string? Religion, string? HouseStreet, string? Barangay, string? CityMunicipality, string? Province, string? FatherName, string? MotherMaidenName, string? GuardianName, string? GuardianRelationship, string? Contact, string? Remarks)`.
- `GetSf1Query(string? SchoolYear, Guid SectionId) : IRequest<Sf1Dto>`; `GetSf1ExportQuery(string? SchoolYear, Guid SectionId) : IRequest<Sf1Dto>`.
- `Sf1CsvBuilder.Header` + `Build(Sf1Dto)` matching the on-screen columns, `EscapeText` for all text, `Escape` for age.

- [ ] **Step 1: Write failing tests** — the section's `Listed` enrollments; order Male A–Z, then Female A–Z, then not-set A–Z (by last/first/middle); columns pulled from `HomeAddress` (barangay/city/province **names**); IP group filled only when `IsIndigenousPeople`; totals male/female/not-set/total; CSV columns == screen; `sectionId` required.

- [ ] **Step 2–4: Run fail → implement → run pass.** Use `DepEdRules.AgeOn`, `.LearnerName`, `.Contact`, `.Remarks`, `.Sex` (map to `M`/`F`/blank).

- [ ] **Step 5: Add `GET api/reports/deped/sf1` and `…/sf1/export`** (`sectionId` required; CSV filename `sf1_{section}.csv`).

- [ ] **Step 6: Build + commit.** `git commit -m "DepEd reports: SF1 working copy + CSV"`

---

## Phase E — Frontend

### Task 11: Models, ApiService, manilaToday

**Files:** Create `core/utils/manila-today.ts` + `.spec.ts`; Modify `core/models/index.ts`, `core/services/api.service.ts`.

**Interfaces (Produces):** TS interfaces mirroring every DTO above (`SchoolProfile`, `LisWorklist`, `WorklistSection`, `WorklistLearner`, `QuickCount`, `QuickCountRow`, `NotSetLearner`, `Sf1`, `Sf1Row`, `SchoolHeader`), and `ApiService` methods: `getSchoolProfile()`, `updateSchoolProfile(body)`, `updateEnrollmentLisDetails(id, body)`, `markEnrollmentWithdrawn(id, body)`, `undoEnrollmentWithdrawal(id)`, `setLisEncoded(body)`, `getLisWorklist(schoolYear?, sectionId?, noSection?)`, `getQuickCount(schoolYear?, asOf?)`, `exportQuickCount(schoolYear?, asOf?): Observable<Blob>`, `getSf1(sectionId, schoolYear?)`, `exportSf1(sectionId, schoolYear?): Observable<Blob>`. `manilaToday(): string` → Manila `yyyy-MM-dd`.

- [ ] **Step 1: Write `manila-today.spec.ts`** — a fixed UTC instant near the Manila midnight boundary maps to the expected Manila date (inject the instant, don't rely on the wall clock).
- [ ] **Step 2: Run — verify fail** (`npm test` narrowed, or `fdescribe`).
- [ ] **Step 3: Implement `manilaToday(now = new Date())`** using the `+8h` offset and `toISOString().slice(0,10)` on the shifted date.
- [ ] **Step 4: Run — verify pass.**
- [ ] **Step 5: Add the model interfaces + ApiService methods** (blob exports use `{ responseType: 'blob' }`).
- [ ] **Step 6: `npm run build` clean + commit.** `git commit -m "DepEd reports: frontend models, API methods, manilaToday"`

### Task 12: Settings → School Profile tab

**Files:** Create `pages/settings/school-profile-settings.component.ts`; Modify `pages/settings/settings.component.ts`.

- [ ] **Step 1: Build `SchoolProfileSettingsComponent`** (standalone, signals, `LoadingComponent`): loads `getSchoolProfile()`, form bound with `[(ngModel)]`, region as a `<select>` fed by `psgc.index().regions` (store both code and name), Save via `updateSchoolProfile`, errors via `formatApiError` → `notify`. A Registrar sees values read-only with the note "Only an Admin can change the school profile." (gate on `auth.userRole()`).
- [ ] **Step 2: Add the tab** — push `{ id: 'school-profile', label: 'School Profile' }` to `tabs` (after School Year) and render `<app-school-profile-settings>` in its `@if` block; add the component to `imports`.
- [ ] **Step 3: `npm run build` clean + commit.** `git commit -m "DepEd reports: Settings School Profile tab"`

### Task 13: Enrollment detail — DepEd/LIS card, withdrawal, LIS status, guardian relationship

**Files:** Modify `enrollment-detail.component.ts`; `student-form.component.ts`, `my-profile.component.ts`, `child-profile.component.ts` (+ `StudentForm`/`ProfileSelfUpdate` types).

- [ ] **Step 1: Learner-tab DepEd/LIS card** — Repeater checkbox + first-attendance date (placeholder = school-year start) + Save → `updateEnrollmentLisDetails`; follow the existing `saveLearnerInfo` signal pattern.
- [ ] **Step 2: Withdrawal panel** — "Mark dropped / transferred out" button shown only for Approved/Paid/Enrolled; a `notify.prompt`-driven or inline modal collecting type/date/reason/destination → `markEnrollmentWithdrawn`; once marked, a summary line + Undo (confirm) → `undoEnrollmentWithdrawal`.
- [ ] **Step 3: LIS status line** — "Encoded in LIS on <date> by <name>" or "Not yet encoded", with mark/undo via `setLisEncoded([enrollmentId], …)`.
- [ ] **Step 4: Guardian relationship field** beside `guardianName` in the staff student form and the two family-profile pages; add `guardianRelationship` to `StudentForm`/`ProfileSelfUpdate` and the load/save mappings; suggestions: Mother, Father, Grandparent, Aunt/Uncle, Sibling, Other relative, Non-relative.
- [ ] **Step 5: `npm run build` clean + commit.** `git commit -m "DepEd reports: enrollment LIS/withdrawal UI + guardian relationship"`

### Task 14: LIS worklist page

**Files:** Create `pages/reports/deped/lis-worklist.component.ts`; Modify `app.routes.ts`, `main-layout.component.ts`.

- [ ] **Step 1: Build the page** — section list with progress bars (listed/encoded/pending); on section select, the header, a "Fix before encoding" box (each row `routerLink` to `/students/:id`), and the two groups with checkboxes + "Mark encoded"/"Undo" + "Mark all in group" via `setLisEncoded`; withdrawn learners shown with a DRP/T-O label. Signals for `report`/`loading`/`loadFailed`; `sectionId`/`noSection` synced to the URL.
- [ ] **Step 2: Route** — add `reports/deped/lis-worklist` under the shell children (`canActivate: [roleGuard], data: { roles: ['Admin','Registrar'] }`).
- [ ] **Step 3: Nav** — add a "Reports" `folio-eyebrow` header with a "DepEd" subsection in the staff `@else` branch of `main-layout`; link "LIS Worklist" (and the next two pages).
- [ ] **Step 4: `npm run build` clean + commit.** `git commit -m "DepEd reports: LIS worklist page + nav"`

### Task 15: Quick count page + print

**Files:** Create `pages/reports/deped/quick-count.component.ts`, `pages/print/print-quick-count.component.ts`; Modify `app.routes.ts`.

- [ ] **Step 1: Quick count page** — date picker defaulting to `manilaToday()`, the grade×sex grid with the change column, the "Sex not set" list linking to `/students/:id`, Print (`window.open('/print/quick-count?…')`) and Download CSV (`exportQuickCount` blob dance).
- [ ] **Step 2: Print page** — copy `print-enrollment-register.component.ts` structure + `@media print` styles + `maybeAutoPrint()`; reads `schoolYear`/`asOf` from `route.snapshot.queryParamMap`.
- [ ] **Step 3: Routes** — `reports/deped/quick-count` under the shell; `print/quick-count` top-level with `[authGuard, roleGuard]`.
- [ ] **Step 4: `npm run build` clean + commit.** `git commit -m "DepEd reports: quick count page + print"`

### Task 16: SF1 register page + print

**Files:** Create `pages/reports/deped/sf1-register.component.ts`, `pages/print/print-sf1.component.ts`; Modify `app.routes.ts`.

- [ ] **Step 1: SF1 page** — section picker, on-screen table, Print + Download CSV (`exportSf1`), and the banner "Working copy for checking against LIS. The official SF1 is generated in LIS."
- [ ] **Step 2: Print page** — landscape long bond: add `@page { size: 13in 8.5in; }` to this print page's styles (the one `@page` rule in the app); footer "Prepared by: <adviser>" and "Certified correct: <school head>" signature lines; `maybeAutoPrint()`.
- [ ] **Step 3: Routes** — `reports/deped/sf1-register` under the shell; `print/sf1/:sectionId` top-level with `[authGuard, roleGuard]`.
- [ ] **Step 4: Missing-profile note** — report headers show "School ID not set" (linking to Settings → School Profile for Admins) wherever the profile is empty; apply on all three report pages.
- [ ] **Step 5: `npm run build` clean + commit.** `git commit -m "DepEd reports: SF1 register page + print"`

---

## Verification (final task)

- [ ] `dotnet build Enrollify.sln` — 0 errors/0 warnings.
- [ ] `dotnet test` — all green (the suite grows by the DepEd tests).
- [ ] `npm run build` — clean; `npm test` (Karma) — green.
- [ ] Migration re-verified on a throwaway DB (not `EnrollifyDb`).
- [ ] Hand the manual browser checklist (spec §5) to the user: Settings → School Profile as Admin and Registrar; enrollment DepEd/LIS card, withdrawal dialog + undo; worklist mark/undo; quick-count date change; SF1 print preview on long bond landscape; phone width for the three report pages.

---

## Self-Review notes

- **Spec coverage:** §1 → Task 1; §2 → Task 2; §3.1 → Task 3; §3.2 → Task 4; §3.3 → Task 5; §3.4 → Task 6; §3.5 worklist/quick-count/SF1 → Tasks 8/9/10; the guardian-relationship approval copy (§1 Student) → Task 7; §4 → Tasks 11–16; §5 seed/migration → Task 1, tests distributed across the owning tasks, manual checklist → Verification.
- **Review Focus:** each of the five lines is pinned to a named test in Task 2, 5, 6, or 9.
- **Type consistency:** `SchoolHeaderDto` is defined once (Task 8) and reused by Tasks 9–10; the eight new `EnrollmentDto` fields are added in Task 4 and consumed by the enrollment-detail UI (Task 13).
- **Shared-report pattern:** each report query owns its filter/order/project inline and the export query reuses it, exactly as `CollectionsReport`/`EnrollmentRegisterReport` do, so paged and export views never diverge.
