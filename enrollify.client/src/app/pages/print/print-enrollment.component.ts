import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { Enrollment, FeeLine, BalanceInfo, Payment, PublicTenant, EnrollmentLedger } from '../../core/models';
import { ENROLLMENT_STATUS_NAMES } from '../../core/constants';

type PrintDoc = 'cor' | 'assessment' | 'receipt' | 'soa';

// Printable documents for an enrollment: Certificate of Registration, Assessment Slip,
// Official Receipt, Statement of Account. Lives OUTSIDE MainLayout so no sidebar/nav
// ends up on paper. URL: /print/enrollment/:id?doc=cor|assessment|receipt|soa&paymentId=...
// Auto-triggers window.print() once all data for the requested document has loaded.
@Component({
  selector: 'app-print-enrollment',
  standalone: true,
  imports: [CommonModule],
  styles: [`
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
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">{{ docTitle() }}</span></p>
        <div class="flex gap-2">
          <button (click)="print()" class="btn btn-primary">Print</button>
          <button (click)="close()" class="btn btn-secondary">Close</button>
        </div>
      </div>

      @if (error()) {
        <div class="no-print max-w-[800px] mx-auto mt-8 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (enrollment(); as e) {
        <div class="document mx-auto my-8 max-w-[800px] bg-white border border-gray-200 shadow-sm p-12">
          <!-- School header -->
          <div class="text-center border-b-2 border-gray-900 pb-5">
            <h1 class="text-2xl font-bold text-gray-900">{{ tenant()?.name || '' }}</h1>
            <p class="mt-2 text-base font-semibold uppercase tracking-[0.25em] text-gray-800">{{ docTitle() }}</p>
            <p class="mt-1 text-sm text-gray-500">School Year {{ e.schoolYear }}</p>
          </div>

          <!-- Certificate of Registration -->
          @if (doc === 'cor') {
            <div class="mt-8">
              <p class="text-sm leading-relaxed text-gray-700">
                This is to certify that the student named below is registered at
                {{ tenant()?.name || 'this school' }} for School Year {{ e.schoolYear }}
                with the following enrollment details:
              </p>
              <table class="w-full mt-6 text-sm">
                <tbody>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 w-48 text-gray-500">Student Name</td>
                    <td class="py-2.5 font-semibold text-gray-900">{{ e.studentName }}</td>
                  </tr>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">Grade Level</td>
                    <td class="py-2.5 font-medium text-gray-900">{{ e.gradeLevel }}</td>
                  </tr>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">Section</td>
                    <td class="py-2.5 font-medium text-gray-900">{{ e.sectionName || 'Not yet assigned' }}</td>
                  </tr>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">School Year</td>
                    <td class="py-2.5 font-medium text-gray-900">{{ e.schoolYear }}</td>
                  </tr>
                  <tr class="border-b border-gray-200">
                    <td class="py-2.5 text-gray-500">Enrollment Status</td>
                    <td class="py-2.5 font-medium text-gray-900">{{ statusName() }}</td>
                  </tr>
                </tbody>
              </table>

              <!-- Signature lines -->
              <div class="grid grid-cols-2 gap-16 mt-24">
                <div class="text-center">
                  <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">Registrar</div>
                  <p class="text-xs text-gray-500 mt-0.5">Signature over printed name</p>
                </div>
                <div class="text-center">
                  <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">School Head</div>
                  <p class="text-xs text-gray-500 mt-0.5">Signature over printed name</p>
                </div>
              </div>
            </div>
          }

          <!-- Assessment Slip -->
          @if (doc === 'assessment') {
            <div class="mt-8">
              <div class="grid grid-cols-2 gap-x-8 gap-y-3 text-sm">
                <div><p class="text-xs text-gray-500">Student</p><p class="font-semibold text-gray-900">{{ e.studentName }}</p></div>
                <div><p class="text-xs text-gray-500">Grade Level</p><p class="font-medium text-gray-900">{{ e.gradeLevel }}</p></div>
                <div><p class="text-xs text-gray-500">Payment Plan</p><p class="font-medium text-gray-900">{{ e.paymentPlan || 'Not yet selected' }}</p></div>
                <div><p class="text-xs text-gray-500">Enrollment Status</p><p class="font-medium text-gray-900">{{ statusName() }}</p></div>
              </div>

              <table class="w-full mt-8 text-sm">
                <thead>
                  <tr class="border-b-2 border-gray-900 text-left">
                    <th class="py-2 font-semibold text-gray-900">Fee</th>
                    <th class="py-2 font-semibold text-gray-900 text-right">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  @for (f of fees(); track $index) {
                    <tr class="border-b border-gray-100">
                      <td class="py-2 text-gray-900">{{ f.name }}@if (f.description) {<span class="text-gray-400"> — {{ f.description }}</span>}</td>
                      <td class="py-2 text-right text-gray-900">₱{{ f.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                  @empty {
                    <tr><td colspan="2" class="py-4 text-center text-gray-400">No fees configured for this grade level and school year.</td></tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="border-t-2 border-gray-900">
                    <td class="py-2.5 font-bold text-gray-900">Total</td>
                    <td class="py-2.5 text-right font-bold text-gray-900">₱{{ feesTotal() | number:'1.2-2' }}</td>
                  </tr>
                </tfoot>
              </table>

              @if (balance(); as b) {
                <div class="grid grid-cols-3 gap-4 mt-8 text-sm">
                  <div class="border border-gray-300 rounded p-3 text-center">
                    <p class="text-xs text-gray-500">Total Assessed</p>
                    <p class="font-bold text-gray-900 mt-1">₱{{ b.totalFees | number:'1.2-2' }}</p>
                  </div>
                  <div class="border border-gray-300 rounded p-3 text-center">
                    <p class="text-xs text-gray-500">Total Paid</p>
                    <p class="font-bold text-gray-900 mt-1">₱{{ b.totalPaid | number:'1.2-2' }}</p>
                  </div>
                  <div class="border border-gray-300 rounded p-3 text-center">
                    <p class="text-xs text-gray-500">Balance</p>
                    <p class="font-bold text-gray-900 mt-1">₱{{ b.balance | number:'1.2-2' }}</p>
                  </div>
                </div>
                <p class="mt-2 text-xs text-gray-400">Balance may include a payment-plan discount or installment interest, so it can differ from Total Assessed minus Total Paid.</p>
              }
            </div>
          }

          <!-- Official Receipt -->
          @if (doc === 'receipt') {
            @if (receiptPayment(); as p) {
              <div class="mt-8 text-sm">
                <div class="flex items-start justify-between">
                  <div>
                    <p class="text-xs text-gray-500">Received from</p>
                    <p class="font-semibold text-gray-900">{{ e.studentName }}</p>
                    <p class="text-xs text-gray-500 mt-0.5">{{ e.gradeLevel }} · {{ e.schoolYear }}</p>
                  </div>
                  <div class="text-right">
                    <p class="text-xs text-gray-500">Payment Date</p>
                    <p class="font-medium text-gray-900">{{ p.paymentDate | date:'mediumDate' }}</p>
                  </div>
                </div>

                <div class="mt-8 border-2 border-gray-900 rounded p-6 text-center">
                  <p class="text-xs uppercase tracking-wider text-gray-500">Amount Received</p>
                  <p class="mt-1 text-3xl font-bold text-gray-900">₱{{ p.amount | number:'1.2-2' }}</p>
                </div>

                <table class="w-full mt-8">
                  <tbody>
                    <tr class="border-b border-gray-200">
                      <td class="py-2.5 w-48 text-gray-500">Payment Method</td>
                      <td class="py-2.5 font-medium text-gray-900">{{ p.paymentMethod }}</td>
                    </tr>
                    <tr class="border-b border-gray-200">
                      <td class="py-2.5 text-gray-500">Reference Number</td>
                      <td class="py-2.5 font-medium text-gray-900">{{ p.referenceNumber || '—' }}</td>
                    </tr>
                    <tr class="border-b border-gray-200">
                      <td class="py-2.5 text-gray-500">Reviewed By</td>
                      <td class="py-2.5 font-medium text-gray-900">{{ p.reviewedBy || '—' }}</td>
                    </tr>
                    <tr class="border-b border-gray-200">
                      <td class="py-2.5 text-gray-500">Receipt / Payment ID</td>
                      <td class="py-2.5 font-mono text-xs text-gray-900">{{ p.id }}</td>
                    </tr>
                  </tbody>
                </table>

                <div class="grid grid-cols-2 mt-20">
                  <div></div>
                  <div class="text-center">
                    <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">Authorized Signature</div>
                  </div>
                </div>
              </div>
            } @else if (paymentsLoaded()) {
              <div class="mt-8 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">
                Receipt unavailable — receipts can only be printed for approved payments.
              </div>
            }
          }

          <!-- Statement of Account -->
          @if (doc === 'soa') {
            <div class="mt-8">
              <div class="grid grid-cols-2 gap-x-8 gap-y-3 text-sm">
                <div><p class="text-xs text-gray-500">Student</p><p class="font-semibold text-gray-900">{{ e.studentName }}</p></div>
                <div><p class="text-xs text-gray-500">Grade Level</p><p class="font-medium text-gray-900">{{ e.gradeLevel }}</p></div>
                <div><p class="text-xs text-gray-500">School Year</p><p class="font-medium text-gray-900">{{ e.schoolYear }}</p></div>
                <div><p class="text-xs text-gray-500">Payment Plan</p><p class="font-medium text-gray-900">{{ e.paymentPlan || 'Not yet selected' }}</p></div>
              </div>

              <table class="w-full mt-8 text-sm">
                <thead>
                  <tr class="border-b-2 border-gray-900 text-left">
                    <th class="py-2 font-semibold text-gray-900">Date</th>
                    <th class="py-2 font-semibold text-gray-900">Description</th>
                    <th class="py-2 font-semibold text-gray-900 text-right">Debit</th>
                    <th class="py-2 font-semibold text-gray-900 text-right">Credit</th>
                    <th class="py-2 font-semibold text-gray-900 text-right">Balance</th>
                  </tr>
                </thead>
                <tbody>
                  @for (en of soaEntries(); track $index) {
                    <tr class="border-b border-gray-100">
                      <td class="py-2 whitespace-nowrap text-gray-700">{{ en.date | date:'MM/dd/yyyy' }}</td>
                      <td class="py-2 text-gray-900">
                        {{ en.description }}
                        @if (en.reference) { <span class="text-gray-400"> — Ref: {{ en.reference }}</span> }
                      </td>
                      <td class="py-2 text-right text-gray-900">@if (en.debit != null) { ₱{{ en.debit | number:'1.2-2' }} }</td>
                      <td class="py-2 text-right text-gray-900">@if (en.credit != null) { ₱{{ en.credit | number:'1.2-2' }} }</td>
                      <td class="py-2 text-right text-gray-900">₱{{ en.balance | number:'1.2-2' }}</td>
                    </tr>
                  }
                  @empty {
                    <tr><td colspan="5" class="py-4 text-center text-gray-400">No ledger entries — the enrollment has not been assessed yet.</td></tr>
                  }
                </tbody>
                @if (ledger(); as l) {
                  <tfoot>
                    <tr class="border-t-2 border-gray-900">
                      <td colspan="2" class="py-2.5 font-bold text-gray-900">Totals</td>
                      <td class="py-2.5 text-right font-bold text-gray-900">₱{{ l.totalDebits | number:'1.2-2' }}</td>
                      <td class="py-2.5 text-right font-bold text-gray-900">₱{{ l.totalCredits | number:'1.2-2' }}</td>
                      <td class="py-2.5 text-right font-bold text-gray-900">₱{{ l.balance | number:'1.2-2' }}</td>
                    </tr>
                  </tfoot>
                }
              </table>

              @if (ledger(); as l) {
                <div class="mt-6 flex justify-end">
                  <div class="border border-gray-900 rounded px-6 py-3 text-right">
                    <p class="text-xs uppercase tracking-wider text-gray-500">Ending Balance</p>
                    <p class="mt-0.5 text-xl font-bold text-gray-900">₱{{ l.balance | number:'1.2-2' }}</p>
                  </div>
                </div>
              }

              <div class="grid grid-cols-2 mt-20">
                <div></div>
                <div class="text-center">
                  <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">Cashier / Registrar</div>
                  <p class="text-xs text-gray-500 mt-0.5">Signature over printed name</p>
                </div>
              </div>
            </div>
          }

          <p class="mt-10 text-xs text-gray-400">Date printed: {{ today | date:'medium' }} · Enrollment ID: {{ e.id }}</p>
        </div>
      }
    </div>
  `
})
export class PrintEnrollmentComponent implements OnInit {
  enrollment = signal<Enrollment | null>(null);
  tenant = signal<PublicTenant | null>(null);
  fees = signal<FeeLine[]>([]);
  balance = signal<BalanceInfo | null>(null);
  payments = signal<Payment[]>([]);
  ledger = signal<EnrollmentLedger | null>(null);
  error = signal('');

  // Loaded flags gate the one-shot auto-print (a legitimately empty list still counts as loaded).
  private tenantLoaded = signal(false);
  private feesLoaded = signal(false);
  private balanceLoaded = signal(false);
  private ledgerLoaded = signal(false);
  paymentsLoaded = signal(false);

  doc: PrintDoc = 'cor';
  today = new Date();
  private paymentId: string | null = null;
  private printed = false;

  feesTotal = computed(() => this.fees().reduce((sum, f) => sum + f.amount, 0));

  // Only Approved payments get an official receipt.
  receiptPayment = computed(() =>
    this.payments().find(p => p.id === this.paymentId && p.status === 'Approved') ?? null);

  // Voided adjustments are omitted from the printed statement.
  soaEntries = computed(() => (this.ledger()?.entries ?? []).filter(en => !en.voided));

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    const doc = this.route.snapshot.queryParamMap.get('doc');
    this.doc = doc === 'assessment' || doc === 'receipt' || doc === 'soa' ? doc : 'cor';
    this.paymentId = this.route.snapshot.queryParamMap.get('paymentId');

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without header name rather than never
    });

    this.api.getEnrollment(id).subscribe({
      next: e => {
        this.enrollment.set(e);
        this.maybeAutoPrint();
      },
      error: (err) => this.error.set(err.error?.error || 'Failed to load enrollment.')
    });

    if (this.doc === 'assessment') {
      // Snapshot fee lines (not the live catalog) so the printed document can't
      // contradict its own totals after a later fee-catalog edit.
      this.api.getEnrollmentFees(id).subscribe({
        next: f => { this.fees.set(f); this.feesLoaded.set(true); this.maybeAutoPrint(); },
        error: () => { this.feesLoaded.set(true); this.maybeAutoPrint(); }
      });
      this.api.getBalance(id).subscribe({
        next: b => { this.balance.set(b); this.balanceLoaded.set(true); this.maybeAutoPrint(); },
        error: () => { this.balanceLoaded.set(true); this.maybeAutoPrint(); }
      });
    }
    if (this.doc === 'receipt') {
      this.api.getPayments(id).subscribe({
        next: p => { this.payments.set(p); this.paymentsLoaded.set(true); this.maybeAutoPrint(); },
        error: () => { this.paymentsLoaded.set(true); this.maybeAutoPrint(); }
      });
    }
    if (this.doc === 'soa') {
      this.api.getEnrollmentLedger(id).subscribe({
        next: l => { this.ledger.set(l); this.ledgerLoaded.set(true); this.maybeAutoPrint(); },
        error: () => { this.ledgerLoaded.set(true); this.maybeAutoPrint(); }
      });
    }
  }

  docTitle(): string {
    switch (this.doc) {
      case 'assessment': return 'Assessment Slip';
      case 'receipt': return 'Official Receipt';
      case 'soa': return 'Statement of Account';
      default: return 'Certificate of Registration';
    }
  }

  statusName(): string {
    const status = this.enrollment()?.status;
    if (status == null) return 'Draft';
    if (typeof status === 'number') return ENROLLMENT_STATUS_NAMES[status] ?? 'Draft';
    return status;
  }

  print(): void { window.print(); }

  close(): void { window.close(); }

  private maybeAutoPrint(): void {
    if (this.printed) return;
    if (!this.enrollment() || !this.tenantLoaded()) return;
    if (this.doc === 'assessment' && (!this.feesLoaded() || !this.balanceLoaded())) return;
    if (this.doc === 'soa' && !this.ledgerLoaded()) return;
    if (this.doc === 'receipt') {
      if (!this.paymentsLoaded()) return;
      if (!this.receiptPayment()) return;  // nothing printable; leave the unavailable notice on screen
    }
    this.printed = true;
    // Give Angular a tick to render the freshly-set signals before the print dialog snapshots the page.
    setTimeout(() => window.print(), 400);
  }
}
