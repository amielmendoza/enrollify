import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { EnrollmentRegister, EnrollmentRegisterFilters, PublicTenant } from '../../core/models';
import { formatApiError } from '../../core/utils/format-api-error';

// Printable enrollment register. Top-level (outside MainLayout) like the other print pages so
// no sidebar/nav prints. URL: /print/enrollment-register?schoolYear&gradeLevel&status&sectionId&includeCancelled
// Fetches up to 500 rows; larger registers get a visible truncation notice (never silent).
@Component({
  selector: 'app-print-enrollment-register',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    .document { font-family: 'Public Sans', ui-sans-serif, system-ui, sans-serif; }
    .doc-title { font-family: 'Archivo', ui-sans-serif, system-ui, sans-serif; }
    @media print {
      .no-print { display: none !important; }
      .print-root { background: white !important; }
      .document {
        border: none !important;
        box-shadow: none !important;
        margin: 0 auto !important;
        max-width: 100% !important;
      }
    }
  `],
  template: `
    <div class="min-h-screen bg-gray-100 print-root">
      <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">Enrollment Register</span></p>
        <div class="flex gap-2">
          <button (click)="print()" class="btn btn-primary">Print</button>
          <button (click)="close()" class="btn btn-secondary">Close</button>
        </div>
      </div>

      @if (error()) {
        <div class="no-print max-w-[800px] mx-auto mt-8 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (report(); as r) {
        <div class="document mx-auto my-8 max-w-[800px] bg-white border border-[#E2D9C2] shadow-sm p-12">
          <div class="text-center border-b-2 border-gray-900 pb-5">
            <h1 class="text-2xl font-bold text-gray-900">{{ tenant()?.name || '' }}</h1>
            <p class="doc-title mt-2 text-base font-semibold uppercase tracking-[0.25em] text-gray-800">Enrollment Register</p>
            <p class="mt-1 text-sm text-gray-500">
              School Year <span class="folio-mono">{{ filters.schoolYear || 'active' }}</span>
              @if (filters.gradeLevel) { · {{ filters.gradeLevel }} }
              @if (filters.sectionId && r.items.length > 0) { · {{ r.items[0].sectionName }} }
              @if (filters.status) { · {{ filters.status }} only }
              @if (filters.includeCancelled) { · including cancelled }
            </p>
          </div>

          @if (truncated()) {
            <div class="mt-6 rounded border border-amber-400 bg-amber-50 p-3 text-sm text-amber-800">
              Showing first {{ r.items.length }} of {{ r.totalCount }} enrollments — filter by grade or section for a complete printout.
            </div>
          }

          <table class="w-full mt-8 text-sm">
            <thead>
              <tr class="border-b-2 border-gray-900 text-left">
                <th class="py-2 pr-2 font-semibold text-gray-900 w-10">No.</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Student No.</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">LRN</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Student</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Grade</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Section</th>
                <th class="py-2 font-semibold text-gray-900">Status</th>
              </tr>
            </thead>
            <tbody>
              @for (row of r.items; track row.enrollmentId; let i = $index) {
                <tr class="border-b border-gray-100">
                  <td class="py-1.5 pr-2 text-gray-500 folio-mono">{{ i + 1 }}</td>
                  <td class="py-1.5 pr-2 text-gray-700 folio-mono whitespace-nowrap">{{ row.studentNumber }}</td>
                  <td class="py-1.5 pr-2 text-gray-700 folio-mono whitespace-nowrap">{{ row.lrn || '—' }}</td>
                  <td class="py-1.5 pr-2 text-gray-900">{{ row.studentName }}</td>
                  <td class="py-1.5 pr-2 text-gray-700 whitespace-nowrap">{{ row.gradeLevel }}</td>
                  <td class="py-1.5 pr-2 text-gray-700">{{ row.sectionName || '—' }}</td>
                  <td class="py-1.5 text-gray-700">{{ row.status }}</td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="py-4 text-center text-gray-400">No enrollments match these filters.</td></tr>
              }
            </tbody>
          </table>

          @if (r.summary.byGrade.length > 0) {
            <div class="mt-8 grid grid-cols-2 gap-8 text-sm">
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-500 mb-2">By Grade</p>
                <table class="w-full">
                  <tbody>
                    @for (g of r.summary.byGrade; track g.gradeLevel) {
                      <tr class="border-b border-gray-100"><td class="py-1 text-gray-700">{{ g.gradeLevel }}</td><td class="py-1 text-right folio-mono text-gray-900">{{ g.count }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
              <div>
                <p class="text-xs font-semibold uppercase tracking-wider text-gray-500 mb-2">By Status</p>
                <table class="w-full">
                  <tbody>
                    @for (s of r.summary.byStatus; track s.status) {
                      <tr class="border-b border-gray-100"><td class="py-1 text-gray-700">{{ s.status }}</td><td class="py-1 text-right folio-mono text-gray-900">{{ s.count }}</td></tr>
                    }
                    <tr class="border-t border-gray-900"><td class="py-1 font-bold text-gray-900">Total</td><td class="py-1 text-right font-bold folio-mono text-gray-900">{{ r.summary.totalCount }}</td></tr>
                  </tbody>
                </table>
              </div>
            </div>
          }

          <div class="grid grid-cols-2 mt-20">
            <div></div>
            <div class="text-center">
              <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">Registrar</div>
              <p class="text-xs text-gray-500 mt-0.5">Signature over printed name</p>
            </div>
          </div>

          <p class="mt-10 text-xs text-gray-400">Date printed: <span class="folio-mono">{{ today | date:'medium' }}</span></p>
        </div>
      }
    </div>
  `
})
export class PrintEnrollmentRegisterComponent implements OnInit {
  report = signal<EnrollmentRegister | null>(null);
  tenant = signal<PublicTenant | null>(null);
  error = signal('');
  filters: EnrollmentRegisterFilters = {};
  today = new Date();

  private tenantLoaded = signal(false);
  private printed = false;

  truncated = computed(() => {
    const r = this.report();
    return !!r && r.items.length < r.totalCount;
  });

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.filters = {
      schoolYear: qp.get('schoolYear') ?? undefined,
      gradeLevel: qp.get('gradeLevel') ?? undefined,
      status: qp.get('status') ?? undefined,
      sectionId: qp.get('sectionId') ?? undefined,
      includeCancelled: qp.get('includeCancelled') === 'true',
    };

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without header name rather than never
    });

    this.api.getEnrollmentRegister(this.filters, 1, 500).subscribe({
      next: r => { this.report.set(r); this.maybeAutoPrint(); },
      error: (err) => this.error.set(formatApiError(err, 'Failed to load the enrollment register.'))
    });
  }

  print(): void { window.print(); }

  close(): void { window.close(); }

  private maybeAutoPrint(): void {
    if (this.printed) return;
    if (!this.report() || !this.tenantLoaded()) return;
    this.printed = true;
    setTimeout(() => window.print(), 400);
  }
}
