import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Enrollment, Section, BalanceInfo, Payment } from '../../../core/models';

@Component({
  selector: 'app-enrollment-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-4xl">
      @if (enrollment()) {
        <div class="flex items-center justify-between mb-6">
          <div>
            <h2 class="text-2xl font-bold text-gray-900">{{ enrollment()!.studentName }}</h2>
            <p class="text-sm text-gray-500">{{ enrollment()!.schoolYear }} - {{ enrollment()!.gradeLevel }}</p>
          </div>
          <span class="inline-flex items-center rounded-full px-3 py-1 text-sm font-medium" [ngClass]="getStatusClass(getStatusName())">
            {{ getStatusName() }}
          </span>
        </div>

        <!-- Workflow Progress -->
        <div class="bg-white rounded-xl border border-gray-200 p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Enrollment Progress</h3>
          <div class="flex items-center justify-between">
            @for (s of statuses; track s; let i = $index) {
              <div class="flex-1 flex flex-col items-center">
                @if (getStepIndex() > i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold bg-emerald-500 text-white shadow-sm">&#10003;</div>
                } @else if (getStepIndex() === i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold text-white shadow-sm" style="background-color: #4361ee;">{{ i + 1 }}</div>
                } @else {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold bg-gray-100 text-gray-400 border border-gray-200">{{ i + 1 }}</div>
                }
                <span class="text-xs mt-2 text-center" [class]="getStepIndex() >= i ? 'text-gray-900 font-medium' : 'text-gray-400'">{{ s }}</span>
              </div>
              @if (i < statuses.length - 1) {
                <div class="flex-1 h-0.5 rounded-full mx-1" [class]="getStepIndex() > i ? 'bg-emerald-400' : 'bg-gray-200'"></div>
              }
            }
          </div>

          @if (getStatusName() === 'Submitted') {
            <div class="mt-4 p-3 bg-blue-50 rounded-lg text-sm text-blue-700">Review the student's uploaded requirements below, then advance to mark as Assessed.</div>
            <div class="mt-3"><button (click)="moveStep()" class="btn btn-primary">Mark as Assessed</button></div>
          }
          @if (getStatusName() === 'Assessed') {
            <div class="mt-4 p-3 bg-yellow-50 rounded-lg text-sm text-yellow-700">Requirements have been assessed. Approve the enrollment to proceed.</div>
            <div class="mt-3"><button (click)="moveStep()" class="btn btn-primary">Approve Enrollment</button></div>
          }
          @if (getStatusName() === 'Approved') {
            <div class="mt-4 p-3 bg-purple-50 rounded-lg text-sm text-purple-700">Waiting for student to pay. Once fully paid, verify and mark as Paid.</div>
            <div class="mt-3"><button (click)="moveStep()" class="btn btn-primary">Verify Payment & Mark Paid</button></div>
          }
          @if (getStatusName() === 'Paid') {
            <div class="mt-4 p-3 bg-green-50 rounded-lg text-sm text-green-700">Payment verified. Assign a section below, then finalize enrollment.</div>
            <div class="mt-3"><button (click)="moveStep()" class="btn btn-primary">Finalize Enrollment</button></div>
          }
          @if (getStatusName() === 'Draft') {
            <div class="mt-4 p-3 bg-gray-50 rounded-lg text-sm text-gray-600">Waiting for student to upload requirements and submit.</div>
          }
        </div>

        <!-- Section Assignment -->
        <div class="bg-white rounded-xl border border-gray-200 p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Section Assignment</h3>
          @if (enrollment()!.sectionName) {
            <p class="text-gray-600">Assigned to: <span class="font-medium text-gray-900">{{ enrollment()!.sectionName }}</span></p>
          } @else {
            <div class="flex items-end gap-3">
              <div class="flex-1">
                <label class="form-label">Select Section</label>
                <select [(ngModel)]="selectedSectionId" class="form-input">
                  <option value="">Choose...</option>
                  @for (s of sections(); track s.id) {
                    <option [value]="s.id">{{ s.name }} ({{ s.currentCount }}/{{ s.capacity }})</option>
                  }
                </select>
              </div>
              <button (click)="assignSection()" [disabled]="!selectedSectionId" class="btn btn-primary disabled:opacity-50">Assign</button>
            </div>
          }
        </div>

        <!-- Requirements Review (Admin) -->
        @if (enrollment()!.requirements && enrollment()!.requirements!.length > 0) {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mb-6">
            <h3 class="font-semibold text-gray-900 mb-4">Requirements</h3>
            <div class="space-y-2">
              @for (req of enrollment()!.requirements!; track req.id) {
                <div class="flex items-center justify-between py-2 border-b border-gray-100 last:border-0">
                  <div class="flex items-center gap-3">
                    @if (req.isSubmitted) {
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                    } @else {
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                    }
                    <div>
                      <p class="text-sm font-medium text-gray-900">{{ req.documentName }}</p>
                      @if (req.fileName) { <p class="text-xs text-gray-500">{{ req.fileName }}</p> }
                    </div>
                  </div>
                  <span class="text-xs font-medium" [class]="req.isSubmitted ? 'text-emerald-600' : 'text-gray-400'">
                    {{ req.isSubmitted ? 'Submitted' : 'Pending' }}
                  </span>
                </div>
              }
            </div>
          </div>
        }

        <!-- Balance & Payments -->
        <div class="bg-white rounded-xl border border-gray-200 p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Fees & Payments</h3>
          @if (balance()) {
            <div class="grid grid-cols-3 gap-4 mb-4">
              <div class="bg-gray-50 border border-gray-200 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Total Fees</p>
                <p class="text-xl font-bold text-gray-900">{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
              <div class="bg-emerald-50 border border-emerald-200 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Total Paid</p>
                <p class="text-xl font-bold text-emerald-700">{{ balance()!.totalPaid | number:'1.2-2' }}</p>
              </div>
              <div class="p-4 rounded-lg text-center border" [ngClass]="balance()!.balance > 0 ? 'bg-red-50 border-red-200' : 'bg-emerald-50 border-emerald-200'">
                <p class="text-sm text-gray-500">Balance</p>
                <p class="text-xl font-bold" [ngClass]="balance()!.balance > 0 ? 'text-red-700' : 'text-emerald-700'">{{ balance()!.balance | number:'1.2-2' }}</p>
              </div>
            </div>
          }

          <div class="border-t border-gray-200 pt-4 mt-4">
            <h4 class="text-sm font-medium text-gray-900 mb-3">Record Payment</h4>
            <div class="flex flex-wrap gap-3">
              <input type="number" [(ngModel)]="paymentAmount" placeholder="Amount" class="form-input w-32" />
              <select [(ngModel)]="paymentMethod" class="form-input w-auto">
                <option value="Cash">Cash</option>
                <option value="Bank Transfer">Bank Transfer</option>
                <option value="GCash">GCash</option>
                <option value="Maya">Maya</option>
              </select>
              <input type="text" [(ngModel)]="paymentRef" placeholder="Reference #" class="form-input w-auto" />
              <button (click)="recordPayment()" [disabled]="!paymentAmount" class="btn btn-primary disabled:opacity-50">Record</button>
            </div>
          </div>

          @if (payments().length > 0) {
            <div class="mt-4 rounded-lg border border-gray-200 overflow-hidden">
              <table class="w-full text-sm">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                    <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Method</th>
                    <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Reference</th>
                    <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-200">
                  @for (p of payments(); track p.id) {
                    <tr class="hover:bg-gray-50 transition-colors">
                      <td class="px-4 py-2 text-gray-600">{{ p.paymentDate | date:'short' }}</td>
                      <td class="px-4 py-2 text-gray-600">{{ p.paymentMethod }}</td>
                      <td class="px-4 py-2 text-gray-600">{{ p.referenceNumber || '-' }}</td>
                      <td class="px-4 py-2 text-right font-medium text-gray-900">{{ p.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class EnrollmentDetailComponent implements OnInit {
  enrollment = signal<Enrollment | null>(null);
  sections = signal<Section[]>([]);
  balance = signal<BalanceInfo | null>(null);
  payments = signal<Payment[]>([]);

  selectedSectionId = '';
  paymentAmount = 0;
  paymentMethod = 'Cash';
  paymentRef = '';

  statuses = ['Draft', 'Submitted', 'Assessed', 'Approved', 'Paid', 'Enrolled'];

  private enrollmentId = '';

  constructor(private api: ApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.enrollmentId = this.route.snapshot.paramMap.get('id')!;
    this.loadAll();
  }

  loadAll(): void {
    this.api.getEnrollment(this.enrollmentId).subscribe(e => {
      this.enrollment.set(e);
      this.api.getSections(e.schoolYear, e.gradeLevel).subscribe(s => this.sections.set(s));
    });
    this.api.getBalance(this.enrollmentId).subscribe(b => this.balance.set(b));
    this.api.getPayments(this.enrollmentId).subscribe(p => this.payments.set(p));
  }

  getStepIndex(): number {
    const status = this.enrollment()?.status;
    if (status == null) return 0;
    // Handle both numeric (0-5) and string ('Draft', 'Submitted', ...) status
    if (typeof status === 'number') return status;
    return this.statuses.indexOf(status);
  }

  getStatusName(): string {
    const status = this.enrollment()?.status;
    if (status == null) return 'Draft';
    if (typeof status === 'number') return this.statuses[status] ?? 'Draft';
    return status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-700', Submitted: 'bg-blue-100 text-blue-700',
      Assessed: 'bg-yellow-100 text-yellow-700', Approved: 'bg-purple-100 text-purple-700',
      Paid: 'bg-green-100 text-green-700', Enrolled: 'bg-emerald-100 text-emerald-800'
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }

  moveStep(): void {
    this.api.moveEnrollmentStep(this.enrollmentId, 'Advanced by user').subscribe(() => this.loadAll());
  }

  assignSection(): void {
    if (!this.selectedSectionId) return;
    this.api.assignSection(this.enrollmentId, this.selectedSectionId).subscribe(() => this.loadAll());
  }

  recordPayment(): void {
    if (!this.paymentAmount) return;
    this.api.createPayment({
      enrollmentId: this.enrollmentId,
      amount: this.paymentAmount,
      paymentMethod: this.paymentMethod,
      referenceNumber: this.paymentRef || null,
      remarks: null
    }).subscribe(() => {
      this.paymentAmount = 0;
      this.paymentRef = '';
      this.loadAll();
    });
  }
}
