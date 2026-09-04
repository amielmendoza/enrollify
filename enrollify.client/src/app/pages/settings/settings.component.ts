import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { SchoolYearService } from '../../core/services/school-year.service';
import { NotificationService } from '../../core/services/notification.service';
import { WorkflowDefinition, Fee, Section, SchoolYear, PaymentTerm, RequirementTemplate, ApplicationFormField, FormFieldType, FormFieldSection, FormFieldAppliesTo, Registrar } from '../../core/models';
import { GRADE_LEVELS } from '../../core/constants';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <h1 class="text-2xl font-bold text-gray-900">School Management</h1>
      <p class="mt-1 text-sm text-gray-500">Configure school year, fees, sections, and workflows</p>

      <!-- Tabs -->
      <div class="mt-6 border-b border-gray-200">
        <nav class="flex gap-6">
          @for (tab of tabs; track tab.id) {
            <button (click)="activeTab = tab.id"
                    class="pb-3 text-sm font-medium border-b-2 transition-colors"
                    [class]="activeTab === tab.id ? 'border-[#0038A8] text-[#0038A8]' : 'border-transparent text-gray-500 hover:text-gray-700'">
              {{ tab.label }}
            </button>
          }
        </nav>
      </div>

      <!-- School Year Tab -->
      @if (activeTab === 'school-year') {
        <div class="mt-6 space-y-6">
          <!-- Add School Year Form -->
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Add School Year</h2>
            <div class="grid grid-cols-1 sm:grid-cols-4 gap-4">
              <div>
                <label class="form-label">Name *</label>
                <input type="text" [(ngModel)]="newSchoolYear.name" class="form-input" placeholder="e.g. 2026-2027" />
              </div>
              <div>
                <label class="form-label">Start Date *</label>
                <input type="date" [(ngModel)]="newSchoolYear.startDate" class="form-input" />
              </div>
              <div>
                <label class="form-label">End Date *</label>
                <input type="date" [(ngModel)]="newSchoolYear.endDate" class="form-input" />
              </div>
              <div class="flex items-end">
                <button (click)="addSchoolYear()" [disabled]="!newSchoolYear.name || !newSchoolYear.startDate || !newSchoolYear.endDate"
                        class="w-full rounded-xl py-2.5 text-sm font-semibold text-white disabled:opacity-50 transition-colors" style="background-color: #0038A8;">
                  Add
                </button>
              </div>
            </div>
          </div>

          <!-- School Year List -->
          <div class="bg-white rounded-xl border border-[#E2D9C2]">
            <div class="p-4 border-b border-gray-100">
              <h2 class="text-lg font-semibold text-gray-900">School Years</h2>
              <p class="text-sm text-gray-500 mt-1">Manage school years and set the active one for enrollment.</p>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Start Date</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">End Date</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                    <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  @for (sy of schoolYearList(); track sy.id) {
                    <tr class="hover:bg-gray-50">
                      <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ sy.name }}</td>
                      <td class="px-6 py-4 text-sm text-gray-500">{{ sy.startDate | date:'MMM d, yyyy' }}</td>
                      <td class="px-6 py-4 text-sm text-gray-500">{{ sy.endDate | date:'MMM d, yyyy' }}</td>
                      <td class="px-6 py-4">
                        @if (sy.isActive) {
                          <span class="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-700">Active</span>
                        } @else {
                          <span class="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-500">Inactive</span>
                        }
                      </td>
                      <td class="px-6 py-4 text-right space-x-2">
                        @if (!sy.isActive) {
                          <button (click)="setActiveSchoolYear(sy.id)" class="text-xs text-[#0038A8] hover:text-[#002B85] font-medium">Set Active</button>
                          <button (click)="deleteSchoolYear(sy.id)" class="text-xs text-red-600 hover:text-red-700 font-medium">Delete</button>
                        }
                      </td>
                    </tr>
                  }
                  @empty {
                    <tr><td colspan="5" class="px-6 py-8 text-center text-gray-400">No school years configured</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Summary for active school year -->
          @if (syService.active()) {
            <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
              <h3 class="text-sm font-medium text-gray-900 mb-3">Summary for {{ syService.activeName() }}</h3>
              <div class="grid grid-cols-3 gap-4">
                <div>
                  <p class="text-xs text-gray-500">Fees Configured</p>
                  <p class="text-lg font-bold text-gray-900">{{ feesForYear().length }}</p>
                </div>
                <div>
                  <p class="text-xs text-gray-500">Sections Created</p>
                  <p class="text-lg font-bold text-gray-900">{{ sectionsForYear().length }}</p>
                </div>
                <div>
                  <p class="text-xs text-gray-500">Total Capacity</p>
                  <p class="text-lg font-bold text-gray-900">{{ totalCapacity() }}</p>
                </div>
              </div>
            </div>
          }
        </div>
      }

      <!-- Fees Tab -->
      @if (activeTab === 'fees') {
        <div class="mt-6 space-y-6">
          <!-- Add Fee Form -->
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Add Fee</h2>
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
              <div>
                <label class="form-label">Fee Name *</label>
                <input type="text" [(ngModel)]="newFee.name" class="form-input" placeholder="e.g. Tuition Fee" />
              </div>
              <div>
                <label class="form-label">Amount *</label>
                <input type="number" [(ngModel)]="newFee.amount" class="form-input" placeholder="0.00" />
              </div>
              <div>
                <label class="form-label">Grade Level *</label>
                <select [(ngModel)]="newFee.gradeLevel" class="form-input">
                  @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                </select>
              </div>
              <div>
                <label class="form-label">School Year *</label>
                <select [(ngModel)]="newFee.schoolYear" class="form-input">
                  @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
                </select>
              </div>
              <div class="flex items-end">
                <button (click)="addFee()" [disabled]="!newFee.name || !newFee.amount"
                        class="w-full rounded-xl py-2.5 text-sm font-semibold text-white disabled:opacity-50 transition-colors" style="background-color: #0038A8;">
                  Add Fee
                </button>
              </div>
            </div>
            <div class="mt-2">
              <label class="form-label">Description</label>
              <input type="text" [(ngModel)]="newFee.description" class="form-input" placeholder="Optional description" />
            </div>
          </div>

          <!-- Fee List -->
          <div class="bg-white rounded-xl border border-[#E2D9C2]">
            <div class="p-4 border-b border-gray-100 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900">Fee Schedule</h2>
              <select [(ngModel)]="feeFilter" (ngModelChange)="loadFees()" class="form-input w-auto text-sm">
                <option value="">All School Years</option>
                @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
              </select>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Fee Name</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade Level</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">School Year</th>
                    <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
                    <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  @for (f of fees(); track f.id) {
                    <tr class="hover:bg-gray-50">
                      <td class="px-6 py-4">
                        <div class="flex items-center gap-2">
                          <p class="text-sm font-medium text-gray-900">{{ f.name }}</p>
                          @if (!f.isActive) {
                            <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Inactive</span>
                          }
                        </div>
                        @if (f.description) { <p class="text-xs text-gray-500">{{ f.description }}</p> }
                      </td>
                      <td class="px-6 py-4 text-sm">{{ f.gradeLevel }}</td>
                      <td class="px-6 py-4 text-sm">{{ f.schoolYear }}</td>
                      <td class="px-6 py-4 text-sm text-right font-semibold">\u20B1{{ f.amount | number:'1.2-2' }}</td>
                      <td class="px-6 py-4 text-right space-x-2">
                        <button (click)="openEditFee(f)" class="text-xs text-[#0038A8] hover:text-[#002B85] font-medium">Edit</button>
                        <button (click)="deleteFee(f.id)" class="text-xs text-red-600 hover:text-red-700 font-medium">Delete</button>
                      </td>
                    </tr>
                  }
                  @empty {
                    <tr><td colspan="5" class="px-6 py-8 text-center text-gray-400">No fees configured</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Sections Tab -->
      @if (activeTab === 'sections') {
        <div class="mt-6 space-y-6">
          <!-- Add Section Form -->
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Add Section</h2>
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4">
              <div>
                <label class="form-label">Section Name *</label>
                <input type="text" [(ngModel)]="newSection.name" class="form-input" placeholder="e.g. Section A" />
              </div>
              <div>
                <label class="form-label">Grade Level *</label>
                <select [(ngModel)]="newSection.gradeLevel" class="form-input">
                  @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                </select>
              </div>
              <div>
                <label class="form-label">School Year *</label>
                <select [(ngModel)]="newSection.schoolYear" class="form-input">
                  @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
                </select>
              </div>
              <div>
                <label class="form-label">Capacity *</label>
                <input type="number" [(ngModel)]="newSection.capacity" class="form-input" placeholder="40" />
              </div>
              <div>
                <label class="form-label">Adviser</label>
                <input type="text" [(ngModel)]="newSection.adviser" class="form-input" placeholder="Teacher name" />
              </div>
              <div class="flex items-end">
                <button (click)="addSection()" [disabled]="!newSection.name || !newSection.capacity"
                        class="w-full rounded-xl py-2.5 text-sm font-semibold text-white disabled:opacity-50 transition-colors" style="background-color: #0038A8;">
                  Add
                </button>
              </div>
            </div>
          </div>

          <!-- Section List -->
          <div class="bg-white rounded-xl border border-[#E2D9C2]">
            <div class="p-4 border-b border-gray-100 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900">Sections</h2>
              <div class="flex gap-2">
                <select [(ngModel)]="sectionFilterGrade" (ngModelChange)="loadSections()" class="form-input w-auto text-sm">
                  <option value="">All Grades</option>
                  @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                </select>
                <select [(ngModel)]="sectionFilterYear" (ngModelChange)="loadSections()" class="form-input w-auto text-sm">
                  <option value="">All Years</option>
                  @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
                </select>
              </div>
            </div>
            <div class="p-4">
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                @for (s of sections(); track s.id) {
                  <div class="border border-gray-200 rounded-lg p-4">
                    <div class="flex items-start justify-between">
                      <div>
                        <div class="flex items-center gap-2">
                          <p class="font-medium text-gray-900">{{ s.name }}</p>
                          @if (!s.isActive) {
                            <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Inactive</span>
                          }
                        </div>
                        <p class="text-xs text-gray-500">{{ s.gradeLevel }} &bull; {{ s.schoolYear }}</p>
                      </div>
                      <div class="flex items-center">
                        <button (click)="openEditSection(s)" class="text-xs text-[#0038A8] hover:text-[#002B85] p-1" title="Edit section">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" /></svg>
                        </button>
                        <button (click)="deleteSection(s.id)" class="text-xs text-red-500 hover:text-red-700 p-1" title="Delete section">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0" /></svg>
                        </button>
                      </div>
                    </div>
                    <div class="mt-3">
                      <div class="flex justify-between text-sm mb-1">
                        <span class="text-gray-500">Enrollment</span>
                        <span class="font-medium text-gray-900">{{ s.currentCount }} / {{ s.capacity }}</span>
                      </div>
                      <div class="w-full bg-gray-200 rounded-full h-2">
                        <div class="h-2 rounded-full transition-all" style="background-color: #0038A8;" [style.width.%]="s.capacity > 0 ? (s.currentCount / s.capacity) * 100 : 0"></div>
                      </div>
                    </div>
                    @if (s.adviser) { <p class="text-xs text-gray-400 mt-2">Adviser: {{ s.adviser }}</p> }
                  </div>
                }
                @empty {
                  <div class="col-span-3 py-8 text-center text-gray-400">No sections configured</div>
                }
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Payment Terms Tab -->
      @if (activeTab === 'payment-terms') {
        <div class="mt-6 space-y-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h2 class="text-lg font-semibold text-gray-900">Payment Terms</h2>
                <p class="text-sm text-gray-500">Configure payment plan options, interest rates, and discounts per school year.</p>
              </div>
              <select [(ngModel)]="ptFilter" (ngModelChange)="loadPaymentTerms()" class="form-input w-auto text-sm">
                @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
              </select>
            </div>

            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
              <!-- Full Payment -->
              <div class="border border-gray-200 rounded-xl p-5">
                <h3 class="font-semibold text-gray-900 mb-1">Full Payment</h3>
                <p class="text-xs text-gray-500 mb-4">One-time payment with optional discount</p>
                <div class="space-y-3">
                  <div>
                    <label class="form-label">Discount (%)</label>
                    <input type="number" [(ngModel)]="ptFull.discountPercent" class="form-input" min="0" max="100" step="0.5" />
                  </div>
                </div>
                <button (click)="savePaymentTerm('Full')" class="mt-4 w-full rounded-xl py-2 text-sm font-semibold text-white transition-colors" style="background-color: #0038A8;">
                  Save
                </button>
              </div>

              <!-- Monthly -->
              <div class="border border-gray-200 rounded-xl p-5">
                <h3 class="font-semibold text-gray-900 mb-1">Monthly</h3>
                <p class="text-xs text-gray-500 mb-4">Down payment + monthly installments</p>
                <div class="space-y-3">
                  <div>
                    <label class="form-label">Down Payment (%)</label>
                    <input type="number" [(ngModel)]="ptMonthly.downPaymentPercent" class="form-input" min="0" max="100" step="1" />
                  </div>
                  <div>
                    <label class="form-label">Interest Rate (%)</label>
                    <input type="number" [(ngModel)]="ptMonthly.interestRatePercent" class="form-input" min="0" max="100" step="0.5" />
                  </div>
                  <div>
                    <label class="form-label">Number of Installments</label>
                    <input type="number" [(ngModel)]="ptMonthly.installmentCount" class="form-input" min="1" max="12" step="1" />
                  </div>
                </div>
                <button (click)="savePaymentTerm('Monthly')" class="mt-4 w-full rounded-xl py-2 text-sm font-semibold text-white transition-colors" style="background-color: #0038A8;">
                  Save
                </button>
              </div>

              <!-- Quarterly -->
              <div class="border border-gray-200 rounded-xl p-5">
                <h3 class="font-semibold text-gray-900 mb-1">Quarterly</h3>
                <p class="text-xs text-gray-500 mb-4">Down payment + quarterly installments</p>
                <div class="space-y-3">
                  <div>
                    <label class="form-label">Down Payment (%)</label>
                    <input type="number" [(ngModel)]="ptQuarterly.downPaymentPercent" class="form-input" min="0" max="100" step="1" />
                  </div>
                  <div>
                    <label class="form-label">Interest Rate (%)</label>
                    <input type="number" [(ngModel)]="ptQuarterly.interestRatePercent" class="form-input" min="0" max="100" step="0.5" />
                  </div>
                  <div>
                    <label class="form-label">Number of Installments</label>
                    <input type="number" [(ngModel)]="ptQuarterly.installmentCount" class="form-input" min="1" max="4" step="1" />
                  </div>
                </div>
                <button (click)="savePaymentTerm('Quarterly')" class="mt-4 w-full rounded-xl py-2 text-sm font-semibold text-white transition-colors" style="background-color: #0038A8;">
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Requirements Tab -->
      @if (activeTab === 'requirements') {
        <div class="mt-6 space-y-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-1">Add Requirement</h2>
            <p class="text-sm text-gray-500 mb-4">These documents are auto-attached to every new enrollment as the default checklist.</p>
            <div class="grid grid-cols-1 sm:grid-cols-12 gap-4 items-end">
              <div class="sm:col-span-6">
                <label class="form-label">Document Name *</label>
                <input type="text" [(ngModel)]="newRequirement.documentName" class="form-input" placeholder="e.g. PSA Birth Certificate" />
              </div>
              <div class="sm:col-span-3">
                <label class="form-label">Grade Level (optional)</label>
                <select [(ngModel)]="newRequirement.gradeLevel" class="form-input">
                  <option value="">All grade levels</option>
                  @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                </select>
              </div>
              <div class="sm:col-span-1">
                <label class="form-label">Order</label>
                <input type="number" [(ngModel)]="newRequirement.displayOrder" class="form-input" />
              </div>
              <div class="sm:col-span-2">
                <button (click)="addRequirementTemplate()" [disabled]="!newRequirement.documentName"
                        class="w-full rounded-xl py-2.5 text-sm font-semibold text-white disabled:opacity-50 transition-colors" style="background-color: #0038A8;">
                  Add
                </button>
              </div>
            </div>
            @if (reqError()) {
              <div class="mt-3 p-3 bg-red-50 rounded-lg text-sm text-red-700">{{ reqError() }}</div>
            }
          </div>

          <div class="bg-white rounded-xl border border-[#E2D9C2]">
            <div class="p-4 border-b border-gray-100">
              <h2 class="text-lg font-semibold text-gray-900">Requirement Templates</h2>
              <p class="text-sm text-gray-500 mt-1">Toggle Active to control which requirements seed new enrollments.</p>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-16">Order</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Document Name</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade Level</th>
                    <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                    <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100">
                  @for (t of requirementTemplates(); track t.id) {
                    <tr class="hover:bg-gray-50">
                      <td class="px-6 py-4 text-sm text-gray-500">{{ t.displayOrder }}</td>
                      <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ t.documentName }}</td>
                      <td class="px-6 py-4 text-sm text-gray-500">{{ t.gradeLevel || 'All' }}</td>
                      <td class="px-6 py-4 text-sm">
                        @if (t.isActive) {
                          <span class="badge badge-success">Active</span>
                        } @else {
                          <span class="badge bg-gray-100 text-gray-600">Inactive</span>
                        }
                      </td>
                      <td class="px-6 py-4 text-right text-sm">
                        <button (click)="toggleRequirementTemplate(t)" class="text-[#0038A8] hover:underline mr-3">
                          {{ t.isActive ? 'Deactivate' : 'Activate' }}
                        </button>
                        <button (click)="deleteRequirementTemplate(t)" class="text-red-600 hover:underline">Delete</button>
                      </td>
                    </tr>
                  }
                  @if (requirementTemplates().length === 0) {
                    <tr><td colspan="5" class="px-6 py-8 text-center text-sm text-gray-500">No requirement templates yet. Add one above.</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Workflows Tab -->
      @if (activeTab === 'workflows') {
        <div class="mt-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Enrollment Workflow</h2>
            <p class="text-sm text-gray-500 mb-4">The enrollment process follows these steps in order.</p>
            @for (w of workflows(); track w.id) {
              <div class="border border-gray-200 rounded-lg p-5">
                <div class="flex items-center justify-between mb-4">
                  <div>
                    <p class="font-semibold text-gray-900">{{ w.name }}</p>
                    <p class="text-sm text-gray-500">{{ w.description }}</p>
                  </div>
                  <span class="badge badge-success">Active</span>
                </div>
                <div class="space-y-2">
                  @for (s of w.steps; track s.id) {
                    <div class="flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
                      <div class="w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold text-white shrink-0" style="background-color: #0038A8;">{{ s.stepOrder }}</div>
                      <div class="flex-1">
                        <p class="text-sm font-medium text-gray-900">{{ s.stepName }}</p>
                        <p class="text-xs text-gray-500">{{ getStatusLabel(s.fromStatus) }} &rarr; {{ getStatusLabel(s.toStatus) }}
                          @if (s.requiredRole) { &bull; Required: {{ s.requiredRole }} }
                          @if (s.requiresApproval) { &bull; Requires Approval }
                        </p>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- Registrars Tab -->
      @if (activeTab === 'users') {
        <div class="mt-6 space-y-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-5 flex items-start justify-between gap-4 flex-wrap">
            <div>
              <h2 class="text-lg font-semibold text-gray-900">Registrars</h2>
              <p class="text-sm text-gray-500 mt-1">Registrars handle day-to-day enrollment operations: reviewing applications, verifying requirements, processing payments. They cannot change school settings.</p>
            </div>
            <button (click)="openCreateRegistrar()" class="inline-flex items-center gap-2 rounded-xl bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85]">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>
              Add Registrar
            </button>
          </div>

          @if (regError()) {
            <div class="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ regError() }}</div>
          }
          @if (regNotice()) {
            <div class="rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700">{{ regNotice() }}</div>
          }

          <div class="bg-white rounded-xl border border-[#E2D9C2] overflow-hidden">
            <table class="w-full">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
                  <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th class="px-4 py-3 text-right"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                @for (r of registrars(); track r.id) {
                  <tr class="hover:bg-gray-50" [class.opacity-60]="!r.isActive">
                    <td class="px-4 py-3 text-sm text-gray-900">{{ r.firstName }} {{ r.lastName }}</td>
                    <td class="px-4 py-3 text-sm text-gray-700">{{ r.email }}</td>
                    <td class="px-4 py-3 text-center">
                      @if (r.isActive) {
                        <span class="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">Active</span>
                      } @else {
                        <span class="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600">Inactive</span>
                      }
                    </td>
                    <td class="px-4 py-3 text-right">
                      <div class="inline-flex items-center gap-2">
                        <button (click)="openEditRegistrar(r)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Edit</button>
                        <button (click)="openResetRegistrar(r)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Reset password</button>
                      </div>
                    </td>
                  </tr>
                }
                @empty {
                  <tr><td colspan="4" class="px-4 py-12 text-center text-gray-400">No registrars yet. Add one to delegate day-to-day enrollment work.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Add / Edit Registrar modal -->
      @if (regShowModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeRegistrarModal()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">{{ regEditing() ? 'Edit Registrar' : 'Add Registrar' }}</h3>

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">First Name *</label>
                  <input type="text" [(ngModel)]="regForm.firstName" class="form-input" />
                </div>
                <div>
                  <label class="form-label">Last Name *</label>
                  <input type="text" [(ngModel)]="regForm.lastName" class="form-input" />
                </div>
              </div>
              <div>
                <label class="form-label">Email *</label>
                <input type="email" [(ngModel)]="regForm.email" class="form-input" [disabled]="!!regEditing()" />
                @if (regEditing()) { <p class="text-xs text-gray-400 mt-1">Email cannot be changed after creation.</p> }
              </div>
              @if (!regEditing()) {
                <div>
                  <label class="form-label">Initial Password *</label>
                  <input type="text" [(ngModel)]="regForm.password" class="form-input folio-mono" placeholder="At least 8 characters" />
                  <p class="text-xs text-gray-400 mt-1">Communicate this to the registrar so they can sign in and change it.</p>
                </div>
              }
              @if (regEditing()) {
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [(ngModel)]="regForm.isActive" />
                  Active (inactive registrars can't sign in)
                </label>
              }
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeRegistrarModal()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveRegistrar()" [disabled]="regSaving()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85] disabled:opacity-60">
                {{ regSaving() ? 'Saving...' : (regEditing() ? 'Save' : 'Add Registrar') }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Reset registrar password modal -->
      @if (regResetting()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeResetRegistrar()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Reset Password</h3>
            <p class="mt-1 text-sm text-gray-500">For <span class="font-medium">{{ regResetting()!.firstName }} {{ regResetting()!.lastName }}</span> ({{ regResetting()!.email }}).</p>

            <div class="mt-4">
              <label class="form-label">New Password *</label>
              <input type="text" [(ngModel)]="regResetPassword" class="form-input folio-mono" placeholder="At least 8 characters" />
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeResetRegistrar()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="confirmResetRegistrar()" [disabled]="regSaving()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85] disabled:opacity-60">
                {{ regSaving() ? 'Saving...' : 'Reset Password' }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Application Form Tab -->
      @if (activeTab === 'application-form') {
        <div class="mt-6 space-y-6">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-5 flex items-start justify-between gap-4 flex-wrap">
            <div>
              <h2 class="text-lg font-semibold text-gray-900">Application Form Fields</h2>
              <p class="text-sm text-gray-500 mt-1">Control which fields appear on the public /apply form. Toggle visibility and required state, rename labels, and add custom fields. Default fields can be hidden but not deleted.</p>
            </div>
            <div class="flex items-center gap-2">
              <button (click)="restoreDefaults()" [disabled]="restoringDefaults()"
                      class="inline-flex items-center gap-2 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
                @if (restoringDefaults()) {
                  <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                }
                Restore Defaults
              </button>
              <button (click)="openAddField()" class="inline-flex items-center gap-2 rounded-xl bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85]">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>
                Add Custom Field
              </button>
            </div>
          </div>

          @if (formFields().length === 0) {
            <div class="rounded-lg bg-blue-50 border border-blue-200 p-3 text-sm text-blue-800">
              No fields are configured for your school yet. Click <span class="font-semibold">Restore Defaults</span> to seed the standard set (parent / student / enrollment / guardian).
            </div>
          }

          @if (ffError()) {
            <div class="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ ffError() }}</div>
          }

          @for (group of formFieldsBySection(); track group.section) {
            <div class="bg-white rounded-xl border border-[#E2D9C2]">
              <div class="p-4 border-b border-gray-100 flex items-center gap-2">
                <h3 class="text-base font-semibold text-gray-900">{{ group.section }} section</h3>
                <span class="text-xs text-gray-500">({{ group.fields.length }} {{ group.fields.length === 1 ? 'field' : 'fields' }})</span>
              </div>
              @if (group.fields.length === 0) {
                <p class="px-4 py-6 text-sm text-gray-400 text-center">No configured fields in this section yet.</p>
              } @else {
                <div class="divide-y divide-gray-100">
                  @for (f of group.fields; track f.id) {
                    <div class="p-4 flex items-start gap-4 flex-wrap">
                      <div class="flex-1 min-w-[200px]">
                        <div class="flex items-center gap-2">
                          <p class="font-medium text-gray-900">{{ f.label }}</p>
                          @if (!f.isBuiltIn) {
                            <span class="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">Custom</span>
                          }
                        </div>
                        <p class="text-xs text-gray-500 mt-0.5">
                          <span class="folio-mono">{{ f.fieldKey }}</span>
                          &middot; {{ f.fieldType }}
                          &middot; Applies: {{ f.appliesTo }}
                        </p>
                        @if (f.helpText) {
                          <p class="text-xs text-gray-400 mt-1">{{ f.helpText }}</p>
                        }
                      </div>
                      <div class="flex items-center gap-3">
                        <label class="inline-flex items-center gap-1.5 text-xs text-gray-600" [class.opacity-50]="f.isCore" [title]="f.isCore ? 'This field is required by the system and can\\'t be hidden.' : ''">
                          <input type="checkbox" [checked]="f.isVisible" [disabled]="f.isCore" (change)="toggleVisible(f)" />
                          Visible
                        </label>
                        <label class="inline-flex items-center gap-1.5 text-xs text-gray-600" [class.opacity-50]="f.isCore" [title]="f.isCore ? 'This field is required by the system and can\\'t be made optional.' : ''">
                          <input type="checkbox" [checked]="f.isRequired" [disabled]="f.isCore" (change)="toggleRequired(f)" />
                          Required
                        </label>
                        <button (click)="openEditField(f)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Edit</button>
                        @if (!f.isBuiltIn) {
                          <button (click)="deleteField(f)" class="text-xs px-3 py-1.5 rounded-lg border border-red-200 text-red-600 hover:bg-red-50">Delete</button>
                        }
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Add Custom Field Modal -->
      @if (ffShowAddModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeAddField()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Add Custom Field</h3>
            <p class="text-xs text-gray-500 mt-1">Fields you add appear on /apply and store their values per application.</p>

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">Label *</label>
                  <input type="text" [(ngModel)]="ffNew.label" class="form-input" placeholder="Allergies" />
                </div>
                <div>
                  <label class="form-label">Field Key *</label>
                  <input type="text" [(ngModel)]="ffNew.fieldKey" class="form-input folio-mono" placeholder="allergies" />
                  <p class="text-xs text-gray-400 mt-1">Lowercase letters, digits, underscores only.</p>
                </div>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label class="form-label">Type *</label>
                  <select [(ngModel)]="ffNew.fieldType" class="form-input">
                    @for (t of ffFieldTypes; track t) { <option [value]="t">{{ t }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">Section *</label>
                  <select [(ngModel)]="ffNew.section" class="form-input">
                    @for (s of ffSections; track s) { <option [value]="s">{{ s }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">Applies To</label>
                  <select [(ngModel)]="ffNew.appliesTo" class="form-input">
                    @for (a of ffAppliesToOptions; track a) { <option [value]="a">{{ a }}</option> }
                  </select>
                </div>
              </div>
              @if (ffNew.fieldType === 'Dropdown') {
                <div>
                  <label class="form-label">Options *</label>
                  <input type="text" [(ngModel)]="ffNew.options" class="form-input" placeholder="Option A, Option B, Option C" />
                  <p class="text-xs text-gray-400 mt-1">Comma-separated list.</p>
                </div>
              }
              <div>
                <label class="form-label">Help Text</label>
                <input type="text" [(ngModel)]="ffNew.helpText" class="form-input" placeholder="(optional) hint shown below the input" />
              </div>
              <div class="flex items-center gap-4">
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [(ngModel)]="ffNew.isVisible" />
                  Visible
                </label>
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [(ngModel)]="ffNew.isRequired" />
                  Required
                </label>
                <div class="ml-auto">
                  <label class="form-label">Order</label>
                  <input type="number" [(ngModel)]="ffNew.displayOrder" class="form-input w-24" />
                </div>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeAddField()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveNewField()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85]">Add Field</button>
            </div>
          </div>
        </div>
      }

      <!-- Edit Field Modal -->
      @if (ffEditing()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeEditField()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Edit Field</h3>
            <p class="text-xs text-gray-500 mt-1">
              @if (ffEditing()!.isCore) {
                This is a system field — you can rename it and reorder it, but it can't be hidden or made optional.
              } @else if (ffEditing()!.isBuiltIn) {
                You can rename, reorder, and toggle visibility/required state.
              } @else {
                Custom field — every property is editable. Changing the field key may break previously-stored values.
              }
            </p>

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">Label *</label>
                  <input type="text" [(ngModel)]="ffEditing()!.label" class="form-input" />
                </div>
                <div>
                  <label class="form-label">Field Key</label>
                  <input type="text" [ngModel]="ffEditing()!.fieldKey" (ngModelChange)="ffEditing()!.fieldKey = $event"
                         class="form-input folio-mono"
                         [disabled]="ffEditing()!.isBuiltIn" />
                </div>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label class="form-label">Type</label>
                  <select [(ngModel)]="ffEditing()!.fieldType" class="form-input" [disabled]="ffEditing()!.isBuiltIn">
                    @for (t of ffFieldTypes; track t) { <option [value]="t">{{ t }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">Section</label>
                  <select [(ngModel)]="ffEditing()!.section" class="form-input" [disabled]="ffEditing()!.isBuiltIn">
                    @for (s of ffSections; track s) { <option [value]="s">{{ s }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">Applies To</label>
                  <select [(ngModel)]="ffEditing()!.appliesTo" class="form-input" [disabled]="ffEditing()!.isBuiltIn">
                    @for (a of ffAppliesToOptions; track a) { <option [value]="a">{{ a }}</option> }
                  </select>
                </div>
              </div>
              @if (ffEditing()!.fieldType === 'Dropdown') {
                <div>
                  <label class="form-label">Options *</label>
                  <input type="text" [ngModel]="ffEditOptions()" (ngModelChange)="ffEditOptions.set($event)" class="form-input" placeholder="Option A, Option B" />
                  <p class="text-xs text-gray-400 mt-1">Comma-separated list. Edit freely — admins can add or remove options for built-in dropdowns too.</p>
                </div>
              }
              <div>
                <label class="form-label">Help Text</label>
                <input type="text" [ngModel]="ffEditing()!.helpText" (ngModelChange)="ffEditing()!.helpText = $event" class="form-input" />
              </div>
              <div class="flex items-center gap-4">
                <label class="inline-flex items-center gap-2 text-sm text-gray-700" [class.opacity-50]="ffEditing()!.isCore">
                  <input type="checkbox" [(ngModel)]="ffEditing()!.isVisible" [disabled]="ffEditing()!.isCore" />
                  Visible
                </label>
                <label class="inline-flex items-center gap-2 text-sm text-gray-700" [class.opacity-50]="ffEditing()!.isCore">
                  <input type="checkbox" [(ngModel)]="ffEditing()!.isRequired" [disabled]="ffEditing()!.isCore" />
                  Required
                </label>
                <div class="ml-auto">
                  <label class="form-label">Order</label>
                  <input type="number" [(ngModel)]="ffEditing()!.displayOrder" class="form-input w-24" />
                </div>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeEditField()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveEditField()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85]">Save</button>
            </div>
          </div>
        </div>
      }

      <!-- Edit Fee Modal -->
      @if (feeEditing()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeEditFee()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Edit Fee</h3>

            @if (feeEditError()) {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ feeEditError() }}</div>
            }

            <div class="mt-5 space-y-4">
              <div>
                <label class="form-label">Fee Name *</label>
                <input type="text" [(ngModel)]="feeEditForm.name" class="form-input" placeholder="e.g. Tuition Fee" />
              </div>
              <div>
                <label class="form-label">Description</label>
                <input type="text" [(ngModel)]="feeEditForm.description" class="form-input" placeholder="Optional description" />
              </div>
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label class="form-label">Amount *</label>
                  <input type="number" [(ngModel)]="feeEditForm.amount" class="form-input" placeholder="0.00" />
                </div>
                <div>
                  <label class="form-label">Grade Level *</label>
                  <select [(ngModel)]="feeEditForm.gradeLevel" class="form-input">
                    @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">School Year *</label>
                  <select [(ngModel)]="feeEditForm.schoolYear" class="form-input">
                    @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
                  </select>
                </div>
              </div>
              <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                <input type="checkbox" [(ngModel)]="feeEditForm.isActive" />
                Active (inactive fees are excluded from new assessments)
              </label>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeEditFee()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveEditFee()" [disabled]="feeEditSaving()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85] disabled:opacity-60">
                {{ feeEditSaving() ? 'Saving...' : 'Save' }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Edit Section Modal -->
      @if (sectionEditing()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeEditSection()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Edit Section</h3>

            @if (sectionEditError()) {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ sectionEditError() }}</div>
            }

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">Section Name *</label>
                  <input type="text" [(ngModel)]="sectionEditForm.name" class="form-input" placeholder="e.g. Section A" />
                </div>
                <div>
                  <label class="form-label">Capacity *</label>
                  <input type="number" [(ngModel)]="sectionEditForm.capacity" class="form-input" placeholder="40" />
                </div>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">Grade Level *</label>
                  <select [(ngModel)]="sectionEditForm.gradeLevel" class="form-input">
                    @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                  </select>
                </div>
                <div>
                  <label class="form-label">School Year *</label>
                  <select [(ngModel)]="sectionEditForm.schoolYear" class="form-input">
                    @for (sy of schoolYearNames(); track sy) { <option [value]="sy">{{ sy }}</option> }
                  </select>
                </div>
              </div>
              <div>
                <label class="form-label">Adviser</label>
                <input type="text" [(ngModel)]="sectionEditForm.adviser" class="form-input" placeholder="Teacher name" />
              </div>
              <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                <input type="checkbox" [(ngModel)]="sectionEditForm.isActive" />
                Active (inactive sections can't receive new enrollees)
              </label>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeEditSection()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveEditSection()" [disabled]="sectionEditSaving()" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85] disabled:opacity-60">
                {{ sectionEditSaving() ? 'Saving...' : 'Save' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SettingsComponent implements OnInit {
  workflows = signal<WorkflowDefinition[]>([]);
  fees = signal<Fee[]>([]);
  sections = signal<Section[]>([]);
  schoolYearList = signal<SchoolYear[]>([]);
  requirementTemplates = signal<RequirementTemplate[]>([]);
  newRequirement = { documentName: '', gradeLevel: '', displayOrder: 0 };
  reqError = signal('');

  activeTab = 'school-year';
  tabs = [
    { id: 'school-year', label: 'School Year' },
    { id: 'fees', label: 'Fees' },
    { id: 'sections', label: 'Sections' },
    { id: 'payment-terms', label: 'Payment Terms' },
    { id: 'requirements', label: 'Requirements' },
    { id: 'application-form', label: 'Application Form' },
    { id: 'users', label: 'Registrars' },
    { id: 'workflows', label: 'Workflows' },
  ];

  // Registrars tab state
  registrars = signal<Registrar[]>([]);
  regShowModal = signal(false);
  regEditing = signal<Registrar | null>(null);
  regSaving = signal(false);
  regError = signal('');
  regNotice = signal('');
  regForm = { email: '', firstName: '', lastName: '', password: '', isActive: true };
  regResetting = signal<Registrar | null>(null);
  regResetPassword = '';

  // Application Form tab state
  formFields = signal<ApplicationFormField[]>([]);
  formFieldsBySection = computed(() => {
    const order: FormFieldSection[] = ['Parent', 'Student', 'Enrollment', 'Guardian'];
    const grouped = new Map<FormFieldSection, ApplicationFormField[]>();
    for (const sec of order) grouped.set(sec, []);
    for (const f of this.formFields()) grouped.get(f.section)?.push(f);
    return order.map(sec => ({ section: sec, fields: grouped.get(sec) ?? [] }));
  });
  ffEditing = signal<ApplicationFormField | null>(null);
  ffEditOptions = signal('');               // editable comma-separated options text for Dropdown
  ffShowAddModal = signal(false);
  restoringDefaults = signal(false);
  ffNew = {
    fieldKey: '',
    label: '',
    fieldType: 'Text' as FormFieldType,
    section: 'Student' as FormFieldSection,
    appliesTo: 'Both' as FormFieldAppliesTo,
    isRequired: false,
    isVisible: true,
    displayOrder: 100,
    options: '',
    helpText: ''
  };
  ffError = signal('');
  ffFieldTypes: FormFieldType[] = ['Text', 'TextArea', 'Number', 'Date', 'Checkbox', 'Dropdown'];
  ffSections: FormFieldSection[] = ['Parent', 'Student', 'Enrollment', 'Guardian'];
  ffAppliesToOptions: FormFieldAppliesTo[] = ['Both', 'ParentMode', 'StudentMode'];

  schoolYearNames = computed(() => this.schoolYearList().map(sy => sy.name));
  gradeLevels = GRADE_LEVELS;

  feeFilter = '';
  sectionFilterGrade = '';
  sectionFilterYear = '';

  ptFilter = '';
  ptFull = { discountPercent: 0 };
  ptMonthly = { downPaymentPercent: 20, interestRatePercent: 0, installmentCount: 9 };
  ptQuarterly = { downPaymentPercent: 30, interestRatePercent: 0, installmentCount: 3 };

  newFee = { name: '', description: '', amount: 0, gradeLevel: 'Grade 7', schoolYear: '' };
  newSection = { name: '', gradeLevel: 'Grade 7', schoolYear: '', capacity: 40, adviser: '' };
  newSchoolYear = { name: '', startDate: '', endDate: '' };

  // Fee / Section edit modal state
  feeEditing = signal<Fee | null>(null);
  feeEditForm = { name: '', description: '', amount: 0, gradeLevel: 'Grade 7', schoolYear: '', isActive: true };
  feeEditError = signal('');
  feeEditSaving = signal(false);
  sectionEditing = signal<Section | null>(null);
  sectionEditForm = { name: '', gradeLevel: 'Grade 7', schoolYear: '', capacity: 40, adviser: '', isActive: true };
  sectionEditError = signal('');
  sectionEditSaving = signal(false);

  private statusLabels: Record<number | string, string> = {
    0: 'Draft', 1: 'Submitted', 2: 'Assessed', 3: 'Approved', 4: 'Paid', 5: 'Enrolled',
    Draft: 'Draft', Submitted: 'Submitted', Assessed: 'Assessed', Approved: 'Approved', Paid: 'Paid', Enrolled: 'Enrolled'
  };

  constructor(private api: ApiService, public syService: SchoolYearService, private notify: NotificationService) {}

  ngOnInit() {
    this.loadSchoolYears();
    this.api.getWorkflows().subscribe(w => {
      w.forEach(wf => wf.steps.sort((a, b) => a.stepOrder - b.stepOrder));
      this.workflows.set(w);
    });
    this.loadRequirementTemplates();
    this.loadFormFields();
    this.loadRegistrars();
  }

  loadRequirementTemplates() {
    this.api.getRequirementTemplates().subscribe(list => this.requirementTemplates.set(list));
  }

  addRequirementTemplate() {
    if (!this.newRequirement.documentName.trim()) return;
    this.reqError.set('');
    this.api.createRequirementTemplate({
      documentName: this.newRequirement.documentName.trim(),
      gradeLevel: this.newRequirement.gradeLevel || null,
      displayOrder: this.newRequirement.displayOrder || 0
    }).subscribe({
      next: () => {
        this.newRequirement = { documentName: '', gradeLevel: '', displayOrder: 0 };
        this.loadRequirementTemplates();
      },
      error: (err) => this.reqError.set(err.error?.error || 'Failed to add requirement template.')
    });
  }

  toggleRequirementTemplate(t: RequirementTemplate) {
    this.api.updateRequirementTemplate(t.id, {
      documentName: t.documentName,
      gradeLevel: t.gradeLevel,
      isActive: !t.isActive,
      displayOrder: t.displayOrder
    }).subscribe(() => this.loadRequirementTemplates());
  }

  async deleteRequirementTemplate(t: RequirementTemplate) {
    if (!(await this.notify.confirm(`Delete "${t.documentName}" from the requirements catalog?`, { title: 'Delete Requirement', confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteRequirementTemplate(t.id).subscribe(() => this.loadRequirementTemplates());
  }

  loadSchoolYears() {
    this.api.getSchoolYears().subscribe(list => {
      this.schoolYearList.set(list);
      const activeName = list.find(sy => sy.isActive)?.name ?? '';
      // Default filters and forms to active school year
      if (!this.feeFilter) this.feeFilter = activeName;
      if (!this.sectionFilterYear) this.sectionFilterYear = activeName;
      if (!this.newFee.schoolYear) this.newFee.schoolYear = activeName;
      if (!this.newSection.schoolYear) this.newSection.schoolYear = activeName;
      if (!this.ptFilter) this.ptFilter = activeName;
      // Reload with active year filter
      this.loadFees();
      this.loadSections();
      this.loadPaymentTerms();
    });
  }

  loadFees() {
    // includeInactive so admins can still find and re-activate deactivated fees.
    this.api.getFees(this.feeFilter || undefined, undefined, true).subscribe(f => this.fees.set(f));
  }

  loadSections() {
    // includeInactive so admins can still find and re-activate deactivated sections.
    this.api.getSections(this.sectionFilterYear || undefined, this.sectionFilterGrade || undefined, true).subscribe(s => this.sections.set(s));
  }

  // Summary counts only active rows — the lists include inactive ones for re-activation.
  feesForYear() { return this.fees().filter(f => f.isActive && f.schoolYear === this.syService.activeName()); }
  sectionsForYear() { return this.sections().filter(s => s.isActive && s.schoolYear === this.syService.activeName()); }
  totalCapacity() { return this.sectionsForYear().reduce((sum, s) => sum + s.capacity, 0); }

  getStatusLabel(status: number | string): string { return this.statusLabels[status] ?? String(status); }

  addSchoolYear() {
    if (!this.newSchoolYear.name || !this.newSchoolYear.startDate || !this.newSchoolYear.endDate) return;
    this.api.createSchoolYear(this.newSchoolYear).subscribe({
      next: () => {
        this.newSchoolYear = { name: '', startDate: '', endDate: '' };
        this.loadSchoolYears();
        this.syService.refresh();
      },
      error: (err) => this.notify.error(err.error?.error || 'Failed to create school year')
    });
  }

  setActiveSchoolYear(id: string) {
    this.api.setActiveSchoolYear(id).subscribe(() => {
      this.feeFilter = '';
      this.sectionFilterYear = '';
      this.newFee.schoolYear = '';
      this.newSection.schoolYear = '';
      this.loadSchoolYears();
      this.syService.refresh();
    });
  }

  async deleteSchoolYear(id: string) {
    if (!(await this.notify.confirm('Delete this school year?', { title: 'Delete School Year', confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteSchoolYear(id).subscribe({
      next: () => {
        this.loadSchoolYears();
        this.syService.refresh();
      },
      error: (err) => this.notify.error(err.error?.error || 'Cannot delete this school year')
    });
  }

  addFee() {
    if (!this.newFee.name || !this.newFee.amount) return;
    this.api.createFee(this.newFee as any).subscribe(() => {
      this.newFee = { name: '', description: '', amount: 0, gradeLevel: this.newFee.gradeLevel, schoolYear: this.newFee.schoolYear };
      this.loadFees();
    });
  }

  async deleteFee(id: string) {
    if (!(await this.notify.confirm('Delete this fee?', { title: 'Delete Fee', confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteFee(id).subscribe(() => this.loadFees());
  }

  openEditFee(f: Fee) {
    this.feeEditError.set('');
    this.feeEditForm = {
      name: f.name, description: f.description ?? '', amount: f.amount,
      gradeLevel: f.gradeLevel, schoolYear: f.schoolYear, isActive: f.isActive
    };
    this.feeEditing.set(f);
  }

  closeEditFee() { this.feeEditing.set(null); }

  saveEditFee() {
    const target = this.feeEditing();
    if (!target) return;
    if (!this.feeEditForm.name || !this.feeEditForm.amount) {
      this.feeEditError.set('Fee name and amount are required.');
      return;
    }
    this.feeEditError.set('');
    this.feeEditSaving.set(true);
    this.api.updateFee(target.id, {
      name: this.feeEditForm.name,
      description: this.feeEditForm.description || null,
      amount: this.feeEditForm.amount,
      schoolYear: this.feeEditForm.schoolYear,
      gradeLevel: this.feeEditForm.gradeLevel,
      isActive: this.feeEditForm.isActive
    }).subscribe({
      next: () => {
        this.feeEditSaving.set(false);
        this.feeEditing.set(null);
        this.loadFees();
      },
      error: (err) => {
        this.feeEditSaving.set(false);
        this.feeEditError.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to update fee.');
      }
    });
  }

  addSection() {
    if (!this.newSection.name || !this.newSection.capacity) return;
    this.api.createSection(this.newSection as any).subscribe(() => {
      this.newSection = { name: '', gradeLevel: this.newSection.gradeLevel, schoolYear: this.newSection.schoolYear, capacity: 40, adviser: '' };
      this.loadSections();
    });
  }

  async deleteSection(id: string) {
    if (!(await this.notify.confirm('Delete this section?', { title: 'Delete Section', confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteSection(id).subscribe(() => this.loadSections());
  }

  openEditSection(s: Section) {
    this.sectionEditError.set('');
    this.sectionEditForm = {
      name: s.name, gradeLevel: s.gradeLevel, schoolYear: s.schoolYear,
      capacity: s.capacity, adviser: s.adviser ?? '', isActive: s.isActive
    };
    this.sectionEditing.set(s);
  }

  closeEditSection() { this.sectionEditing.set(null); }

  saveEditSection() {
    const target = this.sectionEditing();
    if (!target) return;
    if (!this.sectionEditForm.name || !this.sectionEditForm.capacity) {
      this.sectionEditError.set('Section name and capacity are required.');
      return;
    }
    this.sectionEditError.set('');
    this.sectionEditSaving.set(true);
    this.api.updateSection(target.id, {
      name: this.sectionEditForm.name,
      gradeLevel: this.sectionEditForm.gradeLevel,
      schoolYear: this.sectionEditForm.schoolYear,
      capacity: this.sectionEditForm.capacity,
      adviser: this.sectionEditForm.adviser || null,
      isActive: this.sectionEditForm.isActive
    }).subscribe({
      next: () => {
        this.sectionEditSaving.set(false);
        this.sectionEditing.set(null);
        this.loadSections();
      },
      error: (err) => {
        this.sectionEditSaving.set(false);
        this.sectionEditError.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to update section.');
      }
    });
  }

  loadPaymentTerms() {
    if (!this.ptFilter) return;
    this.api.getPaymentTerms(this.ptFilter).subscribe(terms => {
      const full = terms.find(t => t.planType === 'Full');
      const monthly = terms.find(t => t.planType === 'Monthly');
      const quarterly = terms.find(t => t.planType === 'Quarterly');
      this.ptFull = { discountPercent: full?.discountPercent ?? 0 };
      this.ptMonthly = { downPaymentPercent: monthly?.downPaymentPercent ?? 20, interestRatePercent: monthly?.interestRatePercent ?? 0, installmentCount: monthly?.installmentCount ?? 9 };
      this.ptQuarterly = { downPaymentPercent: quarterly?.downPaymentPercent ?? 30, interestRatePercent: quarterly?.interestRatePercent ?? 0, installmentCount: quarterly?.installmentCount ?? 3 };
    });
  }

  savePaymentTerm(planType: string) {
    const data = planType === 'Full'
      ? { schoolYear: this.ptFilter, planType, downPaymentPercent: 0, interestRatePercent: 0, discountPercent: this.ptFull.discountPercent, installmentCount: 1 }
      : planType === 'Monthly'
        ? { schoolYear: this.ptFilter, planType, downPaymentPercent: this.ptMonthly.downPaymentPercent, interestRatePercent: this.ptMonthly.interestRatePercent, discountPercent: 0, installmentCount: this.ptMonthly.installmentCount }
        : { schoolYear: this.ptFilter, planType, downPaymentPercent: this.ptQuarterly.downPaymentPercent, interestRatePercent: this.ptQuarterly.interestRatePercent, discountPercent: 0, installmentCount: this.ptQuarterly.installmentCount };
    this.api.savePaymentTerm(data).subscribe({
      next: () => this.loadPaymentTerms(),
      error: (err) => this.notify.error(err.error?.error || 'Failed to save')
    });
  }

  // ----- Application Form Fields -----

  loadFormFields() {
    this.api.getApplicationFormFields().subscribe(list => this.formFields.set(list));
  }

  restoreDefaults() {
    this.restoringDefaults.set(true);
    this.ffError.set('');
    this.api.restoreDefaultApplicationFormFields().subscribe({
      next: (res) => {
        this.restoringDefaults.set(false);
        if (res.added === 0) {
          this.ffError.set('All default fields are already in place. Nothing to restore.');
        }
        this.loadFormFields();
      },
      error: (err) => {
        this.restoringDefaults.set(false);
        this.ffError.set(err.error?.error || 'Failed to restore defaults.');
      }
    });
  }

  toggleVisible(f: ApplicationFormField) {
    if (f.isCore) return;  // Core fields are always visible — UI also disables the checkbox
    this.persistField({ ...f, isVisible: !f.isVisible });
  }

  toggleRequired(f: ApplicationFormField) {
    if (f.isCore) return;  // Core fields are always required
    this.persistField({ ...f, isRequired: !f.isRequired });
  }

  openAddField() {
    this.ffError.set('');
    this.ffNew = {
      fieldKey: '', label: '', fieldType: 'Text', section: 'Student',
      appliesTo: 'Both', isRequired: false, isVisible: true,
      displayOrder: 100, options: '', helpText: ''
    };
    this.ffShowAddModal.set(true);
  }

  closeAddField() { this.ffShowAddModal.set(false); }

  saveNewField() {
    this.ffError.set('');
    if (!this.ffNew.label || !this.ffNew.fieldKey) {
      this.ffError.set('Label and field key are required.');
      return;
    }
    if (!/^[a-z][a-zA-Z0-9_]*$/.test(this.ffNew.fieldKey)) {
      this.ffError.set('Field key must start with a lowercase letter and contain only letters, digits, and underscores.');
      return;
    }
    const optionsJson = this.ffNew.fieldType === 'Dropdown'
      ? JSON.stringify(this.parseCsv(this.ffNew.options))
      : null;
    this.api.createApplicationFormField({
      fieldKey: this.ffNew.fieldKey,
      label: this.ffNew.label,
      fieldType: this.ffNew.fieldType,
      section: this.ffNew.section,
      appliesTo: this.ffNew.appliesTo,
      isRequired: this.ffNew.isRequired,
      isVisible: this.ffNew.isVisible,
      displayOrder: this.ffNew.displayOrder,
      options: optionsJson,
      helpText: this.ffNew.helpText || null
    }).subscribe({
      next: () => { this.ffShowAddModal.set(false); this.loadFormFields(); },
      error: (err) => this.ffError.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to add field.')
    });
  }

  openEditField(f: ApplicationFormField) {
    this.ffError.set('');
    // Edit a copy so cancel discards changes
    this.ffEditing.set({ ...f });
    let optsText = '';
    if (f.fieldType === 'Dropdown' && f.options) {
      try {
        const parsed = JSON.parse(f.options);
        if (Array.isArray(parsed)) optsText = parsed.join(', ');
      } catch { optsText = ''; }
    }
    this.ffEditOptions.set(optsText);
  }

  closeEditField() { this.ffEditing.set(null); }

  saveEditField() {
    const draft = this.ffEditing();
    if (!draft) return;
    if (!draft.label) { this.ffError.set('Label is required.'); return; }
    if (draft.fieldType === 'Dropdown') {
      draft.options = JSON.stringify(this.parseCsv(this.ffEditOptions()));
    }
    this.persistField(draft);
  }

  async deleteField(f: ApplicationFormField) {
    if (f.isBuiltIn) return;
    if (!(await this.notify.confirm(`Delete custom field "${f.label}"? Stored values for this field will remain on existing applications but will no longer be displayed.`, { title: 'Delete Custom Field', confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteApplicationFormField(f.id).subscribe({
      next: () => this.loadFormFields(),
      error: (err) => this.ffError.set(err.error?.error || 'Failed to delete field.')
    });
  }

  private persistField(f: ApplicationFormField) {
    this.ffError.set('');
    this.api.updateApplicationFormField(f.id, {
      fieldKey: f.fieldKey, label: f.label, fieldType: f.fieldType,
      section: f.section, appliesTo: f.appliesTo,
      isRequired: f.isRequired, isVisible: f.isVisible,
      displayOrder: f.displayOrder, options: f.options, helpText: f.helpText
    }).subscribe({
      next: () => { this.ffEditing.set(null); this.loadFormFields(); },
      error: (err) => this.ffError.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to save field.')
    });
  }

  private parseCsv(text: string): string[] {
    return (text || '').split(',').map(s => s.trim()).filter(s => s.length > 0);
  }

  // ----- Registrars -----

  loadRegistrars() {
    this.api.getRegistrars().subscribe(list => this.registrars.set(list));
  }

  openCreateRegistrar() {
    this.regEditing.set(null);
    this.regForm = { email: '', firstName: '', lastName: '', password: '', isActive: true };
    this.regError.set(''); this.regNotice.set('');
    this.regShowModal.set(true);
  }

  openEditRegistrar(r: Registrar) {
    this.regEditing.set(r);
    this.regForm = { email: r.email, firstName: r.firstName, lastName: r.lastName, password: '', isActive: r.isActive };
    this.regError.set(''); this.regNotice.set('');
    this.regShowModal.set(true);
  }

  closeRegistrarModal() { this.regShowModal.set(false); }

  saveRegistrar() {
    this.regError.set('');
    if (!this.regForm.firstName || !this.regForm.lastName) { this.regError.set('Name is required.'); return; }
    if (!this.regEditing() && (!this.regForm.email || !this.regForm.password)) { this.regError.set('Email and initial password are required.'); return; }
    if (!this.regEditing() && this.regForm.password.length < 8) { this.regError.set('Password must be at least 8 characters.'); return; }

    this.regSaving.set(true);
    const target = this.regEditing();
    const obs = target
      ? this.api.updateRegistrar(target.id, {
          firstName: this.regForm.firstName, lastName: this.regForm.lastName, isActive: this.regForm.isActive
        })
      : this.api.createRegistrar({
          email: this.regForm.email, firstName: this.regForm.firstName, lastName: this.regForm.lastName, password: this.regForm.password
        });

    obs.subscribe({
      next: () => { this.regSaving.set(false); this.regShowModal.set(false); this.regNotice.set(target ? 'Registrar updated.' : 'Registrar created.'); this.loadRegistrars(); },
      error: (err) => {
        this.regSaving.set(false);
        this.regError.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to save registrar.');
      }
    });
  }

  openResetRegistrar(r: Registrar) {
    this.regResetting.set(r);
    this.regResetPassword = '';
    this.regError.set(''); this.regNotice.set('');
  }

  closeResetRegistrar() { this.regResetting.set(null); }

  confirmResetRegistrar() {
    const r = this.regResetting();
    if (!r) return;
    if (!this.regResetPassword || this.regResetPassword.length < 8) { this.regError.set('Password must be at least 8 characters.'); return; }
    this.regSaving.set(true);
    this.api.resetRegistrarPassword(r.id, this.regResetPassword).subscribe({
      next: () => { this.regSaving.set(false); this.regResetting.set(null); this.regNotice.set(`Password reset for ${r.email}.`); },
      error: (err) => { this.regSaving.set(false); this.regError.set(err.error?.error || 'Failed to reset password.'); }
    });
  }
}
