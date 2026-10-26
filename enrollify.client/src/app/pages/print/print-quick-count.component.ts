import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { PublicTenant, QuickCount } from '../../core/models';
import { formatApiError } from '../../core/utils/format-api-error';

// Printable DepEd quick count. Top-level (outside MainLayout) like the other print pages so no
// sidebar/nav prints. URL: /print/quick-count?schoolYear&asOf
@Component({
  selector: 'app-print-quick-count',
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
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">Quick Count</span></p>
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
            <h1 class="text-2xl font-bold text-gray-900">{{ r.header.schoolName || tenant()?.name || '' }}</h1>
            <p class="text-xs text-gray-600 mt-1">
              @if (r.header.schoolId) { School ID <span class="folio-mono">{{ r.header.schoolId }}</span> }
              @if (r.header.schoolId && r.header.division) { · }
              @if (r.header.division) { {{ r.header.division }} }
            </p>
            <p class="doc-title mt-2 text-base font-semibold uppercase tracking-[0.25em] text-gray-800">Quick Count</p>
            <p class="mt-1 text-sm text-gray-500">
              School Year <span class="folio-mono">{{ r.header.schoolYear }}</span>
              · As of <span class="folio-mono">{{ r.asOf | date:'longDate' }}</span>
            </p>
          </div>

          <table class="w-full mt-8 text-sm">
            <thead>
              <tr class="border-b-2 border-gray-900 text-left">
                <th class="py-2 pr-2 font-semibold text-gray-900">Grade</th>
                <th class="py-2 px-2 font-semibold text-gray-900 text-right">Male</th>
                <th class="py-2 px-2 font-semibold text-gray-900 text-right">Female</th>
                <th class="py-2 px-2 font-semibold text-gray-900 text-right">Sex not set</th>
                <th class="py-2 pl-2 font-semibold text-gray-900 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (row of r.rows; track row.gradeLevel) {
                <tr class="border-b border-gray-100">
                  <td class="py-1.5 pr-2 text-gray-900 whitespace-nowrap">{{ row.gradeLevel }}</td>
                  <td class="py-1.5 px-2 text-right"><span class="folio-mono text-gray-900">{{ row.male }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(row.maleChange) }}</span></td>
                  <td class="py-1.5 px-2 text-right"><span class="folio-mono text-gray-900">{{ row.female }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(row.femaleChange) }}</span></td>
                  <td class="py-1.5 px-2 text-right"><span class="folio-mono text-gray-900">{{ row.notSet }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(row.notSetChange) }}</span></td>
                  <td class="py-1.5 pl-2 text-right"><span class="folio-mono text-gray-900">{{ row.total }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(row.totalChange) }}</span></td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="py-4 text-center text-gray-400">No enrolled learners as of this date.</td></tr>
              }
              <tr class="border-t-2 border-gray-900">
                <td class="py-2 pr-2 font-bold text-gray-900">Total</td>
                <td class="py-2 px-2 text-right"><span class="folio-mono font-bold text-gray-900">{{ r.total.male }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(r.total.maleChange) }}</span></td>
                <td class="py-2 px-2 text-right"><span class="folio-mono font-bold text-gray-900">{{ r.total.female }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(r.total.femaleChange) }}</span></td>
                <td class="py-2 px-2 text-right"><span class="folio-mono font-bold text-gray-900">{{ r.total.notSet }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(r.total.notSetChange) }}</span></td>
                <td class="py-2 pl-2 text-right"><span class="folio-mono font-bold text-gray-900">{{ r.total.total }}</span> <span class="folio-mono text-xs text-gray-500">{{ change(r.total.totalChange) }}</span></td>
              </tr>
            </tbody>
          </table>
          <p class="mt-2 text-xs text-gray-500">Smaller figures show the change since the day before.</p>

          @if (r.notSetLearners.length > 0) {
            <div class="mt-8 text-sm">
              <p class="text-xs font-semibold uppercase tracking-wider text-gray-500 mb-2">Sex not set ({{ r.notSetLearners.length }})</p>
              <table class="w-full">
                <tbody>
                  @for (l of r.notSetLearners; track l.enrollmentId) {
                    <tr class="border-b border-gray-100">
                      <td class="py-1 text-gray-900">{{ l.name }}</td>
                      <td class="py-1 text-right text-gray-700">{{ l.gradeLevel }}</td>
                    </tr>
                  }
                </tbody>
              </table>
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
export class PrintQuickCountComponent implements OnInit {
  report = signal<QuickCount | null>(null);
  tenant = signal<PublicTenant | null>(null);
  error = signal('');
  today = new Date();

  private tenantLoaded = signal(false);
  private printed = false;

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without tenant name rather than never
    });

    this.api.getQuickCount(qp.get('schoolYear') ?? undefined, qp.get('asOf') ?? undefined).subscribe({
      next: r => { this.report.set(r); this.maybeAutoPrint(); },
      error: (err) => this.error.set(formatApiError(err, 'Failed to load the quick count.'))
    });
  }

  // "+2", "−1" (true minus sign), "0".
  change(n: number): string {
    return n > 0 ? `+${n}` : n < 0 ? `−${Math.abs(n)}` : '0';
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
