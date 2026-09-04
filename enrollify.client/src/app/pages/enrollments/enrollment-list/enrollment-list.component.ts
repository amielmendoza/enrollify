import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { SchoolYearService } from '../../../core/services/school-year.service';
import { Enrollment, SchoolYear } from '../../../core/models';
import { ENROLLMENT_STATUS_NAMES } from '../../../core/constants';

@Component({
  selector: 'app-enrollment-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold text-gray-900">Enrollments</h2>
        <a routerLink="/enrollments/new" class="btn btn-primary">
          + New Enrollment
        </a>
      </div>

      <div class="bg-white rounded-xl border border-[#E2D9C2]">
        <div class="p-4 border-b border-gray-200 flex flex-wrap gap-3">
          <input type="text" [(ngModel)]="search" (ngModelChange)="onFilterChange()" placeholder="Search student..."
                 class="form-input max-w-sm" />
          <select [(ngModel)]="schoolYearFilter" (ngModelChange)="onFilterChange()" class="form-input w-auto">
            <option value="">All School Years</option>
            @for (sy of schoolYears(); track sy.id) {
              <option [value]="sy.name">{{ sy.name }}</option>
            }
          </select>
          <select [(ngModel)]="statusFilter" (ngModelChange)="onFilterChange()" class="form-input w-auto">
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="Submitted">Submitted</option>
            <option value="Assessed">Assessed</option>
            <option value="Approved">Approved</option>
            <option value="Paid">Paid</option>
            <option value="Enrolled">Enrolled</option>
            <option value="Cancelled">Cancelled</option>
          </select>
          <label class="inline-flex items-center gap-2 self-center text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="pendingOnly" (ngModelChange)="onFilterChange()" />
            Pending payments only
          </label>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Student</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">School Year</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade Level</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Section</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              @for (e of enrollments(); track e.id) {
                <tr class="hover:bg-gray-50 transition-colors">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ e.studentName }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ e.schoolYear }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ e.gradeLevel }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ e.sectionName || '-' }}</td>
                  <td class="px-6 py-4">
                    <div class="flex flex-wrap items-center gap-1.5">
                      <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                            [ngClass]="getStatusClass(getStatusName(e.status))">{{ getStatusName(e.status) }}</span>
                      @if ((e.pendingPaymentsCount ?? 0) > 0) {
                        <span class="inline-flex items-center rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-700">
                          {{ e.pendingPaymentsCount }} payment{{ e.pendingPaymentsCount === 1 ? '' : 's' }} pending
                        </span>
                      }
                    </div>
                  </td>
                  <td class="px-6 py-4 text-sm text-right">
                    <a [routerLink]="['/enrollments', e.id]" class="text-[#0038A8] hover:text-[#002B85] font-medium">View</a>
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="6" class="px-6 py-8 text-center text-gray-400">No enrollments found</td></tr>
              }
            </tbody>
          </table>
        </div>

        @if (totalPages() > 1) {
          <div class="flex items-center justify-between p-4 border-t border-gray-200">
            <p class="text-sm text-gray-500">Showing page {{ page() }} of {{ totalPages() }} ({{ totalCount() }} total)</p>
            <div class="flex gap-2">
              <button (click)="loadPage(page() - 1)" [disabled]="page() <= 1"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Previous</button>
              <button (click)="loadPage(page() + 1)" [disabled]="page() >= totalPages()"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Next</button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class EnrollmentListComponent implements OnInit {
  enrollments = signal<Enrollment[]>([]);
  schoolYears = signal<SchoolYear[]>([]);
  search = '';
  statusFilter = '';
  schoolYearFilter = '';
  pendingOnly = false;
  page = signal(1);
  totalCount = signal(0);
  totalPages = signal(0);

  constructor(private api: ApiService, private syService: SchoolYearService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    // Deep link from the dashboard tile: /enrollments?pendingPaymentsOnly=true
    this.pendingOnly = this.route.snapshot.queryParamMap.get('pendingPaymentsOnly') === 'true';
    this.syService.ensureLoaded().subscribe(list => {
      this.schoolYears.set(list);
      this.schoolYearFilter = this.syService.activeName() || '';
      this.load();
    });
  }

  load(): void {
    this.api.getEnrollments({
      search: this.search,
      status: this.statusFilter || undefined,
      schoolYear: this.schoolYearFilter || undefined,
      pendingPaymentsOnly: this.pendingOnly || undefined,
      page: this.page()
    }).subscribe(r => {
      this.enrollments.set(r.items);
      this.totalCount.set(r.totalCount);
      this.totalPages.set(r.totalPages);
    });
  }

  onFilterChange(): void {
    this.page.set(1);
    this.load();
  }

  loadPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  getStatusName(status: string | number): string {
    if (typeof status === 'number') return ENROLLMENT_STATUS_NAMES[status] ?? 'Draft';
    return status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-700',
      Submitted: 'bg-blue-100 text-blue-700',
      Assessed: 'bg-yellow-100 text-yellow-700',
      Approved: 'bg-purple-100 text-purple-700',
      Paid: 'bg-green-100 text-green-700',
      Enrolled: 'bg-emerald-100 text-emerald-800',
      Cancelled: 'bg-red-100 text-red-700'
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }
}
