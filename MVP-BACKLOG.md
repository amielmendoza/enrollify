# MVP Backlog

Gap analysis of the enrollment process (2026-07-31). Ordered by priority; check items off as they land.

## Blockers (process-breaking) — ALL DONE 2026-07-31

- [x] **1. Tenant provisioning** — every tenant except the seeded demo school was dead on arrival.
  - Done: `MoveEnrollmentStepCommand` falls back to the standard transition table via the new shared `DefaultWorkflow` (Application/Features/Workflows); `CreateTenantCommand` seeds the default workflow for new tenants; startup seeder backfills workflows, school years, payment terms, and requirement templates **per tenant** (the old global-existence guards starved tenant 2); the raw T-SQL cursor seeder was replaced with LINQ.
- [x] **2. Proof-of-payment upload was cosmetic** — receipt file never left the browser.
  - Done: `Payment.ReceiptFileName/ReceiptFileUrl` (+ migration `AddPaymentReceiptFields`), accepted through all three payment commands and DTOs; `child-payments` / `my-payments` now upload the file before recording the payment; enrollment-detail shows a "View receipt" link on each payment (or "No receipt attached").
- [x] **3. Applicant heard nothing after applying.**
  - Done: anonymous `GET api/schools/{slug}/applications/{number}/status` + `/:slug/status` lookup page (linked from the apply success screen with the number prefilled); success copy no longer promises "credentials via email" — it tells applicants to save their number and explains the temporary password.
  - Still open post-MVP: real email notifications (see Deferred).
- [x] **4. Authorization lockdown.**
  - Done: `register` is Admin-only and `RegisterCommand` refuses privileged roles; `move-step`, `cancel`, `assign-section`, `POST payments`, payments-by-enrollment/balance, students CRUD, enrollments list/detail → `Admin,Registrar`; `GET api/files/{id}` now requires auth and is tenant-scoped; all file links download via authenticated blob (`ApiService.openFile`), which also removed the three hardcoded `http://localhost:5221` prefixes; `roleGuard` protects the admin/student/parent/super route groups.
- [x] **5. Forward-only state machine.**
  - Done: `EnrollmentStatus.Cancelled` + `CancelEnrollmentCommand` + `POST api/enrollments/{id}/cancel` + Cancel button and cancelled-state banner in enrollment-detail; duplicate-enrollment checks ignore cancelled rows so a fresh enrollment can be requested; rejecting a submitted requirement now rolls the enrollment back to Draft (with history) so the parent/student can re-upload.
- [x] **6. Grade levels / ₱0 assessments.**
  - Done: shared `GRADE_LEVELS` + `ENROLLMENT_STATUS_NAMES` constants (`core/constants.ts`) replace the drifting per-component copies (the wizard was missing Kindergarten); Submitted→Assessed is now blocked server-side when no active fees match the enrollment's (SchoolYear, GradeLevel).
  - Still open: seed/setup flow only creates fees for Grade 7 in the demo tenant — real schools must configure fees per grade in Settings.
- [x] **7. Money math.**
  - Done: `GetBalanceQuery` uses the shared `PaymentsCalculator.EffectiveTotal` (discount/interest respected, endpoints agree); the Approved→Paid gate reads the tenant's `PaymentTerm` (and a discounted Full payment no longer demands 100%); all five self-service handlers pick the enrollment via `EnrollmentSelector` (active school year first, newest first, cancelled excluded).

## Next up (ordered, decided 2026-07-31) — ALL 13 DELIVERED 2026-08-14

All three batches implemented by the agent team (team lead + backend + UI/UX + database devs), each batch
team-lead reviewed with required fixes applied. Final state: backend build 0 errors/0 warnings, `dotnet test`
48/48 green (new `backend/tests/Enrollify.Application.Tests`), frontend production build clean.

Follow-ups noted by the final review (small, non-blocking):
- [ ] Seeder's local grade array in `ApplicationDbContextSeed.cs` should reference the shared `Application/Common/GradeLevels.All` (landed in the same batch; one-line swap next time the file is touched).
- [ ] A few fire-and-forget subscribes still lack error handlers (e.g. settings `deleteFee`/`deleteSection`) — the last few percent of the error-surfacing work.

## Batch 4 — Student ledgers / Statement of Account (delivered 2026-08-14, team-lead APPROVED)

Per-enrollment ledger: Charge entries from the assessment fee snapshot, Discount/Interest mirroring
`EffectiveTotal`, manual Adjustments (add-only, voidable with actor + reason, never edited/deleted),
approved Payments — chronological with server-computed running balance. New `LedgerAdjustments` table
(migration `AddLedgerAdjustments`). Endpoints: staff `GET /enrollments/{id}/ledger` + post/void
adjustments, student `GET /enrollments/me/ledger`, parent `GET /parent/children/{sid}/ledger`.
Adjustments integrated into all balance reads (the Approved→Paid down-payment gate deliberately stays
assessed-fees-based). UI: Ledger card with add/void/print on enrollment-detail, read-only Account Ledger
cards on the parent/student payment pages, printable Statement of Account (voided rows omitted).
Verified: backend 0 errors, `dotnet test` 64/64, frontend build clean; team lead hand-traced ledger vs
balance-endpoint agreement.

Follow-ups (non-blocking):
- [ ] Assessment-slip footnote should also mention manual adjustments as a reason Balance can differ (`print-enrollment.component.ts`, one line).
- [ ] Rename misleading `isAdmin()` in `enrollment-detail.component.ts` (it means Admin-or-Registrar; behavior is correct).

## Batch 5 — Collections journal (delivered 2026-08-14, team-lead APPROVED after fixes)

Cashier's journal over verified (Approved) payments, date-basis PaymentDate: `GET /api/reports/collections`
(date-range + method filter, journal-ordered with deterministic Id tie-break, paged up to 500, full-range
summary: total / by method / by day) and `GET /api/reports/collections/export` (CSV, RFC 4180, shares the
exact filter/projection with the view). Collections page for Admin/Registrar (Today / This Week / This Month
presets, summary tiles, day-grouped table with honest day subtotals from the full-range summary, pagination,
CSV download) + printable Collections Journal with per-method summary, signature line, and a data-derived
truncation notice (never silent). Supporting index `Payments (TenantId, Status, PaymentDate)` (migration
`AddPaymentsCollectionsIndex`). Review caught and fixed a page-cap seam (backend clamp 200 vs print's 500)
and non-deterministic ordering on timestamp ties. Verified: backend 0 errors, `dotnet test` 72/72, frontend
build clean.

**Batch 1 — ship-readiness (small fixes; makes the build deployable)**
1. Slug-based school-years endpoint + use it on the apply page (School Year field is blank in prod / can show the wrong school's years in dev — depends on hardcoded `environment.defaultTenantId`).
2. Pagination UI on enrollments + admissions lists.
3. HTTP error interceptor: 401 → logout + redirect to login; surface errors on calls that currently swallow them.
4. Update commands + edit UI for Fees and Sections.
5. Admissions review polish: confirm dialog before approve; reject prompt fires even on cancel; render custom field values + parent contact in the detail modal.

**Batch 2 — registrar essentials**
6. Printables (print-CSS): Certificate of Registration, assessment slip, official receipt.
7. Minimal `IEmailSender` (SMTP, config-driven, no-op when unconfigured) wired to application approval (credentials), payment review, enrollment finalized.
8. Fee snapshot at assessment (persist assessed total/lines on Submitted→Assessed; balances read the snapshot).
9. Status history timeline on enrollment-detail (data already written, never displayed).

**Batch 3 — durability**
10. Backend test project: PaymentsCalculator, Approved→Paid gate, transition guards, cancellation rules.
11. JWT secret + CORS origins from environment config.
12. Year-2 re-enrollment / grade promotion.
13. Toast service; retire the 24 native `alert`/`confirm`/`prompt` calls.

## Should-fix (smaller, still MVP-relevant)

- [x] Pagination UI on enrollments + admissions lists. (Done 2026-08-14, batch 1.)
- [x] HTTP error interceptor: 401 → logout/redirect. (Done 2026-08-14, batch 1; a few stray calls still swallow non-401 errors — see follow-ups above.)
- [x] Edit support for Fees and Sections, including inactive-row visibility in Settings so deactivation is reversible. (Done 2026-08-14, batch 1.)
- [x] Payment-plan descriptions on payments pages are hardcoded and contradict the PaymentTerms API → drive from `GET api/paymentterms`. (Done 2026-07-31 alongside blocker 2.)
- [x] Admissions approve/reject: confirmation dialog; reject aborts on cancel; custom fields + parent contact rendered in the detail modal. (Done 2026-08-14, batch 1.)

## Deferred (post-MVP)

Delivered since this list was written (2026-08-14): printing (COR / assessment slip / receipt), fee snapshot
at assessment, grade promotion, status-history timeline, minimal SMTP email notifications, toast/dialog
system replacing all native browser dialogs.

Still deferred:
- Payment gateway integration (manual proof-of-payment works end-to-end now).
- Workflow CRUD UI (read-only tab; every tenant gets the default workflow + runtime fallback).
- Reports/exports (enrollment register, collection report, CSV/Excel).
- In-app notification center (bell/feed); richer email templates + per-tenant SMTP settings UI.
- Reactive form validation with per-field messages.
- Bulk operations (bulk sectioning, bulk review), refresh tokens, forgot-password self-service.
