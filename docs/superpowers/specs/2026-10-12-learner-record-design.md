# Sub-project 1a — Philippine learner record

Status: design approved in conversation 2026-10-12 (sections 1–4); section 5 (migration & testing) added at spec time for review.
Part of the gap-analysis roadmap (1a learner record → 1b configurable SHS semesters → 2 fees/discounts/vouchers → 3 DepEd reports → 4 data privacy → 5 payment & SMS integration seams → 6 admissions extras → 7 platform hardening → 8 global extras).

## Goal

Enrollify records each learner the way DepEd expects: the real LRN (issued by DepEd LIS, not by us), the Basic Education Enrollment Form (BEEF) fields, SHS track/strand, and documents that are submitted once per learner (DepEd Order 17 s. 2025: PSA birth certificate once per learner). Success = sub-project 3 (SF1, LIS export, quick count) and sub-project 2 (strand fees, vouchers) can read everything they need from typed columns, with no dependency on any school-specific external setup.

## Non-goals (explicitly deferred)

- Configurable SHS semesters (sub-project 1b).
- Strand-based fees, discounts, vouchers (sub-project 2).
- SF1 / LIS export / quick count, and the school's own DepEd School ID on `Tenant` (sub-project 3).
- Privacy consent and data-subject requests (sub-project 4).
- Deleting orphaned file blobs (sub-project 7) — but see the shared-reference constraint in §4.
- Auto-matching legacy free-text addresses to PSGC.
- Server-side validation that a PSGC code exists in the dataset (shape-only on the server; the picker guarantees membership).

## 1. Data model

Approach A: typed columns, not a JSON bag or a separate profile table. The PSGC address is an EF Core **owned type** (`PsgcAddress`) shared by `Student` and `AdmissionApplication`, mapped to columns in the owner's table.

### Student (stable, per learner)

| Field | Type / rule |
|---|---|
| `StudentNumber` | **new**, required, `YYYY-NNNNN` (year in Asia/Manila time + per-tenant per-year sequence, 5-digit zero-padded). Unique per tenant. Minted by Enrollify. |
| `LRN` | becomes **nullable**. New writes: exactly 12 digits. Unique per tenant **when not null** (filtered unique index). Column stays `nvarchar(20)` so legacy values survive. |
| `LrnNeedsReview` | **new** bool. Set for all pre-migration students that have an LRN. Cleared by Confirm LRN or by editing the LRN. |
| `ExtensionName` | nvarchar(10), optional (Jr., Sr., II, III, IV suggested). |
| `PsaBirthCertificateNumber` | nvarchar(30), optional. |
| `PlaceOfBirth` | nvarchar(200), optional (municipality/city, free text). |
| `MotherTongue` | nvarchar(100), optional (still on SF1). |
| `Religion` | nvarchar(100), optional (on SF1). |
| `IsIndigenousPeople` + `IpCommunity` | bool + nvarchar(100); community required when true. |
| `Is4PsBeneficiary` + `FourPsHouseholdId` | bool + nvarchar(30); ID required when true. |
| `HasDisability` + `DisabilityTypes` | bool + nvarchar(500) (comma-separated values from `DepEdLists.DisabilityTypes`); ≥1 type required when true. |
| `FatherLastName/FirstName/MiddleName`, `FatherContactNumber` | nvarchar(100)×3 + nvarchar(50), optional. |
| `MotherMaidenLastName/FirstName/MiddleName`, `MotherContactNumber` | same. |
| `Address` (existing) | becomes the **house/street line**. Unchanged column. |
| `HomeAddress` (owned `PsgcAddress`) | `RegionCode/Name`, `ProvinceCode/Name`, `CityMunicipalityCode/Name`, `BarangayCode/Name` (codes nvarchar(10), names nvarchar(150)), `ZipCode` nvarchar(10). All nullable. Codes and names both stored so a PSGC revision never rewrites history. Province is null for cities directly under a region (NCR, independent component cities). |

Guardian fields (`GuardianName`, `GuardianContact`) are unchanged.

### Enrollment (per school year)

| Field | Type / rule |
|---|---|
| `LearnerType` | nvarchar(20): `New` \| `Continuing` \| `Transferee` \| `Returning` (Balik-Aral). Nullable for legacy rows. |
| `LastGradeLevelCompleted` | nvarchar(20), from `GradeLevels.All`. |
| `LastSchoolYearCompleted` | nvarchar(20) (`YYYY-YYYY`). |
| `LastSchoolAttended` | nvarchar(200). |
| `LastSchoolAddress` | nvarchar(500). |
| `LastSchoolId` | nvarchar(6), 6 digits when present (DepEd School ID). |
| `Track`, `Strand` | nvarchar(100) each; only for Grade 11/12; denormalized strings (like `GradeLevel`/`SchoolYear`). |

### AdmissionApplication

Mirrors every Student field above (including owned `HomeAddress`) plus `Lrn` and the Enrollment fields, so approval is pure transcription. Existing `PreviousSchool` / `PreviousSchoolAddress` columns are reused as last-school-attended / last-school-address (no rename; mapping happens at approval).

### New / changed configuration entities

- **`ShsStrand`** (`TenantEntity`): `Track` nvarchar(100), `Strand` nvarchar(100), `IsActive`, `DisplayOrder`. Unique (TenantId, Track, Strand). Seeded per tenant (only when the tenant has **no** rows at all, so deleting is respected) with: Academic — STEM, ABM, HUMSS, GAS; TVL — ICT, HE, IA, AFA; Sports; Arts and Design (for single-strand tracks `Strand` = track name). Schools edit to match their offerings, including newer curriculum names.
- **`RequirementTemplate.KeepOnFile`** bool (default false).
- **`ApplicationFormField.Section`** gains `Family`.

### Shared constants (`Application/Common/DepEdLists.cs`, mirrored in `core/constants.ts`)

- `LearnerTypes`: New, Continuing, Transferee, Returning.
- `DisabilityTypes` (BEEF categories): Visual Impairment, Hearing Impairment, Learning Disability, Intellectual Disability, Autism Spectrum Disorder, Emotional-Behavioral Disorder, Orthopedic/Physical Handicap, Speech/Language Disorder, Cerebral Palsy, Special Health Problem/Chronic Disease, Multiple Disorder, Cancer.
- `ExtensionNameSuggestions`: Jr., Sr., II, III, IV.
- `IsShsGrade(grade)`: Grade 11 or Grade 12.

## 2. Application form and approval

### Built-in form fields

Added to `DefaultApplicationFormFields` (so tenant creation, the boot seeder, and Restore Defaults all get them). All default **visible + optional**; admins change that through existing form settings.

- **Student section:** `lrn` (help: "Leave blank if none yet (e.g. incoming Kindergarten)"), `extensionName`, `psaBirthCertificateNumber`, `placeOfBirth`, `motherTongue`, `religion`, `indigenousPeople` (checkbox → community text), `fourPs` (checkbox → household ID), `disability` (checkbox → multi-select types). The existing `address` key becomes the PSGC picker + house/street line (same key, so existing visibility/required settings carry over).
- **Family section** (new, per applicant, AppliesTo Both): `fatherName` (last/first/middle + contact), `motherMaidenName` (last/first/middle + contact).
- **Enrollment section:** `learnerType`, `lastGradeLevelCompleted`, `lastSchoolYearCompleted`, `lastSchoolId`; existing `previousSchool` / `previousSchoolAddress` relabelled "Last school attended" / "Last school address". `strand` (track+strand picker) is shown only for Grade 11/12 when the tenant has active strands, and is then **always required** (core rule, not admin-configurable).

### Frontend structure

- Extract the four duplicated custom-field `@switch` blocks in `apply.component.ts` into one reusable `DynamicFieldComponent`; simple new built-ins use it too (bound to typed applicant properties instead of `customValues`).
- New `PsgcAddressComponent` (region → province → city/municipality → barangay, ZIP, house/street) shared by apply, staff student form, child-profile, my-profile. Province step skipped when the selected city has no province.
- Conditional follow-ups (IP community, 4Ps ID, disability types, strand) are hardcoded for these built-ins.
- Public apply page keeps the folio identity; identifiers use `folio-mono`.

### PSGC data

- Source of truth: PSA PSGC publication (quarterly; 10-digit codes; codes are strings — leading zeros significant).
- `enrollify.client/scripts/psgc/build-psgc.mjs` (Node, zero dependencies) reads a CSV export of the PSGC sheet and emits committed JSON:
  - `psgc/index.json` — regions, provinces, cities/municipalities (code, name, parent code, ZIP not included).
  - `psgc/barangays/{provinceOrRegionCode}.json` — barangays grouped by province (by region for province-less cities), loaded lazily when a province/city is chosen.
- A short README beside the script documents the refresh procedure.
- `PsgcService` (frontend) caches loaded files for the session.

### Server-side validation (`SubmitApplicationCommandValidator`)

- `lrn`: 12 digits when present.
- PSGC codes: 10 digits when present; name required with its code; barangay requires city; city requires region.
- `disabilityTypes`: each value in `DepEdLists.DisabilityTypes`; conditional requireds (IP community, 4Ps ID, ≥1 disability type).
- `learnerType` in `DepEdLists.LearnerTypes`; `lastSchoolId` 6 digits; `lastGradeLevelCompleted` in `GradeLevels.All`.
- Grade 11/12 with active strands → strand required and must match an active `ShsStrand`.
- Max lengths mirror the DB (over-length → 400, never a SQL 500), plus the existing admin-required-field enforcement.
- An LRN matching an existing student is **not** rejected (returning learner; approval dedups).

### Approval (`ReviewApplicationCommand`)

- New learner: all fields transcribed to the new `Student` (with a minted `StudentNumber`) and `Enrollment`.
- Dedup: match an active student by the **`Lrn` column** first, then FirstName+LastName+DOB (existing behaviour). On match, new DepEd fields **fill blanks only, never overwrite** (same rule as account links today).
- The custom-field `"lrn"` lookup in `ExtractProvidedLrn` is removed after the backfill in §5 moves values into the column.
- Remains atomic (`ExecuteInTransactionAsync`); StudentNumber collisions retried like today's LRN minting.

## 3. Staff, parent and student screens

- **Staff student form:** sections Identity / Background / Address / Family / Contact. Student No. read-only. `LrnNeedsReview` → amber "Check LRN" notice with **Confirm LRN** (`POST /api/students/{id}/confirm-lrn`); editing the LRN also clears the flag. New students require PSGC city/municipality + barangay; existing students with a legacy-only address may still save and show "Address not matched to PSGC yet".
- **Students list:** Student No. column; LRN column shows the value, "No LRN yet" (muted), or an amber "Check LRN" badge; filters **Missing LRN** / **LRN needs review** (`GET /api/students?missingLrn=&lrnNeedsReview=`); search matches name, Student No., LRN.
- **Enrollment detail + wizard:** learner type, previous-school fields, track/strand (Grade 11/12) on the overview; editable by Admin/Registrar until Assessed (`PUT /api/enrollments/{id}/learner-info`), locked afterwards; send-back unlocks.
- **Parent child-profile / student my-profile:** DepEd identity fields read-only with "Contact the registrar to correct these"; editable: contact, PSGC address + house/street, guardian, father/mother contact numbers (`UpdateChildProfileCommand` / `UpdateMyProfileCommand` extended).
- **Re-enrollment requests** (`RequestEnrollmentCommand`, `StudentEnrollmentCommands`): strand picker when the target grade is 11/12; learner type `Continuing`.
- **Settings:** new **SHS Strands** tab (list / add / edit / deactivate / reorder); Requirements tab gets a **Keep on file — submitted once per learner** checkbox; the Settings tab bar becomes horizontally scrollable on mobile.
- **Public endpoint:** `GET /api/schools/{slug}/shs-strands` (active only) for the apply form — inside the existing tenant-middleware slug exemption. Staff CRUD at `/api/shs-strands` [Admin, Registrar].
- **Printables / exports:** COR, assessment slip, section roster, enrollment register CSV show Student No. and LRN ("—" when absent). Identifiers `folio-mono`.
- **DTO hygiene:** `StudentDto` is positional and constructed in 9 files; introduce a single `StudentMapping.ToDto(Student)` and use it everywhere so future fields touch one place.

## 4. Documents kept on file; strand across years

### Shared requirement creation

Replace the five copy-pasted template loops (approval, staff create, parent request, student request, bulk re-enroll) with `EnrollmentRequirements.AddForAsync(context, enrollment, ct)` plus a batch overload for bulk re-enroll that resolves on-file documents for all students in **one** query.

For each active template matching the grade (`GradeLevel == null || == grade`):

- `KeepOnFile` template → find the learner's most recent requirement with the same `DocumentName` (trimmed, case-insensitive) that `IsVerified`, on **any** of the learner's enrollments including cancelled ones. If found, create the new requirement already `IsSubmitted = IsVerified = true`, copying `FileName`, the file reference (`Notes`), `VerifiedBy`, `VerifiedAt`, with `ReviewNotes = "On file — verified {date} (SY {year})"`.
- Otherwise (not KeepOnFile, or no verified copy — submitted-unverified and rejected copies don't count) → a fresh empty requirement, as today.

Staff may still reject a carried-over document; existing rejection recovery (back to Draft for re-upload) applies unchanged.

**Constraint for sub-project 7:** a `FileDocument` may now be referenced by several enrollments' requirements; any future blob cleanup must check all references before deleting.

### Strand and learner type across years

- **Bulk re-enroll:** 11→12 carries `Track`/`Strand`; 10→11 leaves them empty; `LearnerType = Continuing`; `BulkReenrollResultDto` gains `NeedsStrand` and Settings shows it.
- **Defaults:** application → family chooses (default New); staff wizard → staff chooses; requests and bulk → Continuing.
- **Assessment gate:** `MoveEnrollmentStepCommand` Submitted→Assessed refuses a Grade 11/12 enrollment with no strand when the tenant has any active strands: "Set the learner's track/strand before assessment." Families may still submit without one.
- **Strand writes** must reference an active strand and are only valid for Grade 11/12 (a strand sent for any other grade is refused). Enrollment grade is not editable after creation today, so no grade-change clearing is needed. Deactivating a strand in Settings leaves existing enrollments untouched.

## 5. Migration and testing (new — not yet reviewed in conversation)

### One EF migration `AddLearnerRecord`

1. Add all new nullable columns (Students, Enrollments, AdmissionApplications, RequirementTemplates) and the `ShsStrands` table.
2. Backfill `StudentNumber` in SQL using the Manila year (`YEAR(DATEADD(hour, 8, CreatedAt))`, matching live minting): `CONCAT(<manilaYear>, '-', RIGHT('00000' + CAST(ROW_NUMBER() OVER (PARTITION BY TenantId, <manilaYear> ORDER BY CreatedAt, Id) AS varchar), 5))`, then make it NOT NULL and add unique (TenantId, StudentNumber).
3. `LRN` → nullable; replace the (TenantId, LRN) unique index with a filtered one (`[LRN] IS NOT NULL`).
4. `UPDATE Students SET LrnNeedsReview = 1 WHERE LRN IS NOT NULL` — every pre-migration LRN is either Enrollify-minted or unverified, and minted values cannot be told apart from real ones (Region I school IDs begin with 100…).
5. `UPDATE RequirementTemplates SET KeepOnFile = 1 WHERE DocumentName LIKE '%PSA%' OR DocumentName LIKE '%Birth Certificate%'`.

### Idempotent startup backfill (`LearnerRecordBackfill`, called from the seeder)

- Per tenant: convert an existing **custom** form field keyed `lrn` into the built-in (`IsBuiltIn = true`, Section Student, Type Text; keep label/visible/required), then move `CustomFieldValues["lrn"]` into `AdmissionApplications.Lrn` where the column is null. Done in C# because the values are JSON.
- Ensure default `ShsStrand` rows and the new default form fields per tenant (same Ensure pattern as today; `CreateTenantCommand` does the same for new tenants).
- Seed data updated so demo students get StudentNumbers, a mix of with/without LRN, PSGC addresses, and one Grade 11 STEM enrollment.

### Minting

`StudentNumbering.NextStudentNumberAsync` replaces `NextLrnAsync`: max of the tenant's `{year}-` prefix + 1, retry up to 3× on the unique-index conflict (existing pattern). `CreateStudentCommand` no longer invents LRNs: a blank LRN stays null.

### Backend tests (xUnit + existing `TestDb` InMemory harness)

- StudentNumbering: format, per-tenant per-year sequence, Manila-year boundary, retry on collision.
- LRN: 12-digit rule, blank allowed, duplicate non-null refused on staff create/update (handler-level, since InMemory ignores filtered indexes), Confirm LRN clears the flag, editing clears the flag.
- Submit-application validation: each new rule in §2, including strand required/valid only for Grade 11/12 with active strands, and over-length → validation error.
- Approval: full transcription; dedup by `Lrn` column; fill-blanks-only on a matched student; atomic rollback still holds.
- Requirements helper: verified KeepOnFile carried (incl. from a cancelled enrollment); unverified/rejected not carried; non-KeepOnFile not carried; all five creation paths use it; bulk path produces the same result as the single path.
- Bulk re-enroll: 11→12 carries strand; 10→11 empty and counted in `NeedsStrand`; LearnerType Continuing.
- Assessment gate: refused without strand when strands exist; allowed when the tenant has none.
- Learner-info edits: invalid/inactive strand refused; strand on a non-SHS grade refused; locked after Assessed; unlocked after send-back.
- Backfill: custom `lrn` conversion moves values and is idempotent (second run is a no-op).
- Existing tests updated for `StudentDto`/`StudentMapping` and nullable LRN.

### Frontend verification

- Production build clean (`npm run build`).
- Karma spec for `PsgcAddressComponent` cascade resets (changing province clears city and barangay) and the province-less city path.
- Manual end-to-end through the running app: apply as a new parent with one Grade 11 child (PSGC address, strand) → approve → student has StudentNumber and fields → assess blocked without strand on a strand-less bulk-created Grade 11 → PSA carried over on re-enrollment → staff list filters and Check LRN flow.

## Risks

- **PSGC data acquisition:** the PSA download is an Excel file; the build script deliberately takes a CSV export to stay dependency-free. The plan must include obtaining and converting the current publication.
- **Apply form length:** ~20 more fields. Mitigated by defaults being optional, conditional follow-ups, and admin visibility toggles; the form groups DepEd fields under clear headings.
- **`StudentDto` and nullable `LRN` ripple:** every LRN consumer (9 backend files, 6 frontend files, prints, CSVs) must handle null — covered by the mapping helper and the test sweep.
