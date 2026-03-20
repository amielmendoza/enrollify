import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Payment, BalanceInfo } from '../../../core/models';

@Component({
  selector: 'app-my-payments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <h1 class="text-2xl font-bold text-gray-900">My Payments</h1>
      <p class="mt-1 text-sm text-gray-500">View your balance and make payments</p>

      @if (balance()) {
        <!-- Balance Summary -->
        <div class="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-6">
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-[#4361ee] bg-blue-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 18.75a60.07 60.07 0 0 1 15.797 2.101c.727.198 1.453-.342 1.453-1.096V18.75M3.75 4.5v.75A.75.75 0 0 1 3 6h-.75m0 0v-.375c0-.621.504-1.125 1.125-1.125H20.25M2.25 6v9m18-10.5v.75c0 .414.336.75.75.75h.75m-1.5-1.5h.375c.621 0 1.125.504 1.125 1.125v9.75c0 .621-.504 1.125-1.125 1.125h-.375m1.5-1.5H21a.75.75 0 0 0-.75.75v.75m0 0H3.75m0 0h-.375a1.125 1.125 0 0 1-1.125-1.125V15m1.5 1.5v-.75A.75.75 0 0 0 3 15h-.75M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm3 0h.008v.008H18V10.5Zm-12 0h.008v.008H6V10.5Z" /></svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Total Fees</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ balance()!.totalFees | number:'1.2-2' }}</p>
              </div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
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
          <div class="bg-white rounded-xl border border-gray-200 p-6">
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

        <!-- Make Payment Form -->
        @if (balance()!.balance > 0) {
          <div class="mt-6 bg-white rounded-xl border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Make a Payment</h2>

            @if (paySuccess()) {
              <div class="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700 flex items-center gap-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                Payment recorded successfully!
              </div>
            }
            @if (payError()) {
              <div class="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ payError() }}</div>
            }

            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <div>
                <label class="form-label">Amount *</label>
                <input type="number" [(ngModel)]="payAmount" [max]="balance()!.balance" min="1" step="0.01"
                       class="form-input" placeholder="0.00" />
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
              <div class="flex items-end">
                <button (click)="submitPayment()" [disabled]="paying() || !payAmount || payAmount <= 0"
                        class="w-full flex items-center justify-center gap-2 rounded-xl bg-[#4361ee] py-2.5 text-sm font-semibold text-white hover:bg-[#3a56d4] transition-colors disabled:opacity-60">
                  @if (paying()) {
                    <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                  }
                  Submit Payment
                </button>
              </div>
            </div>
          </div>
        }
      }

      <!-- Payment History -->
      <div class="mt-6 bg-white rounded-xl border border-gray-200">
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
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Remarks</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (p of payments(); track p.id) {
                <tr class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm">{{ p.paymentDate | date:'mediumDate' }}</td>
                  <td class="px-6 py-4 text-sm">
                    <span class="inline-flex items-center gap-1">
                      @if (p.paymentMethod === 'GCash') { <span class="inline-block w-2 h-2 rounded-full bg-blue-500"></span> }
                      @else if (p.paymentMethod === 'Maya') { <span class="inline-block w-2 h-2 rounded-full bg-green-500"></span> }
                      @else if (p.paymentMethod === 'Bank Transfer') { <span class="inline-block w-2 h-2 rounded-full bg-violet-500"></span> }
                      @else { <span class="inline-block w-2 h-2 rounded-full bg-gray-400"></span> }
                      {{ p.paymentMethod }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-sm font-mono text-gray-500">{{ p.referenceNumber || '-' }}</td>
                  <td class="px-6 py-4 text-sm text-gray-500">{{ p.remarks || '-' }}</td>
                  <td class="px-6 py-4 text-sm text-right font-semibold">\u20B1{{ p.amount | number:'1.2-2' }}</td>
                </tr>
              }
              @empty {
                <tr><td colspan="5" class="px-6 py-8 text-center text-gray-400">No payments recorded yet</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class MyPaymentsComponent implements OnInit {
  payments = signal<Payment[]>([]);
  balance = signal<BalanceInfo | null>(null);
  payAmount = 0;
  payMethod = 'GCash';
  payRef = '';
  paying = signal(false);
  paySuccess = signal(false);
  payError = signal('');

  constructor(private api: ApiService) {}

  ngOnInit() { this.load(); }

  load() {
    this.api.getMyPaymentsAndBalance().subscribe({
      next: (res) => {
        this.balance.set(res.balance);
        this.payments.set(res.payments);
      },
      error: () => {
        // No enrollment yet
        this.balance.set({ totalFees: 0, totalPaid: 0, balance: 0 });
      }
    });
  }

  submitPayment() {
    if (!this.payAmount || this.payAmount <= 0) return;
    this.paying.set(true);
    this.paySuccess.set(false);
    this.payError.set('');
    this.api.submitStudentPayment({
      amount: this.payAmount,
      paymentMethod: this.payMethod,
      referenceNumber: this.payRef || null,
      remarks: null
    }).subscribe({
      next: () => {
        this.paying.set(false);
        this.paySuccess.set(true);
        this.payAmount = 0;
        this.payRef = '';
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
