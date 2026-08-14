import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { SchoolYearService } from '../../core/services/school-year.service';
import { NotificationService } from '../../core/services/notification.service';
import { Enrollment, BalanceInfo, PaymentTerm } from '../../core/models';
import { ENROLLMENT_STATUS_NAMES, ENROLLMENT_STEP_NAMES, enrollmentStatusName } from '../../core/constants';

@Component({
  selector: 'app-my-enrollment',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div>
      <h1 class="text-2xl font-bold text-gray-900">My Enrollment</h1>
      <p class="mt-1 text-sm text-gray-500">Track your enrollment progress</p>

      @if (enrollment()) {
        <!-- Progress Tracker -->
        <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
          <h2 class="font-semibold text-gray-900 mb-4">Enrollment Progress</h2>
          <div class="flex items-center justify-between">
            @for (s of statusNames; track s; let i = $index) {
              <div class="flex-1 flex flex-col items-center">
                @if (stepIndex() > i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold bg-emerald-500 text-white shadow-sm">&#10003;</div>
                } @else if (stepIndex() === i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold text-white shadow-sm" style="background-color: #4361ee;">{{ i + 1 }}</div>
                } @else {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold bg-gray-100 text-gray-400 border border-gray-200">{{ i + 1 }}</div>
                }
                <span class="text-xs mt-2 text-center" [class]="stepIndex() >= i ? 'text-gray-900 font-medium' : 'text-gray-400'">{{ s }}</span>
              </div>
              @if (i < statusNames.length - 1) {
                <div class="flex-1 h-0.5 rounded-full mx-1" [class]="stepIndex() > i ? 'bg-emerald-400' : 'bg-gray-200'"></div>
              }
            }
          </div>
        </div>

        <!-- STEP: Draft - Upload Requirements -->
        @if (statusName() === 'Draft') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <h2 class="font-semibold text-gray-900 mb-2">Upload Requirements</h2>
            <p class="text-sm text-gray-500 mb-4">Please upload all required documents before submitting your enrollment.</p>

            @if (enrollment()!.requirements) {
              <div class="space-y-3">
                @for (req of enrollment()!.requirements!; track req.id) {
                  <div class="flex items-center justify-between border border-gray-200 rounded-lg p-4">
                    <div class="flex items-center gap-3">
                      @if (req.isSubmitted) {
                        <div class="w-8 h-8 rounded-full bg-emerald-50 flex items-center justify-center">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                        </div>
                      } @else {
                        <div class="w-8 h-8 rounded-full bg-gray-100 flex items-center justify-center">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 16.5V9.75m0 0 3 3m-3-3-3 3M6.75 19.5a4.5 4.5 0 0 1-1.41-8.775 5.25 5.25 0 0 1 10.233-2.33 3 3 0 0 1 3.758 3.848A3.752 3.752 0 0 1 18 19.5H6.75Z" /></svg>
                        </div>
                      }
                      <div>
                        <p class="text-sm font-medium text-gray-900">{{ req.documentName }}</p>
                        @if (req.isSubmitted) {
                          <p class="text-xs text-emerald-600">
                            @if (req.notes) {
                              <button type="button" (click)="openFile(req.notes!)" class="underline hover:text-emerald-700">{{ req.fileName }}</button>
                            } @else {
                              {{ req.fileName }}
                            }
                          </p>
                        } @else {
                          <p class="text-xs text-gray-400">Not yet uploaded</p>
                        }
                      </div>
                    </div>
                    @if (!req.isSubmitted) {
                      <button (click)="uploadRequirement(req.id, req.documentName)"
                              class="text-sm font-medium px-4 py-2 rounded-lg border border-gray-200 hover:bg-gray-50 transition-colors" style="color: #4361ee;">
                        Upload
                      </button>
                    }
                  </div>
                }
              </div>

              @if (allRequirementsUploaded()) {
                <div class="mt-6">
                  <button (click)="submitEnrollment()" [disabled]="submitting()"
                          class="flex items-center gap-2 rounded-xl px-6 py-3 text-sm font-semibold text-white transition-colors disabled:opacity-60" style="background-color: #4361ee;">
                    @if (submitting()) {
                      <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                    }
                    Submit Enrollment
                  </button>
                </div>
              } @else {
                <p class="mt-4 text-sm text-orange-600">Upload all documents to enable submission.</p>
              }
            }
          </div>
        }

        <!-- STEP: Submitted/Assessed - Waiting for admin -->
        @if (statusName() === 'Submitted') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-blue-50 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <h2 class="font-semibold text-gray-900">Under Review</h2>
                <p class="text-sm text-gray-500">Your documents are being assessed by the registrar. Please wait.</p>
              </div>
            </div>
          </div>
        }

        @if (statusName() === 'Assessed') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-yellow-50 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-yellow-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <h2 class="font-semibold text-gray-900">Assessment Complete</h2>
                <p class="text-sm text-gray-500">Your documents have been assessed. Waiting for admin approval.</p>
              </div>
            </div>
          </div>
        }

        <!-- STEP: Approved - Select Payment Plan & Pay -->
        @if (statusName() === 'Approved') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <h2 class="font-semibold text-gray-900 mb-4">Payment</h2>

            @if (!enrollment()!.paymentPlan) {
              <p class="text-sm text-gray-500 mb-4">Select your preferred payment plan:</p>
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                @for (plan of paymentPlans(); track plan.value) {
                  <button (click)="selectPlan(plan.value)"
                          class="relative border-2 rounded-xl p-5 text-left transition-all cursor-pointer"
                          [class]="selectedPlan() === plan.value ? 'border-[#4361ee] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                    @if (selectedPlan() === plan.value) {
                      <div class="absolute top-3 right-3 w-5 h-5 rounded-full flex items-center justify-center text-white" style="background-color: #4361ee;">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                      </div>
                    }
                    <p class="text-base font-semibold text-gray-900">{{ plan.label }}</p>
                    <p class="text-sm text-gray-500 mt-1">{{ plan.description }}</p>
                    <p class="text-lg font-bold mt-2" style="color: #4361ee;">{{ getPaymentCount(plan.value) }} payment{{ getPaymentCount(plan.value) > 1 ? 's' : '' }}</p>
                  </button>
                }
              </div>

              <!-- Payment Breakdown Preview -->
              @if (selectedPlan() && balance() && balance()!.totalFees > 0) {
                <div class="mt-5 border border-gray-200 rounded-xl overflow-hidden">
                  <div class="bg-gray-50 px-4 py-3 border-b border-gray-200">
                    <h3 class="text-sm font-semibold text-gray-900">Payment Schedule Preview</h3>
                  </div>
                  <table class="w-full">
                    <thead class="bg-gray-50/50">
                      <tr>
                        <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">#</th>
                        <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Description</th>
                        <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Due</th>
                        <th class="px-4 py-2 text-right text-xs font-medium text-gray-500 uppercase">Amount</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-100">
                      @for (inst of previewSchedule(); track inst.number) {
                        <tr>
                          <td class="px-4 py-2.5 text-sm text-gray-600">{{ inst.number }}</td>
                          <td class="px-4 py-2.5 text-sm font-medium text-gray-900">{{ inst.label }}</td>
                          <td class="px-4 py-2.5 text-sm text-gray-500">{{ inst.due }}</td>
                          <td class="px-4 py-2.5 text-sm text-right font-semibold text-gray-900">\u20B1{{ inst.amount | number:'1.2-2' }}</td>
                        </tr>
                      }
                    </tbody>
                    <tfoot class="bg-gray-50">
                      @if (previewDiscount() > 0) {
                        <tr>
                          <td colspan="3" class="px-4 py-1.5 text-xs text-emerald-700">Discount</td>
                          <td class="px-4 py-1.5 text-xs text-right text-emerald-700">-\u20B1{{ previewDiscount() | number:'1.2-2' }}</td>
                        </tr>
                      }
                      @if (previewInterest() > 0) {
                        <tr>
                          <td colspan="3" class="px-4 py-1.5 text-xs text-orange-600">Interest</td>
                          <td class="px-4 py-1.5 text-xs text-right text-orange-600">+\u20B1{{ previewInterest() | number:'1.2-2' }}</td>
                        </tr>
                      }
                      <tr>
                        <td colspan="3" class="px-4 py-2.5 text-sm font-bold text-gray-900">Total</td>
                        <td class="px-4 py-2.5 text-sm text-right font-bold text-gray-900">\u20B1{{ previewTotal() | number:'1.2-2' }}</td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              }

              <div class="mt-4 flex justify-end">
                <button (click)="confirmPaymentPlan()" [disabled]="!selectedPlan() || confirmingPlan()"
                        class="flex items-center gap-2 rounded-xl px-6 py-2.5 text-sm font-semibold text-white disabled:opacity-60 transition-colors" style="background-color: #4361ee;">
                  @if (confirmingPlan()) {
                    <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                  }
                  Confirm Payment Plan
                </button>
              </div>
            } @else {
              <p class="text-sm text-gray-500 mb-4">Payment plan: <span class="font-semibold text-gray-900">{{ enrollment()!.paymentPlan }}</span></p>
              <p class="text-sm text-gray-500">Go to <a routerLink="/my-payments" class="font-medium" style="color: #4361ee;">My Payments</a> to view schedule and make payments.</p>
            }
          </div>

          <!-- Balance -->
          @if (balance()) {
            <div class="mt-6 grid grid-cols-3 gap-4">
              <div class="bg-white rounded-xl border border-gray-200 p-4 text-center">
                <p class="text-sm text-gray-500">Total Fees</p>
                <p class="text-xl font-bold text-gray-900 mt-1">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
              <div class="bg-white rounded-xl border border-gray-200 p-4 text-center">
                <p class="text-sm text-gray-500">Paid</p>
                <p class="text-xl font-bold text-emerald-700 mt-1">\u20B1{{ balance()!.totalPaid | number:'1.2-2' }}</p>
              </div>
              <div class="bg-white rounded-xl border border-gray-200 p-4 text-center">
                <p class="text-sm text-gray-500">Balance</p>
                <p class="text-xl font-bold mt-1" [class]="balance()!.balance > 0 ? 'text-orange-600' : 'text-emerald-700'">\u20B1{{ balance()!.balance | number:'1.2-2' }}</p>
              </div>
            </div>
          }
        }

        <!-- STEP: Paid - Waiting for section assignment -->
        @if (statusName() === 'Paid') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-green-50 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <h2 class="font-semibold text-gray-900">Payment Verified</h2>
                <p class="text-sm text-gray-500">Your payment has been verified. Waiting for section assignment.</p>
              </div>
            </div>
          </div>
        }

        <!-- STEP: Enrolled - Complete -->
        @if (statusName() === 'Enrolled') {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-emerald-50 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <h2 class="font-semibold text-gray-900">Enrolled!</h2>
                <p class="text-sm text-gray-500">Section: {{ enrollment()!.sectionName || 'TBA' }} &bull; {{ enrollment()!.gradeLevel }} &bull; {{ enrollment()!.schoolYear }}</p>
              </div>
            </div>
          </div>
        }

        <!-- Balance summary for Paid/Enrolled -->
        @if ((statusName() === 'Paid' || statusName() === 'Enrolled') && balance()) {
          <div class="mt-6 bg-white rounded-xl border border-gray-200 p-6">
            <h2 class="font-semibold text-gray-900 mb-3">Tuition Summary</h2>
            <div class="grid grid-cols-3 gap-4">
              <div class="bg-gray-50 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Total Fees</p>
                <p class="text-xl font-bold text-gray-900">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
              <div class="bg-emerald-50 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Paid</p>
                <p class="text-xl font-bold text-emerald-700">\u20B1{{ balance()!.totalPaid | number:'1.2-2' }}</p>
              </div>
              <div class="bg-emerald-50 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Balance</p>
                <p class="text-xl font-bold text-emerald-700">\u20B1{{ balance()!.balance | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
        }

      } @else {
        <div class="mt-6 bg-white rounded-xl border border-gray-200 p-12 text-center">
          <p class="text-gray-400 mb-4">No enrollment found for the active school year.</p>
          <button (click)="requestEnrollment()" [disabled]="requesting()"
                  class="inline-flex items-center gap-2 rounded-xl bg-[#4361ee] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#3a56d4] disabled:opacity-60">
            @if (requesting()) {
              <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
            }
            Request Enrollment
          </button>
        </div>
      }
    </div>
  `
})
export class MyEnrollmentComponent implements OnInit {
  enrollment = signal<Enrollment | null>(null);
  balance = signal<BalanceInfo | null>(null);
  submitting = signal(false);
  requesting = signal(false);
  selectedPlan = signal('');

  statusNames = ENROLLMENT_STEP_NAMES;

  confirmingPlan = signal(false);
  terms = signal<PaymentTerm[]>([]);

  paymentPlans = computed(() => {
    const t = this.terms();
    if (t.length === 0) return [
      { value: 'Full', label: 'Full Payment', description: '100% due upon enrollment' },
      { value: 'Monthly', label: 'Monthly', description: 'Down payment + monthly installments' },
      { value: 'Quarterly', label: 'Quarterly', description: 'Down payment + quarterly installments' }
    ];
    return t.map(term => ({
      value: term.planType,
      label: term.planType === 'Full' ? 'Full Payment' : term.planType,
      description: this.buildPlanDescription(term)
    }));
  });

  previewSchedule = computed(() => {
    const total = this.balance()?.totalFees ?? 0;
    const plan = this.selectedPlan();
    if (!plan || total <= 0) return [];

    const term = this.terms().find(t => t.planType === plan);
    const sy = this.syService.active();
    const syStart = sy ? new Date(sy.startDate) : new Date();
    const fmt = (d: Date) => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

    if (plan === 'Full') {
      const discPct = term?.discountPercent ?? 0;
      const discount = Math.round(total * discPct / 100 * 100) / 100;
      const amount = total - discount;
      return [{ number: 1, label: discPct > 0 ? `Full Payment (${discPct}% discount)` : 'Full Payment', due: fmt(syStart), amount }];
    }

    const downPct = term?.downPaymentPercent ?? (plan === 'Monthly' ? 20 : 30);
    const intPct = term?.interestRatePercent ?? 0;
    const count = term?.installmentCount ?? (plan === 'Monthly' ? 9 : 3);
    const monthsPerInst = plan === 'Quarterly' ? 3 : 1;

    const down = Math.round(total * downPct / 100 * 100) / 100;
    const remaining = total - down;
    const interest = Math.round(remaining * intPct / 100 * 100) / 100;
    const totalRemaining = remaining + interest;
    const instAmt = Math.round(totalRemaining / count * 100) / 100;

    const schedule: { number: number; label: string; due: string; amount: number }[] = [
      { number: 1, label: `Down Payment (${downPct}%)`, due: fmt(syStart), amount: down }
    ];
    const ordinals = ['1st','2nd','3rd','4th','5th','6th','7th','8th','9th','10th','11th','12th'];
    for (let i = 0; i < count; i++) {
      const dueDate = new Date(syStart);
      dueDate.setMonth(dueDate.getMonth() + (i + 1) * monthsPerInst);
      const isLast = i === count - 1;
      const amt = isLast ? Math.round((totalRemaining - instAmt * (count - 1)) * 100) / 100 : instAmt;
      const label = plan === 'Quarterly' ? `${ordinals[i + 1]} Quarter` : `Month ${i + 1}`;
      schedule.push({ number: i + 2, label, due: fmt(dueDate), amount: amt });
    }
    return schedule;
  });

  previewTotal = computed(() => {
    return this.previewSchedule().reduce((sum, inst) => sum + inst.amount, 0);
  });

  previewDiscount = computed(() => {
    const total = this.balance()?.totalFees ?? 0;
    const plan = this.selectedPlan();
    if (plan !== 'Full' || total <= 0) return 0;
    const term = this.terms().find(t => t.planType === 'Full');
    return Math.round(total * (term?.discountPercent ?? 0) / 100 * 100) / 100;
  });

  previewInterest = computed(() => {
    const total = this.balance()?.totalFees ?? 0;
    const plan = this.selectedPlan();
    if (!plan || plan === 'Full' || total <= 0) return 0;
    const term = this.terms().find(t => t.planType === plan);
    const downPct = term?.downPaymentPercent ?? (plan === 'Monthly' ? 20 : 30);
    const intPct = term?.interestRatePercent ?? 0;
    const remaining = total - Math.round(total * downPct / 100 * 100) / 100;
    return Math.round(remaining * intPct / 100 * 100) / 100;
  });

  constructor(private api: ApiService, private syService: SchoolYearService, private notify: NotificationService) {}

  ngOnInit() {
    this.syService.ensureLoaded().subscribe(() => this.load());
  }

  load() {
    this.api.getMyEnrollments().subscribe(enrollments => {
      const activeSY = this.syService.activeName();
      // Skip cancelled enrollments so the student can request a fresh one.
      const match = enrollments.find(e => e.schoolYear === activeSY && enrollmentStatusName(e.status) !== 'Cancelled') ?? null;
      this.enrollment.set(match);
    });
    this.api.getMyPaymentsAndBalance().subscribe({
      next: (res) => this.balance.set(res.balance),
      error: () => {}
    });
    const activeSY = this.syService.activeName();
    if (activeSY) {
      this.api.getPaymentTerms(activeSY).subscribe(t => this.terms.set(t));
    }
  }

  requestEnrollment() {
    this.requesting.set(true);
    this.api.requestEnrollment().subscribe({
      next: () => { this.requesting.set(false); this.load(); },
      error: (err) => { this.requesting.set(false); this.notify.error(err.error?.error || 'Failed to request enrollment'); }
    });
  }

  getPaymentCount(planValue: string): number {
    const term = this.terms().find(t => t.planType === planValue);
    if (planValue === 'Full') return 1;
    return (term?.installmentCount ?? (planValue === 'Monthly' ? 9 : 3)) + 1; // +1 for down payment
  }

  buildPlanDescription(term: PaymentTerm): string {
    if (term.planType === 'Full') {
      return term.discountPercent > 0 ? `${term.discountPercent}% discount if paid in full` : '100% due upon enrollment';
    }
    const parts = [`${term.downPaymentPercent}% down + ${term.installmentCount} ${term.planType === 'Monthly' ? 'monthly' : 'quarterly'}`];
    if (term.interestRatePercent > 0) parts.push(`(${term.interestRatePercent}% interest)`);
    return parts.join(' ');
  }

  stepIndex(): number {
    const s = this.enrollment()?.status;
    if (s == null) return 0;
    if (typeof s === 'number') return s;
    return this.statusNames.indexOf(s);
  }

  statusName(): string {
    const s = this.enrollment()?.status;
    if (s == null) return 'Draft';
    if (typeof s === 'number') return ENROLLMENT_STATUS_NAMES[s] ?? 'Draft';
    return s;
  }

  allRequirementsUploaded(): boolean {
    const reqs = this.enrollment()?.requirements;
    if (!reqs || reqs.length === 0) return false;
    return reqs.every(r => r.isSubmitted);
  }

  uploadRequirement(reqId: string, docName: string) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*,.pdf';
    input.onchange = () => {
      const file = input.files?.[0];
      if (!file) return;
      if (file.size > 10 * 1024 * 1024) { this.notify.error('File must be under 10MB'); return; }
      // Upload file first, then mark requirement
      this.api.uploadFile(file).subscribe({
        next: (res) => {
          this.api.uploadRequirement(reqId, file.name, res.fileUrl).subscribe(() => this.load());
        },
        error: () => this.notify.error('Upload failed')
      });
    };
    input.click();
  }

  openFile(fileUrl: string): void {
    this.api.openFile(fileUrl);
  }

  submitEnrollment() {
    if (!this.enrollment()) return;
    this.submitting.set(true);
    this.api.submitEnrollment(this.enrollment()!.id).subscribe({
      next: () => { this.submitting.set(false); this.load(); },
      error: (err) => { this.submitting.set(false); this.notify.error(err.error?.error || 'Submit failed'); }
    });
  }

  selectPlan(plan: string) { this.selectedPlan.set(plan); }

  confirmPaymentPlan() {
    if (!this.selectedPlan()) return;
    this.confirmingPlan.set(true);
    this.api.selectPaymentPlan(this.selectedPlan()).subscribe({
      next: () => { this.confirmingPlan.set(false); this.load(); },
      error: (err) => { this.confirmingPlan.set(false); this.notify.error(err.error?.error || 'Failed'); }
    });
  }
}
