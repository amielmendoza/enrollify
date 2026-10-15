# Sub-project 3 — DepEd reports (LIS worklist, quick count, SF1)

Status: design approved in conversation 2026-10-15 (parts 1–3). Written up for review.
Part of the gap-analysis roadmap (1a learner record → 1b SHS semesters → 2 fees/discounts/vouchers → **3 DepEd reports** → 4 data privacy → 5 payment & SMS seams → 6 admissions extras → 7 platform hardening → 8 global extras). Builds on 1a (learner record, merged to `main` at 25064f1).

## Goal

Speed up the registrar's LIS encoding. DepEd's Learner Information System (LIS) takes enrolment on screen only, class by class — it has no file import, and it generates the official SF1 itself. So Enrollify does not try to feed LIS; it prepares the registrar to type into it quickly and correctly:

1. **LIS Worklist** — per section, who to tick from LIS's Prospective List, who to search/register, what to fix first, and which learners are already encoded.
2. **Quick Count** — enrolled learners by grade level and sex as of a date, laid out like the LIS Quick Count Facility grid, with the change since the day before.
3. **SF1 Register** — a working copy of School Form 1 per section, to check against what LIS produces and for the adviser's file.

Success = a registrar can work through a section in LIS from the worklist without opening each student record, type the quick count from one screen, and catch data problems (missing sex, unchecked LRN) before they reach LIS.

## Decisions taken in brainstorming

- **Enrolled** means status `Approved`, `Paid` or `Enrolled`. `Draft`, `Submitted`, `Assessed` and `Cancelled` are not enrolled.
- **Withdrawal is a mark on the enrollment, not a status.** Status, payments and balance are untouched; the reports read the mark.
- **New data**: school DepEd profile on `Tenant`; `Student.GuardianRelationship`; `Enrollment.IsRepeater`; `Enrollment.FirstAttendanceDate`; the withdrawal mark; an "encoded in LIS" mark.
- **SF1 remark codes** are derived from data; nothing is typed by hand. ALIVE and ACL are out of scope.
- **Approach**: reports plus an LIS tracker (the encoded mark), not a file export.

## Non-goals (explicitly deferred)

- Any automated upload to LIS (LIS has no import).
- The official SF1 Excel template; SF2 (attendance) onward; end-of-year statuses (promoted / retained / irregular).
- ALIVE tag; ACL (accelerated) remark.
- Quick count split by SHS track/strand.
- Re-enrolling a withdrawn learner into the same school year (undo the mark instead).
- Server-side check that a PSGC region code exists in the dataset (shape only on the server, as in 1a; the picker guarantees membership).

## 1. Data model (migration `AddDepEdReports`)

### Tenant — the school's DepEd profile

| Field | Type / rule |
|---|---|
| `DepEdSchoolId` | nvarchar(6), null. Exactly 6 digits when set. |
| `DepEdRegionCode` | nvarchar(10), null. PSGC region code: 10 digits, last 8 are zeros. |
| `DepEdRegionName` | nvarchar(150), null. Required when the code is set. |
| `DepEdDivision` | nvarchar(150), null. Free text (e.g. "City of Manila"). |
| `DepEdDistrict` | nvarchar(150), null. Free text. |
| `SchoolHeadName` | nvarchar(150), null. Printed under "Certified correct" on SF1. |

### Student

| Field | Type / rule |
|---|---|
| `GuardianRelationship` | nvarchar(50), null. Same length as `AdmissionApplication.GuardianRelationship`. |

Approval (`ReviewApplicationCommand`) copies `app.GuardianRelationship` into a new Student, and into a reused Student only when the student's own value is empty. The migration backfills existing students once, in SQL: for each Student with a null value, take the `GuardianRelationship` of their most recently updated `Approved` application (`AdmissionApplications.StudentId`) that has a non-blank value. It is a one-time migration step (not a startup job), so a value staff later clear stays cleared.

### Enrollment

| Field | Type / rule |
|---|---|
| `IsRepeater` | bit, not null, default 0. |
| `FirstAttendanceDate` | date, null. Null means "the school year's start date". When set: on or after the school year's `StartDate`, on or before its `EndDate`, and on or before `WithdrawalDate` if withdrawn. |
| `WithdrawalType` | nvarchar(20), null: `Dropped` \| `TransferredOut`. Null = not withdrawn. |
| `WithdrawalDate` | date, null. Set exactly when `WithdrawalType` is set. |
| `WithdrawalReason` | nvarchar(500), null. Required for `Dropped`, optional for `TransferredOut`. |
| `TransferredToSchool` | nvarchar(200), null. Required for `TransferredOut`, null for `Dropped`. |
| `LisEncodedAt` | datetime2 (UTC), null. Null = not yet encoded in LIS. |
| `LisEncodedBy` | nvarchar(200), null. The marking user's full name at the time (snapshot, not a foreign key). |

Effective first attendance = `FirstAttendanceDate ?? SchoolYear.StartDate` (date part).

## 2. Shared rules (`Features/Reports/DepEd/DepEdRules.cs`)

One static class holds every rule the three reports share, so each rule is written and tested once. All three reports load the needed rows into memory (a school year is at most a few thousand enrollments) and apply these rules in C#.

**Philippine day.** Asia/Manila is UTC+8 with no daylight saving (as in `StudentNumbering`). The end of Manila day `d` in UTC is `d + 1 day, 00:00` minus 8 hours. "Today" means today's Manila date.

**Status on a day.** An enrollment's status on Manila day `d` is the `ToStatus` of its last `EnrollmentStatusHistory` row with `TransitionDate` before the end of `d` (ordered by `TransitionDate`). If the enrollment has history rows but none that early, it had no status that day. If it has **no history rows at all**, its current `Status` applies from its `CreatedAt` onward.

**Counted on a day.** Status on `d` is `Approved`, `Paid` or `Enrolled`, **and** it is not withdrawn on `d` (`WithdrawalDate` is null or later than `d`). A learner withdrawn on day X counts on X − 1 and not on X. A learner cancelled after approval counts on the days before the cancellation.

**Listed (worklist and SF1).** Current `Status` is `Approved`, `Paid` or `Enrolled`. Withdrawn learners stay listed with their mark; cancelled ones are not listed.

**Sex.** `Student.Gender` trimmed, case-insensitive: `male` or `m` → Male; `female` or `f` → Female; anything else (including null) → **not set**.

**School year start year.** `SchoolYear.StartDate.Year` for the matching `SchoolYear` row; if there is no row, the first four digits of the school year name (`"2026-2027"` → 2026).

**Age on SF1.** Whole years from `BirthDate` to the first Friday of June of the start year. The birthday counts on its own day (a learner born on that Friday's date is already a year older). No birth date → blank.

**Learner name.** `Last, First, Ext, Middle`, skipping blank parts (SF1's "Last Name, First Name, Name Extension, Middle Name"). Parents: `Last, First, Middle`; the mother uses her maiden name fields.

**Contact number.** The first non-blank of `GuardianContact`, `MotherContactNumber`, `FatherContactNumber`; otherwise blank.

**Dates on printouts and CSVs.** `MM/dd/yyyy`.

**SF1 remarks**, in this order, joined with `"; "`:

| Code | When |
|---|---|
| `T/I` | `LearnerType` is `Transferee`. |
| `B/A` | `LearnerType` is `Returning` (Balik-Aral). |
| `LE MM/dd/yyyy` | `FirstAttendanceDate` is set and later than the school year's `StartDate`. The date is the first attendance. |
| `CCT` | `Student.Is4PsBeneficiary`. |
| `LWD` | `Student.HasDisability`. |
| `DRP MM/dd/yyyy` | `WithdrawalType` is `Dropped`. |
| `T/O MM/dd/yyyy <school>` | `WithdrawalType` is `TransferredOut`; `<school>` is `TransferredToSchool`. |

Repeater is not an SF1 remark code; it appears only as an LIS tag on the worklist.

## 3. Backend

All new handlers follow the existing CQRS pattern and depend only on `IApplicationDbContext`; the tenant query filter scopes every read. The current tenant id comes from `ICurrentUserService.TenantId`; the current user's name from `Users` by `ICurrentUserService.UserId`.

### 3.1 School profile (`Features/SchoolProfile`)

- `GetSchoolProfileQuery` → `SchoolProfileDto(SchoolName, DepEdSchoolId, DepEdRegionCode, DepEdRegionName, DepEdDivision, DepEdDistrict, SchoolHeadName)`. `SchoolName` is `Tenant.Name` (read-only here; SuperAdmin still owns it).
- `UpdateSchoolProfileCommand` (all fields except `SchoolName`). Validation: School ID null or `^\d{6}$`; region code null or `^\d{2}0{8}$`, and region name required when the code is set; division, district and school head ≤ 150 characters. Blank strings are stored as null.
- `SchoolProfileController`: `GET api/school-profile` (`Admin,Registrar`), `PUT api/school-profile` (`Admin`).

### 3.2 Enrollment LIS details

`UpdateEnrollmentLearnerInfoCommand` locks at `Assessed` (strand can affect fees), but the repeater flag and first attendance date matter most after approval. So they get their own command:

- `UpdateEnrollmentLisDetailsCommand(EnrollmentId, IsRepeater, FirstAttendanceDate)`, allowed in any status except `Cancelled`. `FirstAttendanceDate` rules are in §1.
- `PUT api/enrollments/{id}/lis-details` (`Admin,Registrar`).
- The enrollment detail DTO gains `IsRepeater`, `FirstAttendanceDate`, the four withdrawal fields, `LisEncodedAt` and `LisEncodedBy`.

### 3.3 Withdrawal mark

- `MarkEnrollmentWithdrawnCommand(EnrollmentId, Type, Date, Reason, TransferredToSchool)`:
  - Status must be `Approved`, `Paid` or `Enrolled`; otherwise rejected.
  - Must not already be withdrawn (undo first).
  - `Date` ≥ effective first attendance and ≤ today (Manila).
  - `Type` is `Dropped` or `TransferredOut`. `Dropped`: reason required (≤ 500), no destination. `TransferredOut`: destination required (≤ 200), reason optional.
  - Adds an `EnrollmentStatusHistory` row with `FromStatus = ToStatus = current status` and remarks `Marked dropped as of MM/dd/yyyy: <reason>` or `Marked transferred out as of MM/dd/yyyy to <school>`.
- `UndoEnrollmentWithdrawalCommand(EnrollmentId)`: must be withdrawn; clears all four fields; adds a history row `Withdrawal mark removed (was dropped|transferred out as of MM/dd/yyyy)`.
- `POST api/enrollments/{id}/withdrawal`, `DELETE api/enrollments/{id}/withdrawal` (`Admin,Registrar`).
- **Bulk re-enroll** (`BulkReenrollCommand`) skips source enrollments that carry a withdrawal mark, and `BulkReenrollResultDto` gains `SkippedWithdrawn`. Creating an enrollment for that learner by hand still works (a dropped learner may come back next year as Balik-Aral).
- Nothing else changes: cancel, payments, ledger and balance ignore the mark.

### 3.4 LIS encoded mark

- `SetLisEncodedCommand(IReadOnlyList<Guid> EnrollmentIds, bool Encoded)`: 1–500 distinct ids. Every id must exist (in the tenant) and be listed (current status `Approved`, `Paid` or `Enrolled`), otherwise the whole request is rejected and nothing changes. `Encoded = true` stamps `LisEncodedAt = UtcNow` and `LisEncodedBy` on enrollments not yet marked (already-marked ones keep their original stamp). `Encoded = false` clears both. Returns the number of enrollments changed.
- `POST api/enrollments/lis-encoded` (`Admin,Registrar`).

### 3.5 Report queries (`Features/Reports/DepEd`) and endpoints

All under `ReportsController` (`Admin,Registrar`). `schoolYear` defaults to the active school year when omitted. Each report response carries a `SchoolHeader` (school name, School ID, region name, division, district, school year), with nulls where the profile is not set.

**LIS worklist** — `GET api/reports/deped/lis-worklist?schoolYear=&sectionId=&noSection=`
- Always returns `Sections`: every section of the school year (grade-level order from `GradeLevels.All`, then name), each with listed, encoded and pending counts; plus the same counts for listed learners with no section.
- With `sectionId` (or `noSection=true`) it also returns the section header (grade, section name, adviser) and that group's learners.
- Each learner row: enrollment id, student id, LRN, `LrnNeedsReview`, learner name, sex (Male/Female/not set), birth date, learner type, effective first attendance, tags (`4Ps`, `Balik-aral`, `Repeater`), withdrawal type and date, `LisEncodedAt`, `LisEncodedBy`, and fix reasons.
- Rows are split into two groups, each sorted by last name, first name, middle name:
  - **Continuing**: `LearnerType` is `Continuing`. Ticked from LIS's Prospective List and batch-enrolled.
  - **Search or register**: `New`, `Transferee`, `Returning`, or no learner type. Found by search in LIS, or registered there.
- **Fix reasons** (a learner can have several): `Sex not set`; `No birth date`; `LRN needs checking` (`LrnNeedsReview`); `No LRN` (no LRN and learner type is not `New`); `Learner type not set`. A `New` learner without an LRN is not a problem: the row notes that LIS will assign one, to be entered on the student record afterwards.

**Quick count** — `GET api/reports/deped/quick-count?schoolYear=&asOf=yyyy-MM-dd` and `.../quick-count/export` (CSV)
- `asOf` defaults to today (Manila).
- Rows: one per grade level with anyone counted on `asOf` or the day before, in `GradeLevels.All` order; unknown grade strings follow, sorted alphabetically. Then a total row.
- Columns: Male, Female, Not set, Total (= Male + Female + Not set) for `asOf`, and the change since the day before (`asOf − 1`) for each.
- `NotSetLearners`: enrollment id, student id, name and grade of every counted learner with sex not set, so the page can link to them.
- CSV columns: `Grade Level, Male, Female, Sex Not Set, Total, Male Change, Female Change, Sex Not Set Change, Total Change`, with a final `Total` row.

**SF1** — `GET api/reports/deped/sf1?schoolYear=&sectionId=` and `.../sf1/export` (CSV); `sectionId` required.
- Header: the `SchoolHeader` plus grade level, section name, adviser, and `SchoolHeadName`.
- Learners: the section's listed enrollments. Order: Male A–Z, then Female A–Z, then not set A–Z (by last, first, middle).
- Columns: LRN; name; sex (`M`/`F`/blank); birth date; age (§2); mother tongue; IP group (`IpCommunity` when `IsIndigenousPeople`, else blank); religion; house/street (`Student.Address`); barangay, city/municipality, province (names from `HomeAddress`); father's name; mother's maiden name; guardian name; guardian relationship; contact number (§2); remarks (§2).
- Totals: male, female, not set, total.
- CSV: one row per learner in the same order and columns, header row first. CSV escaping follows `EnrollmentRegisterCsvBuilder`.

## 4. Frontend

- **Models and `ApiService`**: types and methods for every endpoint above.
- **Settings → School Profile** tab, placed after School Year. A new standalone component `pages/settings/school-profile-settings.component.ts` embedded in the tab, so `settings.component.ts` (1,784 lines) does not grow. Region is a select fed by `PsgcService` regions. A Registrar sees the values read-only with "Only an Admin can change the school profile."
- **Enrollment detail**:
  - On the Learner tab, a "DepEd / LIS" card: Repeater checkbox, first attendance date (placeholder shows the school year start), Save.
  - Withdrawal panel:
    - A "Mark dropped / transferred out" button, shown only when the status is Approved, Paid or Enrolled.
    - A dialog with type, date, reason and destination school.
    - Once marked, a summary line with an Undo button that asks for confirmation.
  - LIS status: "Encoded in LIS on <date> by <name>" or "Not yet encoded", with mark and undo.
- **Guardian relationship**: wherever staff, parents or students already edit `GuardianName`, a "Relationship" input sits beside it, with suggestions (Mother, Father, Grandparent, Aunt/Uncle, Sibling, Other relative, Non-relative). Every DTO and command that carries `GuardianName` also carries `GuardianRelationship`.
- **Report pages** under `pages/reports/deped/`, linked from a "DepEd" group in the Reports section of the sidebar (`main-layout.component.ts`):
  - `lis-worklist.component.ts`:
    - A section list with progress bars.
    - For the chosen section: the header, a "Fix before encoding" box (each row links to the student), and the two groups.
    - Checkboxes with "Mark encoded" / "Undo", and "Mark all in group".
    - Withdrawn learners shown with a DRP or T/O label.
  - `quick-count.component.ts`: date picker (default today in Manila), the grid, the "Sex not set" list with links, Print and Download CSV.
  - `sf1-register.component.ts`: section picker, an on-screen table, Print and Download CSV, and the banner "Working copy for checking against LIS. The official SF1 is generated in LIS."
- **Print pages**: `print/sf1/:sectionId?schoolYear=` and `print/quick-count?schoolYear=&asOf=`, in the same style as the existing print pages.
  - SF1 prints landscape on long bond paper (`@page { size: 13in 8.5in; }`).
  - The SF1 footer has "Prepared by: <adviser>" and "Certified correct: <school head>" signature lines.
- **Missing school profile**: report headers show "School ID not set" (linking to Settings → School Profile for Admins) wherever the profile is empty.
- A shared `manilaToday()` helper in `core/` supplies the default date.

## 5. Seed, migration and testing

- **Seeder**: the demo tenant gets an obviously placeholder DepEd profile: School ID `000000`, region NCR (`1300000000`, "National Capital Region (NCR)"), division "Demo Division", district "Demo District", school head "Demo School Head". It is set only when the tenant is created, so existing databases keep their (empty) profile until an Admin fills it in.
- **Migration**: `AddDepEdReports` adds the columns in §1 and runs the guardian-relationship backfill SQL. It is verified on a throwaway SQL Server database (fresh seed, then the API smoke-tested on a separate port, then dropped), never on `EnrollifyDb`.
- **Backend tests** (xUnit, `Enrollify.Application.Tests`, in-memory `TestDb`):
  - `DepEdRules`:
    - Day boundary: approved at 23:30 Manila counts that day; approved at 00:30 the next day does not.
    - No history: current status counts from `CreatedAt`.
    - Approved, then cancelled: counted before the cancellation, not after.
    - Withdrawn on X: counted on X − 1, not on X.
    - Sex: `"male"`, `" M "`, `"Female"`, `"f"`, `"Other"`, null.
    - Age: birthday on the first Friday itself, the day after it, a Feb 29 birth, no birth date.
    - Start year without a `SchoolYear` row.
    - Each remark code alone, all together in order, and LE not raised when first attendance equals the start date.
    - Name format with and without extension and middle name.
    - Contact fallback order.
  - **Quick count**: grade order including an unknown grade; the not-set column and list; change since the previous day; CSV header and total row.
  - **Worklist**: section counts including the no-section group; group split; every fix reason; a `New` learner without an LRN raises no reason; withdrawn learners still listed; cancelled ones not listed.
  - **SF1**: sex grouping and order; columns from `HomeAddress`; IP group only when `IsIndigenousPeople`; totals; CSV columns match the screen.
  - **Withdrawal**:
    - Each status guard; date earlier than first attendance rejected; future date rejected.
    - Dropped requires a reason; TransferredOut requires a destination; already-withdrawn rejected.
    - A history row is written; undo works; status and balance unchanged.
  - **LIS encoded**: stamps name and time; an already-marked learner keeps the original stamp; clearing works; the whole request is rejected if any id is not listed.
  - **LIS details**: editable after approval; rejected when cancelled; date range rules.
  - **Bulk re-enroll**: withdrawn enrollments skipped and counted in `SkippedWithdrawn`.
  - **School profile**: School ID and region validation; blank strings become null.
  - **Approval**: guardian relationship copied to a new student, and to a reused student only when empty.
- **Frontend**: `npm run build` clean; Karma suite green, with a spec for `manilaToday()`.
- **Manual browser check (user)**:
  - Settings → School Profile as Admin and as Registrar.
  - Enrollment DepEd/LIS card, withdrawal dialog and undo.
  - Worklist mark/undo.
  - Quick count date change.
  - SF1 print preview on long bond, landscape.
  - Phone width for the three report pages.
