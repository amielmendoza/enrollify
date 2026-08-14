import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { NotificationService } from '../../core/services/notification.service';
import { ApplicationListDto, ApplicationDetailDto } from '../../core/models';

@Component({
  selector: 'app-admissions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <div class="flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Admissions</h1>
          <p class="mt-1 text-sm text-gray-500">Review and manage student applications</p>
        </div>
      </div>

      <!-- Filters -->
      <div class="bg-white rounded-xl border border-gray-200 mt-6">
        <div class="p-4 border-b border-gray-100 flex flex-wrap gap-3">
          <input type="text" [(ngModel)]="search" (ngModelChange)="onFilterChange()" placeholder="Search by name or application #..."
                 class="form-input max-w-sm" />
          <select [(ngModel)]="statusFilter" (ngModelChange)="onFilterChange()" class="form-input w-auto">
            <option value="">All Statuses</option>
            <option value="Submitted">Submitted</option>
            <option value="UnderReview">Under Review</option>
            <option value="Approved">Approved</option>
            <option value="Rejected">Rejected</option>
          </select>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Application #</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Applicant</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade Level</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date Applied</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (app of applications(); track app.id) {
                <tr class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-mono">{{ app.applicationNumber }}</td>
                  <td class="px-6 py-4">
                    <p class="text-sm font-medium text-gray-900">{{ app.fullName }}</p>
                    <p class="text-xs text-gray-500">{{ app.email }}</p>
                  </td>
                  <td class="px-6 py-4 text-sm">{{ app.gradeLevel }}</td>
                  <td class="px-6 py-4">
                    <span class="badge" [class]="getStatusBadge(app.status)">{{ app.status }}</span>
                  </td>
                  <td class="px-6 py-4 text-sm text-gray-500">{{ app.createdAt | date:'mediumDate' }}</td>
                  <td class="px-6 py-4 text-sm text-right space-x-2">
                    <button (click)="viewDetail(app.id)" class="text-[#4361ee] hover:text-[#3a56d4] font-medium">View</button>
                    @if (app.status === 'Submitted') {
                      <button (click)="approve(app.id)" class="text-emerald-600 hover:text-emerald-700 font-medium">Approve</button>
                      <button (click)="reject(app.id)" class="text-red-600 hover:text-red-700 font-medium">Reject</button>
                    }
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="6" class="px-6 py-8 text-center text-gray-400">No applications found</td></tr>
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

      <!-- Detail Modal -->
      @if (selectedApp()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-gray-900/50" (click)="selectedApp.set(null)">
          <div class="bg-white rounded-xl shadow-xl max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto" (click)="$event.stopPropagation()">
            <div class="flex items-center justify-between p-6 border-b border-gray-200">
              <div>
                <h2 class="text-lg font-semibold text-gray-900">{{ selectedApp()!.applicationNumber }}</h2>
                <span class="badge mt-1" [class]="getStatusBadge(selectedApp()!.status)">{{ selectedApp()!.status }}</span>
              </div>
              <button (click)="selectedApp.set(null)" class="p-2 hover:bg-gray-100 rounded-lg">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" /></svg>
              </button>
            </div>
            <div class="p-6 space-y-4">
              <div class="grid grid-cols-2 gap-4">
                <div><p class="text-xs text-gray-500">Full Name</p><p class="text-sm font-medium">{{ selectedApp()!.firstName }} {{ selectedApp()!.middleName || '' }} {{ selectedApp()!.lastName }}</p></div>
                <div><p class="text-xs text-gray-500">Email</p><p class="text-sm font-medium">{{ selectedApp()!.email }}</p></div>
                <div><p class="text-xs text-gray-500">Gender</p><p class="text-sm font-medium">{{ selectedApp()!.gender }}</p></div>
                <div><p class="text-xs text-gray-500">Date of Birth</p><p class="text-sm font-medium">{{ selectedApp()!.dateOfBirth | date:'mediumDate' }}</p></div>
                <div><p class="text-xs text-gray-500">Contact</p><p class="text-sm font-medium">{{ selectedApp()!.contactNumber || '-' }}</p></div>
                <div><p class="text-xs text-gray-500">Address</p><p class="text-sm font-medium">{{ selectedApp()!.address || '-' }}</p></div>
                <div><p class="text-xs text-gray-500">Grade Level</p><p class="text-sm font-medium">{{ selectedApp()!.gradeLevel }}</p></div>
                <div><p class="text-xs text-gray-500">School Year</p><p class="text-sm font-medium">{{ selectedApp()!.schoolYear }}</p></div>
                <div><p class="text-xs text-gray-500">Previous School</p><p class="text-sm font-medium">{{ selectedApp()!.previousSchool || '-' }}</p></div>
                <div><p class="text-xs text-gray-500">Guardian</p><p class="text-sm font-medium">{{ selectedApp()!.guardianName || '-' }} {{ selectedApp()!.guardianRelationship ? '(' + selectedApp()!.guardianRelationship + ')' : '' }}</p></div>
              </div>
              @if (selectedApp()!.parentEmail || selectedApp()!.parentContactNumber) {
                <div class="border-t border-gray-100 pt-4">
                  <p class="text-xs font-medium text-gray-400 uppercase tracking-wider mb-3">Parent Contact</p>
                  <div class="grid grid-cols-2 gap-4">
                    @if (selectedApp()!.parentEmail) {
                      <div><p class="text-xs text-gray-500">Parent Email</p><p class="text-sm font-medium">{{ selectedApp()!.parentEmail }}</p></div>
                    }
                    @if (selectedApp()!.parentContactNumber) {
                      <div><p class="text-xs text-gray-500">Parent Contact Number</p><p class="text-sm font-medium">{{ selectedApp()!.parentContactNumber }}</p></div>
                    }
                  </div>
                </div>
              }
              @if (customFieldEntries().length > 0) {
                <div class="border-t border-gray-100 pt-4">
                  <p class="text-xs font-medium text-gray-400 uppercase tracking-wider mb-3">Additional Information</p>
                  <div class="grid grid-cols-2 gap-4">
                    @for (entry of customFieldEntries(); track entry.key) {
                      <div><p class="text-xs text-gray-500">{{ formatFieldKey(entry.key) }}</p><p class="text-sm font-medium">{{ entry.value || '-' }}</p></div>
                    }
                  </div>
                </div>
              }
              @if (selectedApp()!.reviewNotes) {
                <div class="bg-gray-50 rounded-lg p-4 mt-4">
                  <p class="text-xs text-gray-500">Review Notes</p>
                  <p class="text-sm">{{ selectedApp()!.reviewNotes }}</p>
                </div>
              }
            </div>
            @if (selectedApp()!.status === 'Submitted') {
              <div class="flex justify-end gap-3 p-6 border-t border-gray-200">
                <button (click)="reject(selectedApp()!.id)" class="btn btn-danger">Reject</button>
                <button (click)="approve(selectedApp()!.id)" class="btn btn-success">Approve</button>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class AdmissionsComponent implements OnInit {
  applications = signal<ApplicationListDto[]>([]);
  selectedApp = signal<ApplicationDetailDto | null>(null);
  search = '';
  statusFilter = '';
  page = signal(1);
  totalCount = signal(0);
  totalPages = signal(0);

  constructor(private api: ApiService, private notify: NotificationService) {}

  ngOnInit() { this.load(); }

  load() {
    this.api.getApplications({ status: this.statusFilter || undefined, search: this.search || undefined, page: this.page() }).subscribe(r => {
      this.applications.set(r.items);
      this.totalCount.set(r.totalCount);
      this.totalPages.set(r.totalPages);
    });
  }

  onFilterChange() {
    this.page.set(1);
    this.load();
  }

  loadPage(p: number) {
    this.page.set(p);
    this.load();
  }

  viewDetail(id: string) {
    this.api.getApplication(id).subscribe(a => this.selectedApp.set(a));
  }

  async approve(id: string) {
    const ok = await this.notify.confirm(
      'Approve this application? This creates the student record, login account, and a draft enrollment.',
      { title: 'Approve Application', confirmLabel: 'Approve' });
    if (!ok) return;
    this.api.reviewApplication(id, true, 'Application approved').subscribe({
      next: () => {
        this.selectedApp.set(null);
        this.load();
      },
      error: (err) => this.notify.error(err.error?.error || 'Failed to review application.')
    });
  }

  async reject(id: string) {
    const notes = await this.notify.prompt('Rejection reason (optional):',
      { title: 'Reject Application', confirmLabel: 'Reject', danger: true });
    if (notes === null) return;
    this.api.reviewApplication(id, false, notes || 'Application rejected').subscribe({
      next: () => {
        this.selectedApp.set(null);
        this.load();
      },
      error: (err) => this.notify.error(err.error?.error || 'Failed to review application.')
    });
  }

  customFieldEntries(): { key: string; value: string | null }[] {
    const values = this.selectedApp()?.customFieldValues;
    if (!values) return [];
    return Object.entries(values).map(([key, value]) => ({ key, value }));
  }

  // "middle_name" / "middleName" -> "Middle Name" for display in the detail modal.
  formatFieldKey(key: string): string {
    return key
      .replace(/_/g, ' ')
      .replace(/([a-z\d])([A-Z])/g, '$1 $2')
      .replace(/\b\w/g, c => c.toUpperCase());
  }

  getStatusBadge(status: string): string {
    const map: Record<string, string> = {
      Submitted: 'badge-info', UnderReview: 'badge-warning', Approved: 'badge-success', Rejected: 'badge-danger', Enrolled: 'badge-success'
    };
    return map[status] || 'badge-gray';
  }
}
