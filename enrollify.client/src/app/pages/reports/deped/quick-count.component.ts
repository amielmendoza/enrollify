import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchoolYearService } from '../../../core/services/school-year.service';
import { AuthService } from '../../../core/services/auth.service';
import { LoadingComponent } from '../../../core/components/loading.component';
import { QuickCount } from '../../../core/models';
import { formatApiError } from '../../../core/utils/format-api-error';
import { manilaToday } from '../../../core/utils/manila-today';

// DepEd "quick count": enrolled learners by grade and sex as of a date, with the change since the
// day before. Learners with no sex on file are counted separately and listed so they can be fixed.
@Component({
  selector: 'app-quick-count',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingComponent],
  template: `
    <div>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Quick Count</h1>
          <p class="mt-1 text-sm text-gray-500">Enrolled learners by grade and sex, with the change since the day before</p>
        </div>
        <div class="flex gap-2">
          <button type="button" (click)="exportCsv()" [disabled]="exporting() || !report()"
                  class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {{ exporting() ? 'Exporting...' : 'Download CSV' }}
          </button>
          <button type="button" (click)="print()" [disabled]="!report()"
                  class="rounded-lg bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85] disabled:opacity-60">
            Print
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-4 flex flex-wrap items-end justify-between gap-3">
        <div class="flex flex-wrap items-end gap-3">
          <div>
            <label class="form-label" for="qc-sy">School Year</label>
            <select id="qc-sy" [(ngModel)]="schoolYear" (ngModelChange)="onFilterChange()" class="form-input w-auto">
              @for (sy of syService.schoolYears(); track sy.id) { <option [value]="sy.name">{{ sy.name }}{{ sy.isActive ? ' (active)' : '' }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label" for="qc-asof">As of</label>
            <input id="qc-asof" type="date" [(ngModel)]="asOf" (ngModelChange)="onFilterChange()" class="form-input" />
          </div>
        </div>
        @if (report(); as r) {
          <div class="text-sm text-gray-600 min-w-0">
            <p class="font-medium text-gray-900 truncate">{{ r.header.schoolName || 'School' }}</p>
            <p class="text-xs text-gray-500">
              @if (r.header.schoolId) { School ID <span class="folio-mono">{{ r.header.schoolId }}</span> }
              @else {
                School ID not set.
                @if (isAdmin()) { <a routerLink="/settings" class="text-[#0038A8] hover:text-[#002B85] font-medium">Add it in Settings &rarr; School Profile</a> }
              }
            </p>
          </div>
        }
      </div>

      @if (loadFailed() && !report()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
          <p class="px-6 py-8 text-center text-red-600">The quick count couldn't be loaded. Refresh to try again.</p>
        </div>
      } @else if (loading() && !report()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]"><app-loading label="Loading quick count..." /></div>
      } @else {
       @if (report(); as r) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] min-w-0" [class.opacity-60]="loading()">
          <div class="px-4 sm:px-5 pt-4 pb-3 border-b border-gray-100">
            <p class="folio-eyebrow">As of {{ r.asOf | date:'longDate' }}</p>
            <p class="text-xs text-gray-500 mt-0.5">Small figures show the change compared with the day before.</p>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left border-b border-gray-200 bg-gray-50">
                  <th scope="col" class="px-4 sm:px-5 py-2 font-semibold text-gray-700 whitespace-nowrap">Grade</th>
                  <th scope="col" class="px-3 py-2 font-semibold text-gray-700 text-right whitespace-nowrap">Male</th>
                  <th scope="col" class="px-3 py-2 font-semibold text-gray-700 text-right whitespace-nowrap">Female</th>
                  <th scope="col" class="px-3 py-2 font-semibold text-gray-700 text-right whitespace-nowrap">Sex not set</th>
                  <th scope="col" class="px-4 sm:px-5 py-2 font-semibold text-gray-700 text-right whitespace-nowrap">Total</th>
                </tr>
              </thead>
              <tbody>
                @for (row of r.rows; track row.gradeLevel) {
                  <tr class="border-b border-gray-100">
                    <th scope="row" class="px-4 sm:px-5 py-2 text-left font-medium text-gray-900 whitespace-nowrap">{{ row.gradeLevel }}</th>
                    <td class="px-3 py-2 text-right"><ng-container *ngTemplateOutlet="cell; context: { n: row.male, c: row.maleChange }" /></td>
                    <td class="px-3 py-2 text-right"><ng-container *ngTemplateOutlet="cell; context: { n: row.female, c: row.femaleChange }" /></td>
                    <td class="px-3 py-2 text-right"><ng-container *ngTemplateOutlet="cell; context: { n: row.notSet, c: row.notSetChange }" /></td>
                    <td class="px-4 sm:px-5 py-2 text-right"><ng-container *ngTemplateOutlet="cell; context: { n: row.total, c: row.totalChange }" /></td>
                  </tr>
                } @empty {
                  <tr><td colspan="5" class="px-5 py-6 text-center text-gray-400">No enrolled learners as of this date.</td></tr>
                }
                <tr class="bg-gray-50 border-t-2 border-gray-300">
                  <th scope="row" class="px-4 sm:px-5 py-2 text-left font-bold text-gray-900">Total</th>
                  <td class="px-3 py-2 text-right font-bold"><ng-container *ngTemplateOutlet="cell; context: { n: r.total.male, c: r.total.maleChange }" /></td>
                  <td class="px-3 py-2 text-right font-bold"><ng-container *ngTemplateOutlet="cell; context: { n: r.total.female, c: r.total.femaleChange }" /></td>
                  <td class="px-3 py-2 text-right font-bold"><ng-container *ngTemplateOutlet="cell; context: { n: r.total.notSet, c: r.total.notSetChange }" /></td>
                  <td class="px-4 sm:px-5 py-2 text-right font-bold"><ng-container *ngTemplateOutlet="cell; context: { n: r.total.total, c: r.total.totalChange }" /></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        @if (r.notSetLearners.length > 0) {
          <div class="mt-6 rounded-xl border border-amber-300 bg-amber-50 p-4 sm:p-5">
            <p class="text-sm font-semibold text-amber-900">Sex not set ({{ r.notSetLearners.length }})</p>
            <p class="text-xs text-amber-800 mt-0.5">These learners are counted under "Sex not set". Open the student to record their sex.</p>
            <ul class="mt-3 divide-y divide-amber-200">
              @for (l of r.notSetLearners; track l.enrollmentId) {
                <li class="py-2 flex flex-wrap items-baseline justify-between gap-x-3">
                  <a [routerLink]="'/students/' + l.studentId" class="text-sm font-medium text-[#0038A8] hover:text-[#002B85]">{{ l.name }}</a>
                  <span class="text-xs text-amber-900">{{ l.gradeLevel }}</span>
                </li>
              }
            </ul>
          </div>
        }
       }
      }
    </div>

    <ng-template #cell let-n="n" let-c="c">
      <span class="folio-mono text-gray-900">{{ n }}</span>
      <span class="ml-1.5 text-xs folio-mono inline-block min-w-[2ch] text-left"
            [class.text-green-700]="c > 0" [class.text-red-600]="c < 0" [class.text-gray-400]="c === 0">{{ change(c) }}</span>
    </ng-template>
  `
})
export class QuickCountComponent implements OnInit {
  report = signal<QuickCount | null>(null);
  loading = signal(true);
  loadFailed = signal(false);
  exporting = signal(false);

  schoolYear = '';
  asOf = manilaToday();
  private requestSeq = 0;

  isAdmin = computed(() => this.auth.userRole() === 'Admin');

  constructor(
    private api: ApiService,
    public syService: SchoolYearService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private notify: NotificationService) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.asOf = qp.get('asOf') || manilaToday();
    this.syService.ensureLoaded().subscribe({
      next: () => {
        this.schoolYear = qp.get('schoolYear') ?? this.syService.activeName() ?? '';
        this.load();
      },
      // Year list unavailable: the server still resolves the active year (or explains why not).
      error: () => { this.schoolYear = qp.get('schoolYear') ?? ''; this.load(); }
    });
  }

  // "+2", "−1" (true minus sign), "0".
  change(n: number): string {
    return n > 0 ? `+${n}` : n < 0 ? `−${Math.abs(n)}` : '0';
  }

  onFilterChange(): void {
    // A cleared date input yields '' — fall back to today rather than asking for nothing.
    if (!this.asOf) this.asOf = manilaToday();
    this.syncUrl();
    this.load();
  }

  load(): void {
    const seq = ++this.requestSeq;
    this.loading.set(true);
    this.api.getQuickCount(this.schoolYear || undefined, this.asOf).subscribe({
      next: r => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(false);
        this.report.set(r);
      },
      error: (err) => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(true);
        this.report.set(null);
        this.notify.error(formatApiError(err, 'Failed to load the quick count.'));
      }
    });
  }

  print(): void {
    const params = new URLSearchParams({ asOf: this.asOf });
    if (this.schoolYear) params.set('schoolYear', this.schoolYear);
    window.open(`/print/quick-count?${params.toString()}`, '_blank');
  }

  exportCsv(): void {
    if (this.exporting()) return;
    this.exporting.set(true);
    this.api.exportQuickCount(this.schoolYear || undefined, this.asOf).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `quick-count_${this.asOf.replaceAll('-', '')}.csv`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => {
        this.exporting.set(false);
        this.notify.error(formatApiError(err, 'Failed to export CSV.'));
      }
    });
  }

  private syncUrl(): void {
    const q: Record<string, string> = { asOf: this.asOf };
    if (this.schoolYear) q['schoolYear'] = this.schoolYear;
    this.router.navigate([], { relativeTo: this.route, queryParams: q, replaceUrl: true });
  }
}
