import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { NotificationService } from '../../core/services/notification.service';
import { SchoolYearService } from '../../core/services/school-year.service';
import { LoadingComponent } from '../../core/components/loading.component';
import { EnrollmentRegister, EnrollmentRegisterFilters, Section } from '../../core/models';
import { ENROLLMENT_STEP_NAMES, GRADE_LEVELS, enrollmentStatusBadgeClass } from '../../core/constants';
import { formatApiError } from '../../core/utils/format-api-error';

// The registrar's official list of who is enrolling/enrolled in one school year. Filters
// live in the URL (shareable, survives reload). Cancelled enrollments are excluded unless
// "Include cancelled" is ticked — the server applies the same rule to the CSV and the print.
@Component({
  selector: 'app-enrollment-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingComponent],
  template: `
    <div>
      <div class="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Enrollment Register</h1>
          <p class="mt-1 text-sm text-gray-500">Every enrollment in a school year, by grade and name</p>
        </div>
        <div class="flex gap-2">
          <button (click)="exportCsv()" [disabled]="exporting() || !report()"
                  class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {{ exporting() ? 'Exporting...' : 'Export CSV' }}
          </button>
          <button (click)="printRegister()" [disabled]="!report()"
                  class="rounded-lg bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85] disabled:opacity-60">
            Print Register
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-4 flex flex-wrap items-end gap-3">
        <div>
          <label class="form-label" for="reg-sy">School Year</label>
          <select id="reg-sy" [(ngModel)]="schoolYear" (ngModelChange)="onScopeChange()" class="form-input w-auto">
            @for (sy of syService.schoolYears(); track sy.id) { <option [value]="sy.name">{{ sy.name }}{{ sy.isActive ? ' (active)' : '' }}</option> }
          </select>
        </div>
        <div>
          <label class="form-label" for="reg-grade">Grade</label>
          <select id="reg-grade" [(ngModel)]="gradeLevel" (ngModelChange)="onScopeChange()" class="form-input w-auto">
            <option value="">All grades</option>
            @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
          </select>
        </div>
        <div>
          <label class="form-label" for="reg-section">Section</label>
          <select id="reg-section" [(ngModel)]="sectionId" (ngModelChange)="onFilterChange()" class="form-input w-auto" [disabled]="sections().length === 0">
            <option value="">All sections</option>
            @for (s of sections(); track s.id) { <option [value]="s.id">{{ s.name }} ({{ s.gradeLevel }})</option> }
          </select>
        </div>
        <div>
          <label class="form-label" for="reg-status">Status</label>
          <select id="reg-status" [(ngModel)]="status" (ngModelChange)="onFilterChange()" class="form-input w-auto">
            <option value="">All statuses</option>
            @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
          </select>
        </div>
        <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer pb-2.5">
          <input type="checkbox" [(ngModel)]="includeCancelled" (ngModelChange)="onFilterChange()" />
          Include cancelled
        </label>
      </div>

      <!-- Summary -->
      @if (report(); as r) {
        <div class="mt-6 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-5">
            <p class="text-sm text-gray-500">Enrollments</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ r.summary.totalCount }}</p>
          </div>
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-5 lg:col-span-1">
            <p class="text-sm text-gray-500 mb-2">By Status</p>
            <div class="flex flex-wrap gap-1.5">
              @for (s of r.summary.byStatus; track s.status) {
                <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium" [ngClass]="statusClass(s.status)">{{ s.status }} <span class="font-semibold">{{ s.count }}</span></span>
              } @empty { <span class="text-xs text-gray-400">None</span> }
            </div>
          </div>
          <div class="sm:col-span-2 bg-white rounded-xl border border-[#E2D9C2] p-5">
            <p class="text-sm text-gray-500 mb-2">By Grade</p>
            <div class="flex flex-wrap gap-1.5">
              @for (g of r.summary.byGrade; track g.gradeLevel) {
                <span class="inline-flex items-center gap-1.5 rounded-full bg-gray-50 border border-gray-200 px-2.5 py-1 text-xs"><span class="text-gray-700">{{ g.gradeLevel }}</span> <span class="font-semibold text-gray-900">{{ g.count }}</span></span>
              } @empty { <span class="text-xs text-gray-400">None</span> }
            </div>
          </div>
        </div>
      }

      <!-- Register table -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Student No.</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">LRN</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Student</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Section</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Plan</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Assessed</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Paid</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Balance</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (row of report()?.items ?? []; track row.enrollmentId) {
                <tr class="hover:bg-gray-50">
                  <td class="px-4 py-3 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ row.studentNumber }}</td>
                  <td class="px-4 py-3 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ row.lrn || '—' }}</td>
                  <td class="px-4 py-3 text-sm whitespace-nowrap">
                    <a [routerLink]="['/enrollments', row.enrollmentId]" class="font-medium text-[#0038A8] hover:text-[#002B85]">{{ row.studentName }}</a>
                  </td>
                  <td class="px-4 py-3 text-sm text-gray-700 whitespace-nowrap">{{ row.gradeLevel }}</td>
                  <td class="px-4 py-3 text-sm text-gray-600">{{ row.sectionName || '—' }}</td>
                  <td class="px-4 py-3 text-sm">
                    <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium" [ngClass]="statusClass(row.status)">{{ row.status }}</span>
                  </td>
                  <td class="px-4 py-3 text-sm text-gray-600">{{ row.paymentPlan || '—' }}</td>
                  <td class="px-4 py-3 text-sm text-right folio-mono text-gray-700 whitespace-nowrap">{{ row.assessedTotal === null ? '—' : '₱' + (row.assessedTotal | number:'1.2-2') }}</td>
                  <td class="px-4 py-3 text-sm text-right folio-mono text-gray-700 whitespace-nowrap">₱{{ row.totalPaid | number:'1.2-2' }}</td>
                  <td class="px-4 py-3 text-sm text-right folio-mono whitespace-nowrap" [class.text-red-700]="(row.balance ?? 0) > 0" [class.text-gray-700]="(row.balance ?? 0) <= 0">{{ row.balance === null ? '—' : '₱' + (row.balance | number:'1.2-2') }}</td>
                </tr>
              } @empty {
                <tr><td colspan="10">
                  @if (loading()) { <app-loading label="Loading register..." /> }
                  @else if (loadFailed()) { <p class="px-6 py-8 text-center text-red-600">The register couldn't be loaded. Adjust the filters or refresh to try again.</p> }
                  @else { <p class="px-6 py-8 text-center text-gray-400">No enrollments match these filters.</p> }
                </td></tr>
              }
            </tbody>
          </table>
        </div>

        @if (report() && totalPages() > 1) {
          <div class="flex flex-wrap items-center justify-between gap-3 p-4 border-t border-gray-200">
            <p class="text-sm text-gray-500">Showing page {{ page() }} of {{ totalPages() }} ({{ report()!.totalCount }} total)</p>
            <div class="flex gap-2">
              <button (click)="loadPage(page() - 1)" [disabled]="page() <= 1"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Previous</button>
              <button (click)="loadPage(page() + 1)" [disabled]="page() >= totalPages()"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Next</button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class EnrollmentRegisterComponent implements OnInit {
  report = signal<EnrollmentRegister | null>(null);
  loading = signal(true);
  loadFailed = signal(false);
  exporting = signal(false);
  sections = signal<Section[]>([]);
  page = signal(1);
  readonly pageSize = 50;
  totalPages = computed(() => Math.max(1, Math.ceil((this.report()?.totalCount ?? 0) / this.pageSize)));

  schoolYear = '';
  gradeLevel = '';
  status = '';
  sectionId = '';
  includeCancelled = false;

  readonly gradeLevels = GRADE_LEVELS;
  // Cancelled is reached through "Include cancelled", not the status list.
  readonly statuses = ENROLLMENT_STEP_NAMES;

  constructor(
    private api: ApiService,
    public syService: SchoolYearService,
    private route: ActivatedRoute,
    private router: Router,
    private notify: NotificationService) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.gradeLevel = qp.get('gradeLevel') ?? '';
    this.status = qp.get('status') ?? '';
    this.sectionId = qp.get('sectionId') ?? '';
    this.includeCancelled = qp.get('includeCancelled') === 'true';
    this.syService.ensureLoaded().subscribe({
      next: () => {
        this.schoolYear = qp.get('schoolYear') ?? this.syService.activeName() ?? '';
        this.loadSections();
        this.load();
      },
      // Year list unavailable: the server still resolves the active year (or explains why not).
      error: () => this.load()
    });
  }

  // Year or grade changed: the section list (and any chosen section) no longer applies.
  onScopeChange(): void {
    this.sectionId = '';
    this.loadSections();
    this.onFilterChange();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.syncUrl();
    this.load();
  }

  loadPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getEnrollmentRegister(this.filters(), this.page(), this.pageSize).subscribe({
      next: r => { this.loading.set(false); this.loadFailed.set(false); this.report.set(r); },
      error: (err) => {
        this.loading.set(false);
        this.loadFailed.set(true);
        this.report.set(null);
        this.notify.error(formatApiError(err, 'Failed to load the enrollment register.'));
      }
    });
  }

  exportCsv(): void {
    this.exporting.set(true);
    this.api.exportEnrollmentRegister(this.filters()).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `enrollment-register_${this.schoolYear || 'active-year'}.csv`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => {
        this.exporting.set(false);
        this.notify.error(formatApiError(err, 'Failed to export CSV.'));
      }
    });
  }

  printRegister(): void {
    window.open(`/print/enrollment-register?${new URLSearchParams(this.queryParams()).toString()}`, '_blank');
  }

  statusClass(status: string): string { return enrollmentStatusBadgeClass(status); }

  private loadSections(): void {
    if (!this.schoolYear) { this.sections.set([]); return; }
    this.api.getSections(this.schoolYear, this.gradeLevel || undefined).subscribe({
      next: s => this.sections.set(s),
      error: () => this.sections.set([])
    });
  }

  private filters(): EnrollmentRegisterFilters {
    return {
      schoolYear: this.schoolYear || undefined,
      gradeLevel: this.gradeLevel || undefined,
      status: this.status || undefined,
      sectionId: this.sectionId || undefined,
      includeCancelled: this.includeCancelled,
    };
  }

  private queryParams(): Record<string, string> {
    const q: Record<string, string> = {};
    if (this.schoolYear) q['schoolYear'] = this.schoolYear;
    if (this.gradeLevel) q['gradeLevel'] = this.gradeLevel;
    if (this.status) q['status'] = this.status;
    if (this.sectionId) q['sectionId'] = this.sectionId;
    if (this.includeCancelled) q['includeCancelled'] = 'true';
    return q;
  }

  // Keep the URL shareable as filters change (no history spam).
  private syncUrl(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: this.queryParams(), replaceUrl: true });
  }
}
