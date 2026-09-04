import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { NotificationService } from '../../core/services/notification.service';
import { Payment, BalanceInfo, FeeLine, Installment, EnrollmentLedger } from '../../core/models';

type SelectedInstallment = Installment | null;

@Component({
  selector: 'app-my-payments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <h1 class="text-2xl font-bold text-gray-900">My Payments</h1>
      <p class="mt-1 text-sm text-gray-500">View your balance, payment plan, and make payments</p>

      @if (balance()) {
        <!-- Balance Summary -->
        <div class="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <div class="flex items-start gap-4">
              <div class="text-[#0038A8] bg-blue-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 18.75a60.07 60.07 0 0 1 15.797 2.101c.727.198 1.453-.342 1.453-1.096V18.75M3.75 4.5v.75A.75.75 0 0 1 3 6h-.75m0 0v-.375c0-.621.504-1.125 1.125-1.125H20.25M2.25 6v9m18-10.5v.75c0 .414.336.75.75.75h.75m-1.5-1.5h.375c.621 0 1.125.504 1.125 1.125v9.75c0 .621-.504 1.125-1.125 1.125h-.375m1.5-1.5H21a.75.75 0 0 0-.75.75v.75m0 0H3.75m0 0h-.375a1.125 1.125 0 0 1-1.125-1.125V15m1.5 1.5v-.75A.75.75 0 0 0 3 15h-.75M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm3 0h.008v.008H18V10.5Zm-12 0h.008v.008H6V10.5Z" /></svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Total Fees</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <div class="flex items-start gap-4">
              <div class="text-emerald-500 bg-emerald-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Total Paid</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ balance()!.totalPaid | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <div class="flex items-start gap-4">
              <div class="rounded-lg p-2" [class]="balance()!.balance > 0 ? 'text-orange-500 bg-orange-50' : 'text-emerald-500 bg-emerald-50'">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Remaining Balance</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ balance()!.balance | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Tuition Breakdown -->
        @if (fees().length > 0) {
          <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Tuition Breakdown</h2>
            <div class="space-y-3">
              @for (f of fees(); track f.name) {
                <div class="flex items-center justify-between py-2 border-b border-gray-100 last:border-0">
                  <div>
                    <p class="text-sm font-medium text-gray-900">{{ f.name }}</p>
                    @if (f.description) { <p class="text-xs text-gray-500">{{ f.description }}</p> }
                  </div>
                  <p class="text-sm font-semibold text-gray-900">\u20B1{{ f.amount | number:'1.2-2' }}</p>
                </div>
              }
              <div class="flex items-center justify-between pt-2 border-t-2 border-gray-200">
                <p class="text-sm font-bold text-gray-900">Total</p>
                <p class="text-base font-bold text-gray-900">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
        }

        <!-- Payment Plan Selection -->
        @if (!paymentPlan() && balance()!.balance > 0) {
          <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-2">Select Payment Plan</h2>
            <p class="text-sm text-gray-500 mb-4">Choose how you'd like to pay your tuition. This cannot be changed after your first payment.</p>
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
              @for (plan of planOptions; track plan.value) {
                <button (click)="selectedPlan = plan.value"
                        class="relative border-2 rounded-xl p-5 text-left transition-all cursor-pointer"
                        [class]="selectedPlan === plan.value ? 'border-[#0038A8] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                  @if (selectedPlan === plan.value) {
                    <div class="absolute top-3 right-3 w-5 h-5 rounded-full flex items-center justify-center text-white" style="background-color: #0038A8;">
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                    </div>
                  }
                  <p class="text-base font-semibold text-gray-900">{{ plan.label }}</p>
                  <p class="text-sm text-gray-500 mt-1">{{ plan.description }}</p>
                  <p class="text-lg font-bold mt-3" style="color: #0038A8;">{{ plan.detail }}</p>
                </button>
              }
            </div>
            <div class="mt-4 flex justify-end">
              <button (click)="confirmPlan()" [disabled]="!selectedPlan || selectingPlan()"
                      class="flex items-center gap-2 rounded-xl bg-[#0038A8] px-6 py-2.5 text-sm font-semibold text-white hover:bg-[#002B85] transition-colors disabled:opacity-60">
                @if (selectingPlan()) {
                  <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                }
                Confirm Payment Plan
              </button>
            </div>
          </div>
        }

        <!-- Payment Schedule (shown after plan selected) -->
        @if (paymentPlan() && schedule().length > 0) {
          <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
            <div class="flex items-center justify-between mb-4">
              <h2 class="text-lg font-semibold text-gray-900">Payment Schedule</h2>
              <span class="badge badge-info">{{ paymentPlan() }} Payment</span>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">#</th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
                    <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Due Date</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                    <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  @for (inst of schedule(); track inst.number) {
                    <tr [class]="inst.isPaid ? 'bg-emerald-50/30' : selectedInstallment()?.number === inst.number && !hasPendingPayment() ? 'bg-blue-50' : isNextDue(inst) && !hasPendingPayment() ? 'hover:bg-gray-50 cursor-pointer' : 'opacity-60'"
                        (click)="isNextDue(inst) && !hasPendingPayment() && selectInstallment(inst)">
                      <td class="px-4 py-3 text-sm font-medium text-gray-600">
                        @if (!inst.isPaid && selectedInstallment()?.number === inst.number) {
                          <div class="w-5 h-5 rounded border-2 flex items-center justify-center" style="border-color: #0038A8; background-color: #0038A8;">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                          </div>
                        } @else if (!inst.isPaid) {
                          <div class="w-5 h-5 rounded border-2 border-gray-300"></div>
                        } @else {
                          {{ inst.number }}
                        }
                      </td>
                      <td class="px-4 py-3 text-sm font-medium text-gray-900">{{ inst.label }}</td>
                      <td class="px-4 py-3 text-sm text-gray-500">{{ inst.dueDate | date:'mediumDate' }}</td>
                      <td class="px-4 py-3 text-sm text-right font-semibold text-gray-900">\u20B1{{ inst.amount | number:'1.2-2' }}</td>
                      <td class="px-4 py-3 text-center">
                        @if (inst.isPaid) {
                          <span class="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                            Paid
                          </span>
                        } @else if (isNextDue(inst) && hasPendingPayment()) {
                          <span class="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-700">Pending Review</span>
                        } @else if (selectedInstallment()?.number === inst.number && !hasPendingPayment()) {
                          <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium text-white" style="background-color: #0038A8;">Selected</span>
                        } @else {
                          <span class="inline-flex items-center rounded-full bg-orange-100 px-2.5 py-0.5 text-xs font-medium text-orange-700">Upcoming</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="bg-gray-50">
                  <tr>
                    <td colspan="3" class="px-4 py-3 text-sm font-bold text-gray-900">Total</td>
                    <td class="px-4 py-3 text-sm text-right font-bold text-gray-900">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </div>
        }

        <!-- Make Payment Form -->
        @if (paymentPlan() && balance()!.balance > 0) {
          <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Make a Payment</h2>

            @if (hasPendingPayment()) {
              <div class="p-4 bg-yellow-50 border border-yellow-200 rounded-lg flex items-start gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-yellow-500 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                <div>
                  <p class="text-sm font-medium text-yellow-800">Payment under review</p>
                  <p class="text-sm text-yellow-700 mt-1">Your previous payment is being reviewed by the registrar. You can submit a new payment once it has been approved or rejected.</p>
                </div>
              </div>
            }

            @else {
            @if (paySuccess()) {
              <div class="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700 flex items-center gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                Payment recorded successfully!
              </div>
            }
            @if (payError()) {
              <div class="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ payError() }}</div>
            }

            @if (!selectedInstallment()) {
              <div class="p-4 bg-gray-50 rounded-lg text-sm text-gray-500 text-center">
                Select an installment from the schedule above to make a payment.
              </div>
            } @else {
              <div class="mb-4 p-3 rounded-lg text-sm font-medium" style="background-color: #F0F4FF; color: #0038A8;">
                Paying: {{ selectedInstallment()!.label }} &mdash; \u20B1{{ selectedInstallment()!.amount | number:'1.2-2' }}
              </div>

              <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div>
                  <label class="form-label">Amount</label>
                  <input type="number" [ngModel]="payAmount" disabled
                         class="form-input bg-gray-50 text-gray-700 cursor-not-allowed" />
                </div>
                <div>
                  <label class="form-label">Payment Method *</label>
                  <select [(ngModel)]="payMethod" class="form-input">
                    <option value="GCash">GCash</option>
                    <option value="Maya">Maya</option>
                    <option value="Bank Transfer">Bank Transfer</option>
                    <option value="Cash">Cash</option>
                  </select>
                </div>
                <div>
                  <label class="form-label">Reference Number</label>
                  <input type="text" [(ngModel)]="payRef" class="form-input" placeholder="e.g. GCash ref #" />
                </div>
              </div>

              <!-- Receipt Upload -->
              <div class="mt-4">
                <label class="form-label">Proof of Payment (Receipt) *</label>
                @if (!receiptFile()) {
                  <label class="flex flex-col items-center justify-center w-full h-32 border-2 border-dashed border-gray-300 rounded-xl cursor-pointer hover:border-[#0038A8] hover:bg-blue-50/30 transition-colors">
                    <div class="flex flex-col items-center justify-center pt-5 pb-6">
                      <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-gray-400 mb-2" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M12 16.5V9.75m0 0 3 3m-3-3-3 3M6.75 19.5a4.5 4.5 0 0 1-1.41-8.775 5.25 5.25 0 0 1 10.233-2.33 3 3 0 0 1 3.758 3.848A3.752 3.752 0 0 1 18 19.5H6.75Z" />
                      </svg>
                      <p class="text-sm text-gray-500"><span class="font-medium" style="color: #0038A8;">Click to upload</span> or drag and drop</p>
                      <p class="text-xs text-gray-400 mt-1">PNG, JPG, or PDF (max 10MB)</p>
                    </div>
                    <input type="file" class="hidden" accept="image/*,.pdf" (change)="onFileSelected($event)" />
                  </label>
                } @else {
                  <div class="flex items-center justify-between border border-gray-200 rounded-xl p-4">
                    <div class="flex items-center gap-3">
                      @if (receiptPreview()) {
                        <img [src]="receiptPreview()" class="h-16 w-16 object-cover rounded-lg border border-gray-200" />
                      } @else {
                        <div class="h-16 w-16 rounded-lg bg-gray-100 flex items-center justify-center">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
                          </svg>
                        </div>
                      }
                      <div>
                        <p class="text-sm font-medium text-gray-900">{{ receiptFile()!.name }}</p>
                        <p class="text-xs text-gray-500">{{ (receiptFile()!.size / 1024) | number:'1.0-0' }} KB</p>
                      </div>
                    </div>
                    <button (click)="removeReceipt()" class="text-sm text-red-500 hover:text-red-700 font-medium">Remove</button>
                  </div>
                }
              </div>

              <div class="mt-4">
                <button (click)="submitPayment()" [disabled]="paying() || !receiptFile()"
                        class="flex items-center justify-center gap-2 rounded-xl bg-[#0038A8] px-8 py-2.5 text-sm font-semibold text-white hover:bg-[#002B85] transition-colors disabled:opacity-60">
                  @if (paying()) {
                    <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                  }
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 16.5V9.75m0 0 3 3m-3-3-3 3M6.75 19.5a4.5 4.5 0 0 1-1.41-8.775 5.25 5.25 0 0 1 10.233-2.33 3 3 0 0 1 3.758 3.848A3.752 3.752 0 0 1 18 19.5H6.75Z" /></svg>
                  Submit Payment with Receipt
                </button>
              </div>
            }
            }
          </div>
        }
      }

      <!-- Payment History -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
        <div class="p-6 border-b border-gray-100">
          <h2 class="text-lg font-semibold text-gray-900">Payment History</h2>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Method</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Reference</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                <th class="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (p of payments(); track p.id) {
                <tr class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm">{{ p.paymentDate | date:'mediumDate' }}</td>
                  <td class="px-6 py-4 text-sm">
                    <span class="inline-flex items-center gap-1.5">
                      @if (p.paymentMethod === 'GCash') { <span class="inline-block w-2 h-2 rounded-full bg-blue-500"></span> }
                      @else if (p.paymentMethod === 'Maya') { <span class="inline-block w-2 h-2 rounded-full bg-green-500"></span> }
                      @else if (p.paymentMethod === 'Bank Transfer') { <span class="inline-block w-2 h-2 rounded-full bg-violet-500"></span> }
                      @else { <span class="inline-block w-2 h-2 rounded-full bg-gray-400"></span> }
                      {{ p.paymentMethod }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-sm folio-mono text-gray-500">{{ p.referenceNumber || '-' }}</td>
                  <td class="px-6 py-4 text-sm text-right font-semibold">\u20B1{{ p.amount | number:'1.2-2' }}</td>
                  <td class="px-6 py-4 text-center">
                    @if (p.status === 'Pending') {
                      <span class="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-700">Pending Review</span>
                    } @else if (p.status === 'Approved') {
                      <span class="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">Approved</span>
                    } @else {
                      <span class="inline-flex items-center rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-medium text-red-700">Rejected</span>
                    }
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="6" class="px-6 py-8 text-center text-gray-400">No payments recorded yet</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Account Ledger (read-only statement of account) -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
        <div class="p-6 border-b border-gray-100">
          <h2 class="text-lg font-semibold text-gray-900">Account Ledger</h2>
          <p class="text-sm text-gray-500 mt-1">All charges, discounts, and payments on your account, in order.</p>
        </div>
        @if (ledger() && ledger()!.entries.length > 0) {
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Description</th>
                  <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Debit</th>
                  <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Credit</th>
                  <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Balance</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                @for (en of ledger()!.entries; track $index) {
                  <tr [class.opacity-60]="en.voided">
                    <td class="px-6 py-3 text-sm whitespace-nowrap text-gray-500">{{ en.date | date:'mediumDate' }}</td>
                    <td class="px-6 py-3 text-sm">
                      <span class="text-gray-900" [class.line-through]="en.voided">{{ en.description }}</span>
                      @if (en.voided) {
                        <span class="ml-2 inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Voided</span>
                      }
                      @if (en.reference) { <p class="text-xs text-gray-400 mt-0.5">Ref: <span class="folio-mono">{{ en.reference }}</span></p> }
                    </td>
                    <td class="px-6 py-3 text-sm text-right text-gray-900" [class.line-through]="en.voided">
                      @if (en.debit != null) { ₱{{ en.debit | number:'1.2-2' }} }
                    </td>
                    <td class="px-6 py-3 text-sm text-right text-emerald-600" [class.line-through]="en.voided">
                      @if (en.credit != null) { ₱{{ en.credit | number:'1.2-2' }} }
                    </td>
                    <td class="px-6 py-3 text-sm text-right font-medium text-gray-900">₱{{ en.balance | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="bg-gray-50">
                <tr>
                  <td colspan="2" class="px-6 py-3 text-sm font-bold text-gray-900">Totals</td>
                  <td class="px-6 py-3 text-sm text-right font-bold text-gray-900">₱{{ ledger()!.totalDebits | number:'1.2-2' }}</td>
                  <td class="px-6 py-3 text-sm text-right font-bold text-emerald-700">₱{{ ledger()!.totalCredits | number:'1.2-2' }}</td>
                  <td class="px-6 py-3 text-sm text-right font-bold text-gray-900">₱{{ ledger()!.balance | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        } @else {
          <p class="px-6 py-8 text-center text-sm text-gray-500">Ledger opens once the enrollment is assessed.</p>
        }
      </div>
    </div>
  `
})
export class MyPaymentsComponent implements OnInit {
  payments = signal<Payment[]>([]);
  balance = signal<BalanceInfo | null>(null);
  paymentPlan = signal<string | null>(null);
  fees = signal<FeeLine[]>([]);
  schedule = signal<Installment[]>([]);
  ledger = signal<EnrollmentLedger | null>(null);

  selectedInstallment = signal<SelectedInstallment>(null);

  hasPendingPayment = computed(() => this.payments().some(p => p.status === 'Pending'));

  nextInstallment = computed(() => {
    const s = this.schedule();
    return s.find(i => !i.isPaid) ?? null;
  });

  // Payment plan selection
  selectedPlan = '';
  selectingPlan = signal(false);
  // Fallbacks match the seeded defaults; loadPlanOptions() replaces them with the
  // school's actual PaymentTerms so the copy never contradicts the computed schedule.
  planOptions = [
    { value: 'Full', label: 'Full Payment', description: 'Pay everything at once', detail: '1 payment' },
    { value: 'Monthly', label: 'Monthly', description: '20% down + 9 monthly installments', detail: '10 payments' },
    { value: 'Quarterly', label: 'Quarterly', description: '30% down + 3 quarterly installments', detail: '4 payments' },
  ];

  // Make payment
  payAmount = 0;
  payMethod = 'GCash';
  payRef = '';
  paying = signal(false);
  paySuccess = signal(false);
  payError = signal('');
  receiptFile = signal<File | null>(null);
  receiptPreview = signal<string | null>(null);

  constructor(private api: ApiService, private notify: NotificationService) {}

  ngOnInit() {
    this.load();
    this.loadPlanOptions();
  }

  private loadPlanOptions() {
    this.api.getPaymentTerms().subscribe({
      next: (terms) => {
        if (!terms.length) return;
        const pick = (type: string) => terms.find(t => t.planType === type && t.isActive) ?? terms.find(t => t.planType === type);
        const full = pick('Full'); const monthly = pick('Monthly'); const quarterly = pick('Quarterly');
        this.planOptions = [
          { value: 'Full', label: 'Full Payment', description: (full?.discountPercent ?? 0) > 0 ? `Pay everything at once (${full!.discountPercent}% discount)` : 'Pay everything at once', detail: '1 payment' },
          { value: 'Monthly', label: 'Monthly', description: `${monthly?.downPaymentPercent ?? 20}% down + ${monthly?.installmentCount ?? 9} monthly installments`, detail: `${(monthly?.installmentCount ?? 9) + 1} payments` },
          { value: 'Quarterly', label: 'Quarterly', description: `${quarterly?.downPaymentPercent ?? 30}% down + ${quarterly?.installmentCount ?? 3} quarterly installments`, detail: `${(quarterly?.installmentCount ?? 3) + 1} payments` },
        ];
      },
      error: () => {}
    });
  }

  load() {
    // Read-only ledger; errors just leave the empty state.
    this.api.getMyLedger().subscribe({
      next: l => this.ledger.set(l),
      error: () => this.ledger.set(null)
    });
    this.api.getMyPaymentsAndBalance().subscribe({
      next: (res) => {
        this.balance.set(res.balance);
        this.payments.set(res.payments);
        this.paymentPlan.set(res.paymentPlan);
        this.fees.set(res.fees);
        this.schedule.set(res.schedule);
        // Auto-select next unpaid installment only if no pending payments
        const hasPending = res.payments.some(p => p.status === 'Pending');
        const next = res.schedule.find(i => !i.isPaid);
        if (next && !hasPending) {
          this.selectInstallment(next);
        } else {
          this.selectedInstallment.set(null);
          this.payAmount = 0;
        }
      },
      error: () => {
        this.balance.set({ totalFees: 0, totalPaid: 0, balance: 0 });
      }
    });
  }

  selectInstallment(inst: Installment) {
    this.selectedInstallment.set(inst);
    this.payAmount = inst.amount;
  }

  isNextDue(inst: Installment): boolean {
    return !inst.isPaid && inst.number === this.nextInstallment()?.number;
  }

  confirmPlan() {
    if (!this.selectedPlan) return;
    this.selectingPlan.set(true);
    this.api.selectPaymentPlan(this.selectedPlan).subscribe({
      next: () => {
        this.selectingPlan.set(false);
        this.load();
      },
      error: (err) => {
        this.selectingPlan.set(false);
        this.notify.error(err.error?.error || 'Failed to select payment plan');
      }
    });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      this.payError.set('File size must be less than 10MB');
      return;
    }

    this.receiptFile.set(file);

    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = () => this.receiptPreview.set(reader.result as string);
      reader.readAsDataURL(file);
    } else {
      this.receiptPreview.set(null);
    }
  }

  removeReceipt() {
    this.receiptFile.set(null);
    this.receiptPreview.set(null);
  }

  submitPayment() {
    if (!this.payAmount || this.payAmount <= 0 || !this.receiptFile()) return;
    this.paying.set(true);
    this.paySuccess.set(false);
    this.payError.set('');

    // Upload the receipt first — the registrar reviews the actual file, not just a filename.
    this.api.uploadFile(this.receiptFile()!).subscribe({
      next: (up) => this.recordPayment(up.fileName, up.fileUrl),
      error: (err) => {
        this.paying.set(false);
        this.payError.set(err.error?.error || 'Receipt upload failed. Please try again.');
      }
    });
  }

  private recordPayment(receiptFileName: string, receiptFileUrl: string) {
    this.api.submitStudentPayment({
      amount: this.payAmount,
      paymentMethod: this.payMethod,
      referenceNumber: this.payRef || null,
      remarks: null,
      receiptFileName,
      receiptFileUrl
    }).subscribe({
      next: () => {
        this.paying.set(false);
        this.paySuccess.set(true);
        this.payAmount = 0;
        this.payRef = '';
        this.receiptFile.set(null);
        this.receiptPreview.set(null);
        this.load();
        setTimeout(() => this.paySuccess.set(false), 5000);
      },
      error: (err) => {
        this.paying.set(false);
        this.payError.set(err.error?.error || 'Payment failed');
      }
    });
  }
}
