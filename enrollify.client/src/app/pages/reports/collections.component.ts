import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { NotificationService } from '../../core/services/notification.service';
import { CollectionsReport, CollectionRow } from '../../core/models';

type Preset = 'today' | 'week' | 'month' | null;

interface DayGroup {
  key: string;           // YYYY-MM-DD
  date: string;          // representative date for the pipe
  amount: number;        // whole-range subtotal for the day (from summary.byDay)
  count: number;
  rows: CollectionRow[];
}

// Cashier's collections journal: date-range + method filtered list of approved
// payments, grouped by day, with CSV export and a printable journal.
@Component({
  selector: 'app-collections',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div>
      <div class="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Collections</h1>
          <p class="mt-1 text-sm text-gray-500">Cashier's journal of received payments</p>
        </div>
        <div class="flex gap-2">
          <button (click)="exportCsv()" [disabled]="exporting()"
                  class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {{ exporting() ? 'Exporting...' : 'Export CSV' }}
          </button>
          <button (click)="printJournal()"
                  class="rounded-lg bg-[#4361ee] px-4 py-2 text-sm font-semibold text-white hover:bg-[#3a56d4]">
            Print Journal
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="mt-6 bg-white rounded-xl border border-gray-200 p-4 flex flex-wrap items-end gap-3">
        <div>
          <label class="form-label">From</label>
          <input type="date" [(ngModel)]="from" (ngModelChange)="onDatesChanged()" class="form-input" />
        </div>
        <div>
          <label class="form-label">To</label>
          <input type="date" [(ngModel)]="to" (ngModelChange)="onDatesChanged()" class="form-input" />
        </div>
        <div class="flex gap-1">
          <button (click)="setPreset('today')" class="px-3 py-2 rounded-lg text-xs font-semibold border transition-colors"
                  [class]="activePreset() === 'today' ? 'border-[#4361ee] bg-blue-50 text-[#4361ee]' : 'border-gray-200 text-gray-600 hover:bg-gray-50'">Today</button>
          <button (click)="setPreset('week')" class="px-3 py-2 rounded-lg text-xs font-semibold border transition-colors"
                  [class]="activePreset() === 'week' ? 'border-[#4361ee] bg-blue-50 text-[#4361ee]' : 'border-gray-200 text-gray-600 hover:bg-gray-50'">This Week</button>
          <button (click)="setPreset('month')" class="px-3 py-2 rounded-lg text-xs font-semibold border transition-colors"
                  [class]="activePreset() === 'month' ? 'border-[#4361ee] bg-blue-50 text-[#4361ee]' : 'border-gray-200 text-gray-600 hover:bg-gray-50'">This Month</button>
        </div>
        <div>
          <label class="form-label">Method</label>
          <select [(ngModel)]="method" (ngModelChange)="onFilterChange()" class="form-input w-auto">
            <option value="">All Methods</option>
            @for (m of methods; track m) { <option [value]="m">{{ m }}</option> }
          </select>
        </div>
      </div>

      <!-- Summary tiles -->
      @if (report(); as r) {
        <div class="mt-6 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div class="bg-white rounded-xl border border-gray-200 p-5">
            <p class="text-sm text-gray-500">Total Collected</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">₱{{ r.summary.totalAmount | number:'1.2-2' }}</p>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-5">
            <p class="text-sm text-gray-500">Transactions</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ r.summary.totalCount }}</p>
          </div>
          <div class="sm:col-span-2 bg-white rounded-xl border border-gray-200 p-5">
            <p class="text-sm text-gray-500 mb-2">By Method</p>
            <div class="flex flex-wrap gap-2">
              @for (m of r.summary.byMethod; track m.method) {
                <span class="inline-flex items-center gap-2 rounded-full bg-gray-50 border border-gray-200 px-3 py-1.5 text-xs">
                  <span class="font-medium text-gray-700">{{ m.method }}</span>
                  <span class="font-semibold text-gray-900">₱{{ m.amount | number:'1.2-2' }}</span>
                  <span class="text-gray-400">({{ m.count }})</span>
                </span>
              }
              @empty { <span class="text-xs text-gray-400">No payments in this period</span> }
            </div>
          </div>
        </div>
      }

      <!-- Journal table -->
      <div class="mt-6 bg-white rounded-xl border border-gray-200">
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date / Time</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Student</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Method</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Ref No</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Received By</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (g of groupedRows(); track g.key) {
                <tr class="bg-gray-50/70">
                  <td colspan="6" class="px-6 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500">{{ g.date | date:'EEEE, MMM d, y' }}</td>
                  <td class="px-6 py-2 text-right text-xs font-semibold text-gray-600">₱{{ g.amount | number:'1.2-2' }} <span class="text-gray-400 font-normal">({{ g.count }})</span></td>
                </tr>
                @for (row of g.rows; track row.paymentId) {
                  <tr class="hover:bg-gray-50">
                    <td class="px-6 py-3 text-sm text-gray-500 whitespace-nowrap">{{ row.paymentDate | date:'MMM d, h:mm a' }}</td>
                    <td class="px-6 py-3 text-sm">
                      <a [routerLink]="['/enrollments', row.enrollmentId]" class="font-medium text-[#4361ee] hover:text-[#3a56d4]">{{ row.studentName }}</a>
                    </td>
                    <td class="px-6 py-3 text-sm text-gray-600">{{ row.gradeLevel }}</td>
                    <td class="px-6 py-3 text-sm text-gray-600">{{ row.paymentMethod }}</td>
                    <td class="px-6 py-3 text-sm font-mono text-gray-500">{{ row.referenceNumber || '-' }}</td>
                    <td class="px-6 py-3 text-sm text-gray-600">{{ row.receivedBy || '-' }}</td>
                    <td class="px-6 py-3 text-sm text-right font-semibold text-gray-900">₱{{ row.amount | number:'1.2-2' }}</td>
                  </tr>
                }
              }
              @empty {
                <tr><td colspan="7" class="px-6 py-8 text-center text-gray-400">No collections in this period.</td></tr>
              }
            </tbody>
            @if (report() && report()!.rows.length > 0) {
              <tfoot class="bg-gray-50">
                <tr>
                  <td colspan="6" class="px-6 py-3 text-sm font-bold text-gray-900">Grand Total (period)</td>
                  <td class="px-6 py-3 text-sm text-right font-bold text-gray-900">₱{{ report()!.summary.totalAmount | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            }
          </table>
        </div>

        @if (report() && report()!.totalPages > 1) {
          <div class="flex items-center justify-between p-4 border-t border-gray-200">
            <p class="text-sm text-gray-500">Showing page {{ page() }} of {{ report()!.totalPages }} ({{ report()!.totalCount }} total)</p>
            <div class="flex gap-2">
              <button (click)="loadPage(page() - 1)" [disabled]="page() <= 1"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Previous</button>
              <button (click)="loadPage(page() + 1)" [disabled]="page() >= report()!.totalPages"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Next</button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class CollectionsComponent implements OnInit {
  report = signal<CollectionsReport | null>(null);
  from = '';
  to = '';
  method = '';
  page = signal(1);
  exporting = signal(false);
  activePreset = signal<Preset>('today');

  methods = ['Cash', 'Bank Transfer', 'GCash', 'Maya'];

  // Group the current page's rows by calendar day; day subtotals come from
  // summary.byDay so the header shows the whole day even when paging splits it.
  groupedRows = computed<DayGroup[]>(() => {
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

  constructor(private api: ApiService, private notify: NotificationService) {}

  ngOnInit(): void {
    this.setPreset('today');
  }

  setPreset(p: Exclude<Preset, null>): void {
    const now = new Date();
    const today = this.fmt(now);
    if (p === 'today') {
      this.from = today;
      this.to = today;
    } else if (p === 'week') {
      const monday = new Date(now);
      monday.setDate(monday.getDate() - ((monday.getDay() + 6) % 7));
      this.from = this.fmt(monday);
      this.to = today;
    } else {
      this.from = this.fmt(new Date(now.getFullYear(), now.getMonth(), 1));
      this.to = today;
    }
    this.activePreset.set(p);
    this.onFilterChange();
  }

  onDatesChanged(): void {
    this.activePreset.set(null);
    this.onFilterChange();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  loadPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  load(): void {
    if (!this.from || !this.to) return;
    this.api.getCollections({ from: this.from, to: this.to, method: this.method || undefined, page: this.page() }).subscribe({
      next: r => this.report.set(r),
      error: (err) => {
        this.report.set(null);
        this.notify.error(err.error?.error || 'Failed to load collections.');
      }
    });
  }

  exportCsv(): void {
    this.exporting.set(true);
    this.api.exportCollections(this.from, this.to, this.method || undefined).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `collections_${this.from}_${this.to}.csv`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: () => {
        this.exporting.set(false);
        this.notify.error('Failed to export CSV.');
      }
    });
  }

  printJournal(): void {
    const params = new URLSearchParams({ from: this.from, to: this.to });
    if (this.method) params.set('method', this.method);
    window.open(`/print/collections?${params.toString()}`, '_blank');
  }

  private dateKey(iso: string): string {
    return (iso || '').slice(0, 10);
  }

  private fmt(d: Date): string {
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${m}-${day}`;
  }
}
