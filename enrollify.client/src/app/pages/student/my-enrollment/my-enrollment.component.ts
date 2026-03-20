import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Enrollment, BalanceInfo } from '../../../core/models';

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
                          <p class="text-xs text-emerald-600">Uploaded: {{ req.fileName }}</p>
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
              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-4">
                @for (plan of paymentPlans; track plan.value) {
                  <button (click)="selectPlan(plan.value)"
                          class="border-2 rounded-xl p-4 text-left transition-all hover:border-blue-300"
                          [class]="selectedPlan === plan.value ? 'border-blue-500 bg-blue-50' : 'border-gray-200'">
                    <p class="font-semibold text-gray-900">{{ plan.label }}</p>
                    <p class="text-sm text-gray-500 mt-1">{{ plan.description }}</p>
                  </button>
                }
              </div>
              <button (click)="confirmPaymentPlan()" [disabled]="!selectedPlan"
                      class="rounded-xl px-6 py-2.5 text-sm font-semibold text-white disabled:opacity-60" style="background-color: #4361ee;">
                Confirm Plan
              </button>
            } @else {
              <p class="text-sm text-gray-500 mb-4">Payment plan: <span class="font-semibold text-gray-900">{{ enrollment()!.paymentPlan }}</span></p>
              <p class="text-sm text-gray-500">Go to <a routerLink="/my-payments" class="font-medium" style="color: #4361ee;">My Payments</a> to make a payment.</p>
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
          <p class="text-gray-400">No enrollment found. Request enrollment from your dashboard.</p>
        </div>
      }
    </div>
  `
})
export class MyEnrollmentComponent implements OnInit {
  enrollment = signal<Enrollment | null>(null);
  balance = signal<BalanceInfo | null>(null);
  submitting = signal(false);
  selectedPlan = '';

  statusNames = ['Draft', 'Submitted', 'Assessed', 'Approved', 'Paid', 'Enrolled'];

  paymentPlans = [
    { value: 'Full', label: 'Full Payment', description: '100% due now' },
    { value: 'Monthly', label: 'Monthly', description: '20% down + 4 monthly' },
    { value: 'Quarterly', label: 'Quarterly', description: '30% down + 2 quarterly' }
  ];

  constructor(private api: ApiService) {}

  ngOnInit() { this.load(); }

  load() {
    this.api.getMyEnrollments().subscribe(enrollments => {
      if (enrollments.length > 0) this.enrollment.set(enrollments[0]);
    });
    this.api.getMyPaymentsAndBalance().subscribe({
      next: (res) => this.balance.set(res.balance),
      error: () => {}
    });
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
    if (typeof s === 'number') return this.statusNames[s] ?? 'Draft';
    return s;
  }

  allRequirementsUploaded(): boolean {
    const reqs = this.enrollment()?.requirements;
    if (!reqs || reqs.length === 0) return false;
    return reqs.every(r => r.isSubmitted);
  }

  uploadRequirement(reqId: string, docName: string) {
    // Simulate file upload by sending a filename
    const fileName = docName.replace(/ /g, '_').toLowerCase() + '_sofia_ramirez.pdf';
    this.api.uploadRequirement(reqId, fileName).subscribe(() => this.load());
  }

  submitEnrollment() {
    if (!this.enrollment()) return;
    this.submitting.set(true);
    this.api.submitEnrollment(this.enrollment()!.id).subscribe({
      next: () => { this.submitting.set(false); this.load(); },
      error: (err) => { this.submitting.set(false); alert(err.error?.error || 'Submit failed'); }
    });
  }

  selectPlan(plan: string) { this.selectedPlan = plan; }

  confirmPaymentPlan() {
    if (!this.selectedPlan) return;
    this.api.selectPaymentPlan(this.selectedPlan).subscribe(() => this.load());
  }
}
