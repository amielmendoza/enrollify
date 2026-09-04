import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { CollectionsReport, CollectionRow, PublicTenant } from '../../core/models';

interface DayGroup {
  key: string;
  date: string;
  amount: number;
  count: number;
  rows: CollectionRow[];
}

// Printable cashier's collections journal. Top-level (outside MainLayout) like
// print-enrollment so no sidebar/nav prints. URL: /print/collections?from&to&method
// Fetches up to 500 rows; larger ranges get a visible truncation notice (never silent).
@Component({
  selector: 'app-print-collections',
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
      <!-- Toolbar (never printed) -->
      <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">Collections Journal</span></p>
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
          <!-- School header -->
          <div class="text-center border-b-2 border-gray-900 pb-5">
            <h1 class="text-2xl font-bold text-gray-900">{{ tenant()?.name || '' }}</h1>
            <p class="doc-title mt-2 text-base font-semibold uppercase tracking-[0.25em] text-gray-800">Collections Journal</p>
            <p class="mt-1 text-sm text-gray-500">
              From {{ from | date:'mediumDate' }} to {{ to | date:'mediumDate' }}
              @if (method) { · {{ method }} only }
            </p>
          </div>

          @if (truncated()) {
            <div class="mt-6 rounded border border-amber-400 bg-amber-50 p-3 text-sm text-amber-800">
              Showing first {{ report()!.rows.length }} of {{ report()!.totalCount }} entries — narrow the date range for a complete printout.
            </div>
          }

          <table class="w-full mt-8 text-sm">
            <thead>
              <tr class="border-b-2 border-gray-900 text-left">
                <th class="py-2 font-semibold text-gray-900">Time</th>
                <th class="py-2 font-semibold text-gray-900">Student</th>
                <th class="py-2 font-semibold text-gray-900">Grade</th>
                <th class="py-2 font-semibold text-gray-900">Method</th>
                <th class="py-2 font-semibold text-gray-900">Ref No</th>
                <th class="py-2 font-semibold text-gray-900">Received By</th>
                <th class="py-2 font-semibold text-gray-900 text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              @for (g of groups(); track g.key) {
                <tr class="border-b border-gray-300 bg-gray-50">
                  <td colspan="6" class="py-1.5 text-xs font-semibold uppercase tracking-wider text-gray-600">{{ g.date | date:'EEEE, MMM d, y' }}</td>
                  <td class="py-1.5 text-right text-xs font-semibold text-gray-700 folio-mono">₱{{ g.amount | number:'1.2-2' }}</td>
                </tr>
                @for (row of g.rows; track row.paymentId) {
                  <tr class="border-b border-gray-100">
                    <td class="py-1.5 whitespace-nowrap text-gray-700 folio-mono">{{ row.paymentDate | date:'h:mm a' }}</td>
                    <td class="py-1.5 text-gray-900">{{ row.studentName }}</td>
                    <td class="py-1.5 text-gray-700">{{ row.gradeLevel }}</td>
                    <td class="py-1.5 text-gray-700">{{ row.paymentMethod }}</td>
                    <td class="py-1.5 text-gray-700 folio-mono">{{ row.referenceNumber || '—' }}</td>
                    <td class="py-1.5 text-gray-700">{{ row.receivedBy || '—' }}</td>
                    <td class="py-1.5 text-right text-gray-900 folio-mono">₱{{ row.amount | number:'1.2-2' }}</td>
                  </tr>
                }
              }
              @empty {
                <tr><td colspan="7" class="py-4 text-center text-gray-400">No collections in this period.</td></tr>
              }
            </tbody>
            <tfoot>
              <tr class="border-t-2 border-gray-900">
                <td colspan="6" class="py-2.5 font-bold text-gray-900">Grand Total</td>
                <td class="py-2.5 text-right font-bold text-gray-900 folio-mono">₱{{ r.summary.totalAmount | number:'1.2-2' }}</td>
              </tr>
            </tfoot>
          </table>

          <!-- Per-method summary -->
          @if (r.summary.byMethod.length > 0) {
            <div class="mt-8">
              <p class="text-xs font-semibold uppercase tracking-wider text-gray-500 mb-2">Summary by Method</p>
              <table class="w-1/2 text-sm">
                <tbody>
                  @for (m of r.summary.byMethod; track m.method) {
                    <tr class="border-b border-gray-100">
                      <td class="py-1.5 text-gray-700">{{ m.method }} <span class="text-gray-400">({{ m.count }})</span></td>
                      <td class="py-1.5 text-right text-gray-900 folio-mono">₱{{ m.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                  <tr class="border-t border-gray-900">
                    <td class="py-1.5 font-bold text-gray-900">Total ({{ r.summary.totalCount }})</td>
                    <td class="py-1.5 text-right font-bold text-gray-900 folio-mono">₱{{ r.summary.totalAmount | number:'1.2-2' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          }

          <!-- Signature -->
          <div class="grid grid-cols-2 mt-20">
            <div></div>
            <div class="text-center">
              <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">Prepared by / Cashier</div>
              <p class="text-xs text-gray-500 mt-0.5">Signature over printed name</p>
            </div>
          </div>

          <p class="mt-10 text-xs text-gray-400">Date printed: <span class="folio-mono">{{ today | date:'medium' }}</span></p>
        </div>
      }
    </div>
  `
})
export class PrintCollectionsComponent implements OnInit {
  report = signal<CollectionsReport | null>(null);
  tenant = signal<PublicTenant | null>(null);
  error = signal('');

  from = '';
  to = '';
  method = '';
  today = new Date();

  private tenantLoaded = signal(false);
  private printed = false;

  // Data-derived, not a magic number: whatever page size the server actually honored,
  // the notice renders (and prints) whenever the returned rows don't cover the range.
  truncated = computed(() => {
    const r = this.report();
    return !!r && r.rows.length < r.totalCount;
  });

  groups = computed<DayGroup[]>(() => {
    const rep = this.report();
    if (!rep) return [];
    const byDay = new Map(rep.summary.byDay.map(d => [this.dateKey(d.date), d]));
    const groups: DayGroup[] = [];
    for (const row of rep.rows) {
      const key = this.dateKey(row.paymentDate);
      let g = groups.length > 0 && groups[groups.length - 1].key === key ? groups[groups.length - 1] : undefined;
      if (!g) {
        const day = byDay.get(key);
        g = { key, date: row.paymentDate, amount: day?.amount ?? 0, count: day?.count ?? 0, rows: [] };
        groups.push(g);
      }
      g.rows.push(row);
    }
    return groups;
  });

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.from = qp.get('from') ?? '';
    this.to = qp.get('to') ?? '';
    this.method = qp.get('method') ?? '';

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without header name rather than never
    });

    if (!this.from || !this.to) {
      this.error.set('Missing date range — open this page from the Collections report.');
      return;
    }

    this.api.getCollections({ from: this.from, to: this.to, method: this.method || undefined, page: 1, pageSize: 500 }).subscribe({
      next: r => { this.report.set(r); this.maybeAutoPrint(); },
      error: (err) => this.error.set(err.error?.error || 'Failed to load the collections report.')
    });
  }

  print(): void { window.print(); }

  close(): void { window.close(); }

  private maybeAutoPrint(): void {
    if (this.printed) return;
    if (!this.report() || !this.tenantLoaded()) return;
    this.printed = true;
    // Give Angular a tick to render before the print dialog snapshots the page.
    setTimeout(() => window.print(), 400);
  }

  private dateKey(iso: string): string {
    return (iso || '').slice(0, 10);
  }
}
