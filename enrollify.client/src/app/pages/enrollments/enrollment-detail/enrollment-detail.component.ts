import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Enrollment, Section, BalanceInfo, Payment, EnrollmentHistoryItem, EnrollmentLedger, ShsStrand, EnrollmentLearnerInfo } from '../../../core/models';
import { ENROLLMENT_STATUS_NAMES, ENROLLMENT_STEP_NAMES, LEARNER_TYPES, GRADE_LEVELS, isShsGrade } from '../../../core/constants';
import { emptyLearnerInfo } from '../../../core/learner';
import { StrandSelectComponent } from '../../../core/components/strand-select.component';
import { formatApiError } from '../../../core/utils/format-api-error';

@Component({
  selector: 'app-enrollment-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StrandSelectComponent],
  template: `
    <div class="max-w-4xl">
      @if (enrollment()) {
        <div class="flex flex-wrap items-center justify-between gap-4 mb-6">
          <div>
            <h2 class="text-2xl font-bold text-gray-900">
              <a [routerLink]="['/students', enrollment()!.studentId]" class="hover:text-[#0038A8] hover:underline" title="Open the student record">{{ enrollment()!.studentName }}</a>
            </h2>
            <p class="text-sm text-gray-500">{{ enrollment()!.schoolYear }} - {{ enrollment()!.gradeLevel }}@if (enrollment()!.learnerInfo?.strand) { · {{ enrollment()!.learnerInfo!.track }} – {{ enrollment()!.learnerInfo!.strand }} }</p>
            <p class="mt-0.5 text-xs text-gray-500">Student No. <span class="folio-mono text-gray-700">{{ enrollment()!.studentNumber || '—' }}</span> · LRN <span class="folio-mono text-gray-700">{{ enrollment()!.lrn || '—' }}</span></p>
          </div>
          <div class="flex flex-wrap items-center gap-3">
            <span class="inline-flex items-center rounded-full px-3 py-1 text-sm font-medium" [ngClass]="getStatusClass(getStatusName())">
              {{ getStatusName() }}
            </span>
            @if (canPrintAssessment()) {
              <button (click)="openPrintDoc('assessment')" class="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-gray-700 hover:bg-gray-50">Print Assessment</button>
            }
            @if (getStatusName() === 'Enrolled') {
              <button (click)="openPrintDoc('cor')" class="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-gray-700 hover:bg-gray-50">Print COR</button>
            }
            @if (getStatusName() !== 'Cancelled') {
              <button (click)="cancelEnrollment()" class="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-50">Cancel Enrollment</button>
            }
          </div>
        </div>

        <!-- Workflow Progress -->
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Enrollment Progress</h3>
          @if (getStatusName() === 'Cancelled') {
            <div class="p-3 bg-red-50 rounded-lg text-sm text-red-700">
              This enrollment has been cancelled.
              @if (enrollment()!.remarks) { Reason: {{ enrollment()!.remarks }} }
            </div>
          } @else {
          <div class="flex items-center justify-between">
            @for (s of statuses; track s; let i = $index) {
              <div class="flex-1 flex flex-col items-center">
                @if (getStepIndex() > i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold bg-emerald-500 text-white shadow-sm">&#10003;</div>
                } @else if (getStepIndex() === i) {
                  <div class="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold text-white shadow-sm" style="background-color: #0038A8;">{{ i + 1 }}</div>
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
            <div class="mt-3 flex flex-wrap gap-2">
              <button (click)="moveStep()" class="btn btn-primary">Approve Enrollment</button>
              <button (click)="sendBack()" class="rounded-lg border border-gray-200 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50">Send back for re-assessment</button>
            </div>
          }
          @if (getStatusName() === 'Approved') {
            <div class="mt-4 p-3 bg-purple-50 rounded-lg text-sm text-purple-700">
              @if (hasPendingPayments()) {
                Approve the pending payment(s) below — once approved payments cover the plan requirement, the enrollment moves to Paid automatically.
              } @else if (hasApprovedPayments()) {
                Student has approved payment(s). The enrollment advances to Paid automatically when the required amount is covered — use Mark as Paid below if it hasn't advanced.
              } @else {
                Waiting for student to submit payment. Once received, approve the payment below.
              }
            </div>
            @if (stepError()) {
              <div class="mt-2 p-3 bg-red-50 rounded-lg text-sm text-red-700">{{ stepError() }}</div>
            }
            <div class="mt-3 flex flex-wrap gap-2">
              <button (click)="moveStep()" [disabled]="hasPendingPayments() || !hasApprovedPayments()" class="btn btn-primary disabled:opacity-50">
                Mark as Paid
              </button>
              <button (click)="sendBack()" class="rounded-lg border border-gray-200 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50">Send back for re-assessment</button>
            </div>
          }
          @if (getStatusName() === 'Paid') {
            <div class="mt-4 p-3 bg-green-50 rounded-lg text-sm text-green-700">Payment verified. Assign a section below, then finalize enrollment.</div>
            @if (stepError()) {
              <div class="mt-2 p-3 bg-red-50 rounded-lg text-sm text-red-700">{{ stepError() }}</div>
            }
            <div class="mt-3"><button (click)="moveStep()" class="btn btn-primary">Finalize Enrollment</button></div>
          }
          @if (getStatusName() === 'Draft') {
            <div class="mt-4 p-3 bg-gray-50 rounded-lg text-sm text-gray-600">Waiting for student to upload requirements and submit.</div>
          }
          }
        </div>

        <!-- Section Assignment -->
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
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

        <!-- Tabs -->
        <div class="mb-6 border-b border-gray-200">
          <nav class="flex gap-6 overflow-x-auto whitespace-nowrap">
            <button (click)="activeTab.set('learner')"
                    class="shrink-0 pb-3 text-sm font-medium border-b-2 transition-colors inline-flex items-center gap-2"
                    [class]="activeTab() === 'learner' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              Learner
              @if (missingStrand()) {
                <span class="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">Needs strand</span>
              }
            </button>
            <button (click)="activeTab.set('requirements')"
                    class="shrink-0 pb-3 text-sm font-medium border-b-2 transition-colors inline-flex items-center gap-2"
                    [class]="activeTab() === 'requirements' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              Requirements
              @if (toVerifyCount() > 0) {
                <span class="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">{{ toVerifyCount() }} to verify</span>
              }
            </button>
            <button (click)="activeTab.set('payments')"
                    class="shrink-0 pb-3 text-sm font-medium border-b-2 transition-colors inline-flex items-center gap-2"
                    [class]="activeTab() === 'payments' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              Payments
              @if (pendingPaymentsCount() > 0) {
                <span class="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">{{ pendingPaymentsCount() }} pending</span>
              }
            </button>
            <button (click)="activeTab.set('ledger')"
                    class="shrink-0 pb-3 text-sm font-medium border-b-2 transition-colors"
                    [class]="activeTab() === 'ledger' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              Ledger
            </button>
            <button (click)="activeTab.set('history')"
                    class="shrink-0 pb-3 text-sm font-medium border-b-2 transition-colors"
                    [class]="activeTab() === 'history' ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              History
            </button>
          </nav>
        </div>

        <!-- Learner -->
        @if (activeTab() === 'learner') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6 space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-semibold text-gray-900">Learner details</h3>
            <a [routerLink]="['/students', enrollment()!.studentId, 'edit']" class="text-xs text-[#0038A8] hover:underline">Edit student record</a>
          </div>
          @if (learnerLocked()) {
            <p class="text-sm text-gray-600 bg-gray-50 border border-gray-200 rounded-lg p-3">Locked after assessment. Use “Send back for re-assessment” to change these.</p>
          } @else if (missingStrand()) {
            <p class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-3">Set the learner's track/strand before assessing this enrollment.</p>
          }
          @if (isShs() && (strands().length > 0 || learnerForm.strand)) {
            <app-strand-select [strands]="strands()" [track]="learnerForm.track" [strand]="learnerForm.strand"
                               [disabled]="learnerLocked()" selectId="learner-strand"
                               (changed)="learnerForm.track = $event.track; learnerForm.strand = $event.strand" />
          }
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="learner-type">Learner Type</label>
              <select id="learner-type" class="form-input" [disabled]="learnerLocked()"
                      [ngModel]="learnerForm.learnerType" (ngModelChange)="learnerForm.learnerType = $event" [ngModelOptions]="{ standalone: true }">
                <option [ngValue]="null">Not set</option>
                @for (t of learnerTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
              </select>
            </div>
            <div>
              <label class="form-label" for="last-grade">Last Grade Level Completed</label>
              <select id="last-grade" class="form-input" [disabled]="learnerLocked()"
                      [ngModel]="learnerForm.lastGradeLevelCompleted" (ngModelChange)="learnerForm.lastGradeLevelCompleted = $event" [ngModelOptions]="{ standalone: true }">
                <option [ngValue]="null">Not applicable</option>
                @for (g of gradeLevels; track g) { <option [ngValue]="g">{{ g }}</option> }
              </select>
            </div>
            <div>
              <label class="form-label" for="last-sy">Last School Year Completed</label>
              <input id="last-sy" type="text" maxlength="9" placeholder="2025-2026" class="form-input folio-mono" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolYearCompleted" (ngModelChange)="learnerForm.lastSchoolYearCompleted = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school-id">Last School's DepEd School ID</label>
              <input id="last-school-id" type="text" inputmode="numeric" maxlength="6" class="form-input folio-mono" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolId" (ngModelChange)="learnerForm.lastSchoolId = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school">Last School Attended</label>
              <input id="last-school" type="text" maxlength="200" class="form-input" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolAttended" (ngModelChange)="learnerForm.lastSchoolAttended = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
            <div>
              <label class="form-label" for="last-school-address">Last School Address</label>
              <input id="last-school-address" type="text" maxlength="500" class="form-input" [disabled]="learnerLocked()"
                     [ngModel]="learnerForm.lastSchoolAddress" (ngModelChange)="learnerForm.lastSchoolAddress = $event || null" [ngModelOptions]="{ standalone: true }" />
            </div>
          </div>
          @if (!learnerLocked()) {
            <div class="flex justify-end">
              <button (click)="saveLearnerInfo()" [disabled]="savingLearner()" class="btn btn-primary">{{ savingLearner() ? 'Saving…' : 'Save learner details' }}</button>
            </div>
          }
        </div>
        }

        <!-- Requirements Review (Admin) -->
        @if (activeTab() === 'requirements') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <div class="flex items-center justify-between mb-4">
            <h3 class="font-semibold text-gray-900">Requirements</h3>
            @if (isAdmin()) {
              <p class="text-xs text-gray-500">Manage the catalog in <a href="/settings" class="text-[#0038A8] hover:underline">Settings &rsaquo; Requirements</a></p>
            }
          </div>

          @if (reqError()) {
            <div class="mb-3 p-3 bg-red-50 rounded-lg text-sm text-red-700">{{ reqError() }}</div>
          }

          @if (enrollment()!.requirements && enrollment()!.requirements!.length > 0) {
            <div class="space-y-3">
              @for (req of enrollment()!.requirements!; track req.id) {
                <div class="border border-gray-200 rounded-lg p-3">
                  <div class="flex items-start justify-between gap-3">
                    <div class="flex items-start gap-3 flex-1">
                      @if (req.isVerified) {
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-500 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                      } @else if (req.isSubmitted) {
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-blue-500 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 16.5V9.75m0 0 3 3m-3-3-3 3M6.75 19.5a4.5 4.5 0 0 1-1.41-8.775 5.25 5.25 0 0 1 10.233-2.33 3 3 0 0 1 3.758 3.848A3.752 3.752 0 0 1 18 19.5H6.75Z" /></svg>
                      } @else {
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-300 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
                      }
                      <div class="flex-1">
                        <p class="text-sm font-medium text-gray-900">{{ req.documentName }}</p>
                        @if (req.fileName && req.notes) {
                          <button type="button" (click)="openFile(req.notes!)" class="text-xs text-[#0038A8] hover:underline">{{ req.fileName }}</button>
                        } @else if (req.fileName) {
                          <p class="text-xs text-gray-500">{{ req.fileName }}</p>
                        }
                        @if (req.isVerified && req.verifiedBy) {
                          <p class="text-xs text-emerald-600 mt-1">Verified by {{ req.verifiedBy }}</p>
                        }
                        @if (req.reviewNotes) {
                          <p class="text-xs italic text-gray-600 mt-1">"{{ req.reviewNotes }}"</p>
                        }
                      </div>
                    </div>
                    <div class="shrink-0">
                      @if (req.isVerified) {
                        <span class="badge badge-success">Verified</span>
                      } @else if (req.isSubmitted) {
                        <span class="badge badge-warning">Pending Review</span>
                      } @else {
                        <span class="text-xs font-medium text-gray-400">Not Submitted</span>
                      }
                    </div>
                  </div>

                  @if (isAdmin()) {
                    <div class="flex flex-wrap items-center gap-2 mt-3 pt-3 border-t border-gray-100">
                      @if (req.isSubmitted && !req.isVerified) {
                        <button (click)="reviewRequirement(req.id, true)" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 text-white hover:bg-emerald-700">Verify</button>
                        <button (click)="reviewRequirement(req.id, false)" class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-600 text-white hover:bg-red-700">Reject</button>
                      }
                      <label class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-gray-100 text-gray-700 hover:bg-gray-200 cursor-pointer">
                        {{ req.fileName ? 'Replace File' : 'Upload on Behalf' }}
                        <input type="file" class="hidden" (change)="adminUploadFile($event, req.id)" />
                      </label>
                      @if (uploadingReqId() === req.id) {
                        <span class="text-xs text-gray-500">Uploading...</span>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          } @else {
            <p class="text-sm text-gray-500">No requirements configured. Add some in <a href="/settings" class="text-[#0038A8] hover:underline">Settings &rsaquo; Requirements</a>.</p>
          }
        </div>
        }

        <!-- Balance & Payments -->
        @if (activeTab() === 'payments') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Fees & Payments</h3>
          @if (balance()) {
            <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-4">
              <div class="bg-gray-50 border border-gray-200 p-4 rounded-lg text-center">
                <p class="text-sm text-gray-500">Total Charges</p>
                <p class="text-xl font-bold text-gray-900">{{ totalCharges() | number:'1.2-2' }}</p>
                @if (totalCharges() !== balance()!.totalFees) {
                  <p class="text-xs text-gray-400 mt-1">₱{{ balance()!.totalFees | number:'1.2-2' }} assessed &mdash; see Ledger tab for the breakdown</p>
                }
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
            <div class="mt-4 space-y-3">
              <h4 class="text-sm font-medium text-gray-900">Payment Records</h4>
              @for (p of payments(); track p.id) {
                <div class="border rounded-lg p-4" [class]="p.status === 'Pending' ? 'border-yellow-300 bg-yellow-50/50' : p.status === 'Approved' ? 'border-emerald-200 bg-emerald-50/30' : p.status === 'Voided' ? 'border-gray-200 bg-gray-50/60 opacity-70' : 'border-red-200 bg-red-50/30'">
                  <div class="flex items-start justify-between">
                    <div class="flex-1">
                      <div class="flex items-center gap-3 mb-2">
                        <span class="text-sm font-semibold text-gray-900" [class.line-through]="p.status === 'Voided'">\u20B1{{ p.amount | number:'1.2-2' }}</span>
                        <span class="text-sm text-gray-500">via {{ p.paymentMethod }}</span>
                        @if (p.status === 'Pending') {
                          <span class="badge badge-warning">Pending Review</span>
                        } @else if (p.status === 'Approved') {
                          <span class="badge badge-success">Approved</span>
                        } @else if (p.status === 'Voided') {
                          <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Voided</span>
                        } @else {
                          <span class="badge badge-danger">Rejected</span>
                        }
                      </div>
                      <div class="flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-500">
                        <span>{{ p.paymentDate | date:'medium' }}</span>
                        @if (p.referenceNumber) { <span>Ref: <span class="folio-mono">{{ p.referenceNumber }}</span></span> }
                        @if (p.receiptFileUrl) {
                          <button type="button" (click)="openFile(p.receiptFileUrl!)" class="text-[#0038A8] font-medium hover:underline">View receipt</button>
                        } @else { <span class="text-gray-400 italic">No receipt attached</span> }
                        @if (p.remarks) { <span>{{ p.remarks }}</span> }
                        @if (p.reviewedBy) { <span>Reviewed by: {{ p.reviewedBy }}</span> }
                        @if (p.reviewNotes) { <span>Note: {{ p.reviewNotes }}</span> }
                        @if (p.voidedBy) { <span class="text-gray-600">Voided by {{ p.voidedBy }}{{ p.voidReason ? ': ' + p.voidReason : '' }}</span> }
                      </div>
                    </div>
                    @if (p.status === 'Pending') {
                      <div class="flex gap-2 ml-4 shrink-0">
                        <button (click)="approvePayment(p.id)"
                                class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 text-white hover:bg-emerald-700 transition-colors">
                          Approve
                        </button>
                        <button (click)="rejectPayment(p.id)"
                                class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-red-600 text-white hover:bg-red-700 transition-colors">
                          Reject
                        </button>
                      </div>
                    } @else if (p.status === 'Approved') {
                      <div class="flex gap-2 ml-4 shrink-0">
                        <button (click)="openPrintDoc('receipt', p.id)"
                                class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50 transition-colors">
                          Receipt
                        </button>
                        <button (click)="voidPayment(p.id)"
                                class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-red-200 text-red-600 hover:bg-red-50 transition-colors">
                          Void
                        </button>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          }
        </div>
        }

        <!-- Ledger (statement of account) -->
        @if (activeTab() === 'ledger') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <div class="flex items-center justify-between mb-4">
            <h3 class="font-semibold text-gray-900">Ledger</h3>
            @if (isAdmin()) {
              <div class="flex gap-2">
                <button (click)="openPrintDoc('soa')" class="rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-semibold text-gray-700 hover:bg-gray-50">Print SOA</button>
                <button (click)="openAdjustment()" class="rounded-lg bg-[#0038A8] px-3 py-1.5 text-xs font-semibold text-white hover:bg-[#002B85]">Add Adjustment</button>
              </div>
            }
          </div>

          @if (ledger() && ledger()!.entries.length > 0) {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-gray-200 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    <th class="py-2 pr-4">Date</th>
                    <th class="py-2 pr-4">Description</th>
                    <th class="py-2 pr-4 text-right">Debit</th>
                    <th class="py-2 pr-4 text-right">Credit</th>
                    <th class="py-2 pr-4 text-right">Balance</th>
                    <th class="py-2"></th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  @for (en of ledger()!.entries; track $index) {
                    <tr [class.opacity-60]="en.voided">
                      <td class="py-2.5 pr-4 whitespace-nowrap text-gray-500">{{ en.date | date:'mediumDate' }}</td>
                      <td class="py-2.5 pr-4">
                        <span class="text-gray-900" [class.line-through]="en.voided">{{ en.description }}</span>
                        @if (en.voided) {
                          <span class="ml-2 inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Voided</span>
                        }
                        @if (en.reference) { <p class="text-xs text-gray-400 mt-0.5">Ref: <span class="folio-mono">{{ en.reference }}</span></p> }
                      </td>
                      <td class="py-2.5 pr-4 text-right text-gray-900" [class.line-through]="en.voided">
                        @if (en.debit != null) { ₱{{ en.debit | number:'1.2-2' }} }
                      </td>
                      <td class="py-2.5 pr-4 text-right text-emerald-600" [class.line-through]="en.voided">
                        @if (en.credit != null) { ₱{{ en.credit | number:'1.2-2' }} }
                      </td>
                      <td class="py-2.5 pr-4 text-right font-medium text-gray-900">₱{{ en.balance | number:'1.2-2' }}</td>
                      <td class="py-2.5 text-right">
                        @if (isAdmin() && en.adjustmentId && !en.voided) {
                          <button (click)="voidAdjustmentEntry(en.adjustmentId!)" class="text-xs font-medium text-red-600 hover:text-red-700">Void</button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="border-t-2 border-gray-200">
                    <td colspan="2" class="py-2.5 pr-4 font-semibold text-gray-900">Totals</td>
                    <td class="py-2.5 pr-4 text-right font-semibold text-gray-900">₱{{ ledger()!.totalDebits | number:'1.2-2' }}</td>
                    <td class="py-2.5 pr-4 text-right font-semibold text-emerald-700">₱{{ ledger()!.totalCredits | number:'1.2-2' }}</td>
                    <td class="py-2.5 pr-4 text-right font-bold text-gray-900">₱{{ ledger()!.balance | number:'1.2-2' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          } @else {
            <p class="text-sm text-gray-500">Ledger opens once the enrollment is assessed.</p>
          }
        </div>
        }

        <!-- Status History -->
        @if (activeTab() === 'history') {
        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">History</h3>
          @if (history().length > 0) {
            <ol class="relative ml-2 border-l border-gray-200">
              @for (h of history(); track $index) {
                <li class="relative ml-5 pb-5 last:pb-0">
                  <span class="absolute -left-[26px] top-1 h-2.5 w-2.5 rounded-full ring-4 ring-white" style="background-color: #0038A8;"></span>
                  <p class="text-sm font-medium text-gray-900">{{ h.fromStatus }} &rarr; {{ h.toStatus }}</p>
                  <p class="text-xs text-gray-500 mt-0.5">{{ h.transitionDate | date:'medium' }}</p>
                  @if (h.remarks) {
                    <p class="text-xs italic text-gray-400 mt-0.5">{{ h.remarks }}</p>
                  }
                </li>
              }
            </ol>
          } @else {
            <p class="text-sm text-gray-500">No history recorded.</p>
          }
        </div>
        }
      }

      <!-- Add Adjustment modal -->
      @if (adjShowModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeAdjustment()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Add Adjustment</h3>
            <p class="text-xs text-gray-500 mt-1">Debits add charges to the account; credits reduce the balance (discounts, waivers, corrections).</p>

            @if (adjError()) {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ adjError() }}</div>
            }

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-2 gap-3">
                <button type="button" (click)="adjForm.type = 'Debit'"
                        class="border-2 rounded-xl p-3 text-left transition-all"
                        [class]="adjForm.type === 'Debit' ? 'border-[#0038A8] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                  <p class="text-sm font-semibold text-gray-900">Debit</p>
                  <p class="text-xs text-gray-500 mt-0.5">Additional charge</p>
                </button>
                <button type="button" (click)="adjForm.type = 'Credit'"
                        class="border-2 rounded-xl p-3 text-left transition-all"
                        [class]="adjForm.type === 'Credit' ? 'border-[#0038A8] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                  <p class="text-sm font-semibold text-gray-900">Credit</p>
                  <p class="text-xs text-gray-500 mt-0.5">Deduction / discount</p>
                </button>
              </div>
              <div>
                <label class="form-label">Description *</label>
                <input type="text" [(ngModel)]="adjForm.description" class="form-input" placeholder="e.g. Sibling discount, ID replacement fee" />
              </div>
              <div>
                <label class="form-label">Amount *</label>
                <input type="number" [(ngModel)]="adjForm.amount" class="form-input" min="0.01" placeholder="0.00" />
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeAdjustment()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveAdjustment()" [disabled]="adjSaving()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85] disabled:opacity-60">
                {{ adjSaving() ? 'Saving...' : 'Add Adjustment' }}
              </button>
            </div>
          </div>
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
  history = signal<EnrollmentHistoryItem[]>([]);
  ledger = signal<EnrollmentLedger | null>(null);

  // Tabbed workbench: workflow controls stay above the tabs; review surfaces live inside them.
  activeTab = signal<'learner' | 'requirements' | 'payments' | 'ledger' | 'history'>('payments');
  private defaultTabSet = false;
  toVerifyCount = computed(() => (this.enrollment()?.requirements ?? []).filter(r => r.isSubmitted && !r.isVerified).length);
  pendingPaymentsCount = computed(() => this.payments().filter(p => p.status === 'Pending').length);
  // Overall charges on the account: paid + outstanding reconstructs the effective
  // total (assessed ± plan discount/interest ± adjustments) with no extra API data.
  totalCharges = computed(() => {
    const b = this.balance();
    return b ? b.totalPaid + b.balance : 0;
  });

  // Add Adjustment modal state
  adjShowModal = signal(false);
  adjForm = { type: 'Debit' as 'Debit' | 'Credit', description: '', amount: 0 };
  adjError = signal('');
  adjSaving = signal(false);

  selectedSectionId = '';
  paymentAmount = 0;
  paymentMethod = 'Cash';
  paymentRef = '';
  stepError = signal('');

  reqError = signal('');
  uploadingReqId = signal<string | null>(null);

  statuses = ENROLLMENT_STEP_NAMES;

  strands = signal<ShsStrand[]>([]);
  learnerForm: EnrollmentLearnerInfo = emptyLearnerInfo();
  savingLearner = signal(false);
  readonly learnerTypes = LEARNER_TYPES;
  readonly gradeLevels = GRADE_LEVELS;
  isShs = computed(() => isShsGrade(this.enrollment()?.gradeLevel));
  learnerLocked = computed(() => !['Draft', 'Submitted'].includes(this.getStatusName()));
  missingStrand = computed(() => this.isShs() && this.strands().length > 0 && !this.enrollment()?.learnerInfo?.strand);

  private enrollmentId = '';

  constructor(private api: ApiService, private route: ActivatedRoute, private auth: AuthService, private notify: NotificationService) {}

  isAdmin(): boolean {
    const role = this.auth.userRole();
    return role === 'Admin' || role === 'Registrar';
  }

  ngOnInit(): void {
    this.enrollmentId = this.route.snapshot.paramMap.get('id')!;
    this.api.getShsStrands().subscribe(s => this.strands.set(s));
    this.loadAll(true);
  }

  /**
   * Reloads the page. The Learner tab's form is re-read from the server only on the first load and
   * after saving it — other reloads (approve, assign section, payments…) keep the registrar's unsaved edits.
   */
  loadAll(resetLearnerForm = false): void {
    this.api.getEnrollment(this.enrollmentId).subscribe(e => {
      this.enrollment.set(e);
      if (resetLearnerForm) this.learnerForm = { ...emptyLearnerInfo(), ...(e.learnerInfo ?? {}) };
      // Land the registrar where the action is — set once on first load only, so
      // reloads after approve/void/advance don't yank the user's chosen tab away.
      if (!this.defaultTabSet) {
        this.defaultTabSet = true;
        const status = this.getStatusName();
        this.activeTab.set(status === 'Draft' || status === 'Submitted' ? 'requirements' : 'payments');
      }
      this.api.getSections(e.schoolYear, e.gradeLevel).subscribe(s => this.sections.set(s));
    });
    this.api.getBalance(this.enrollmentId).subscribe(b => this.balance.set(b));
    this.api.getPayments(this.enrollmentId).subscribe(p => this.payments.set(p));
    // Old enrollments may predate history rows — tolerate errors silently (card shows empty state).
    this.api.getEnrollmentHistory(this.enrollmentId).subscribe({
      next: h => this.history.set(h),
      error: () => this.history.set([])
    });
    // Not-yet-assessed enrollments return empty entries; errors fall back to the empty state.
    this.api.getEnrollmentLedger(this.enrollmentId).subscribe({
      next: l => this.ledger.set(l),
      error: () => this.ledger.set(null)
    });
  }

  saveLearnerInfo(): void {
    this.savingLearner.set(true);
    this.api.updateEnrollmentLearnerInfo(this.enrollmentId, this.learnerForm).subscribe({
      next: () => { this.savingLearner.set(false); this.notify.success('Learner details saved.'); this.loadAll(true); },
      error: err => { this.savingLearner.set(false); this.notify.error(formatApiError(err, 'Could not save learner details.')); },
    });
  }

  openAdjustment(): void {
    this.adjError.set('');
    this.adjForm = { type: 'Debit', description: '', amount: 0 };
    this.adjShowModal.set(true);
  }

  closeAdjustment(): void { this.adjShowModal.set(false); }

  saveAdjustment(): void {
    if (!this.adjForm.description.trim()) { this.adjError.set('Description is required.'); return; }
    if (!this.adjForm.amount || this.adjForm.amount <= 0) { this.adjError.set('Amount must be greater than zero.'); return; }
    this.adjError.set('');
    this.adjSaving.set(true);
    this.api.createAdjustment(this.enrollmentId, {
      type: this.adjForm.type,
      description: this.adjForm.description.trim(),
      amount: this.adjForm.amount
    }).subscribe({
      next: () => {
        this.adjSaving.set(false);
        this.adjShowModal.set(false);
        this.notify.success('Adjustment added.');
        this.loadAll();
      },
      error: (err) => {
        this.adjSaving.set(false);
        this.adjError.set(formatApiError(err, 'Failed to add adjustment.'));
      }
    });
  }

  async voidAdjustmentEntry(adjustmentId: string): Promise<void> {
    const reason = await this.notify.prompt('Reason for voiding this adjustment:',
      { title: 'Void Adjustment', confirmLabel: 'Void', danger: true, required: true });
    if (reason === null) return;
    this.api.voidAdjustment(this.enrollmentId, adjustmentId, reason).subscribe({
      next: () => {
        this.notify.success('Adjustment voided.');
        this.loadAll();
      },
      error: (err) => this.notify.error(formatApiError(err, 'Failed to void adjustment.'))
    });
  }

  // Assessment slip makes sense once fees have been assessed (status Assessed or later).
  canPrintAssessment(): boolean {
    if (this.getStatusName() === 'Cancelled') return false;
    return this.getStepIndex() >= 2;
  }

  openPrintDoc(doc: 'cor' | 'assessment' | 'receipt' | 'soa', paymentId?: string): void {
    const url = `/print/enrollment/${this.enrollmentId}?doc=${doc}${paymentId ? `&paymentId=${paymentId}` : ''}`;
    window.open(url, '_blank');
  }

  openFile(fileUrl: string): void {
    this.api.openFile(fileUrl);
  }

  getStepIndex(): number {
    const status = this.enrollment()?.status;
    if (status == null) return 0;
    // Handle both numeric (0-6) and string ('Draft', 'Submitted', ...) status
    if (typeof status === 'number') return status;
    return ENROLLMENT_STATUS_NAMES.indexOf(status);
  }

  getStatusName(): string {
    const status = this.enrollment()?.status;
    if (status == null) return 'Draft';
    if (typeof status === 'number') return ENROLLMENT_STATUS_NAMES[status] ?? 'Draft';
    return status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-700', Submitted: 'bg-blue-100 text-blue-700',
      Assessed: 'bg-yellow-100 text-yellow-700', Approved: 'bg-purple-100 text-purple-700',
      Paid: 'bg-green-100 text-green-700', Enrolled: 'bg-emerald-100 text-emerald-800',
      Cancelled: 'bg-red-100 text-red-700'
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }

  async cancelEnrollment(): Promise<void> {
    // Cancelling waives remaining charges server-side, but money already taken in
    // stays unless payments are voided — say so when approved payments exist.
    const approvedSum = this.payments()
      .filter(p => p.status === 'Approved')
      .reduce((sum, p) => sum + p.amount, 0);
    const message = approvedSum > 0
      ? `This enrollment has ₱${approvedSum.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} in approved payments. Remaining charges will be waived on the ledger; void payments separately if money is being returned.\n\nCancel this enrollment? Enter a reason:`
      : 'Cancel this enrollment? Enter a reason:';
    const reason = await this.notify.prompt(message,
      { title: 'Cancel Enrollment', confirmLabel: 'Cancel Enrollment', cancelLabel: 'Keep Enrollment', danger: true });
    if (reason === null) return;
    this.stepError.set('');
    this.api.cancelEnrollment(this.enrollmentId, reason || undefined).subscribe({
      next: () => this.loadAll(),
      error: (err) => this.stepError.set(formatApiError(err, 'Failed to cancel enrollment.'))
    });
  }

  hasPendingPayments(): boolean {
    return this.payments().some(p => p.status === 'Pending');
  }

  hasApprovedPayments(): boolean {
    return this.payments().some(p => p.status === 'Approved');
  }

  moveStep(): void {
    this.stepError.set('');
    this.api.moveEnrollmentStep(this.enrollmentId, 'Advanced by admin').subscribe({
      next: () => this.loadAll(),
      error: (err) => this.stepError.set(formatApiError(err, 'Failed to advance step.'))
    });
  }

  assignSection(): void {
    if (!this.selectedSectionId) return;
    this.api.assignSection(this.enrollmentId, this.selectedSectionId).subscribe(() => this.loadAll());
  }

  approvePayment(paymentId: string): void {
    this.api.reviewPayment(paymentId, true, 'Payment verified and approved').subscribe(() => this.loadAll());
  }

  async rejectPayment(paymentId: string): Promise<void> {
    const reason = await this.notify.prompt('Rejection reason:',
      { title: 'Reject Payment', confirmLabel: 'Reject', danger: true });
    if (reason === null) return;
    this.api.reviewPayment(paymentId, false, reason || 'Payment rejected').subscribe(() => this.loadAll());
  }

  // Void an Approved payment. The server may roll a Paid enrollment back to Approved
  // when the void drops it below the plan threshold — loadAll() picks that up.
  async voidPayment(paymentId: string): Promise<void> {
    const reason = await this.notify.prompt('Reason for voiding this payment:',
      { title: 'Void Payment', confirmLabel: 'Void', danger: true, required: true });
    if (reason === null) return;
    this.api.voidPayment(paymentId, reason).subscribe({
      next: () => {
        this.notify.success('Payment voided.');
        this.loadAll();
      },
      error: (err) => this.notify.error(formatApiError(err, 'Failed to void payment.'))
    });
  }

  // Return an Assessed/Approved enrollment to Submitted for re-assessment.
  async sendBack(): Promise<void> {
    const reason = await this.notify.prompt(
      'Returns this enrollment to Submitted. Re-assessing will replace the fee snapshot.\n\nReason for sending back:',
      { title: 'Send Back for Re-assessment', confirmLabel: 'Send Back', required: true });
    if (reason === null) return;
    this.api.sendBackEnrollment(this.enrollmentId, reason).subscribe({
      next: () => {
        this.notify.success('Enrollment returned to Submitted for re-assessment.');
        this.loadAll();
      },
      error: (err) => this.notify.error(formatApiError(err, 'Failed to send back enrollment.'))
    });
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

  async reviewRequirement(requirementId: string, isVerified: boolean): Promise<void> {
    const promptLabel = isVerified ? 'Verification notes (optional):' : 'Rejection reason:';
    const notes = await this.notify.prompt(promptLabel, {
      title: isVerified ? 'Verify Requirement' : 'Reject Requirement',
      confirmLabel: isVerified ? 'Verify' : 'Reject',
      danger: !isVerified
    });
    if (notes === null) return;
    this.reqError.set('');
    this.api.reviewRequirement(requirementId, isVerified, notes || undefined).subscribe({
      next: () => this.loadAll(),
      error: (err) => this.reqError.set(formatApiError(err, 'Failed to review requirement.'))
    });
  }

  adminUploadFile(event: Event, requirementId: string): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.reqError.set('');
    this.uploadingReqId.set(requirementId);
    this.api.uploadFile(file).subscribe({
      next: (res) => {
        this.api.adminUploadRequirement(requirementId, res.fileName, res.fileUrl).subscribe({
          next: () => {
            this.uploadingReqId.set(null);
            input.value = '';
            this.loadAll();
          },
          error: (err) => {
            this.uploadingReqId.set(null);
            this.reqError.set(formatApiError(err, 'Failed to attach file.'));
          }
        });
      },
      error: (err) => {
        this.uploadingReqId.set(null);
        this.reqError.set(formatApiError(err, 'File upload failed.'));
      }
    });
  }
}
