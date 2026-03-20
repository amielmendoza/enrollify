import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
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
          <input type="text" [(ngModel)]="search" (ngModelChange)="load()" placeholder="Search by name or application #..."
                 class="form-input max-w-sm" />
          <select [(ngModel)]="statusFilter" (ngModelChange)="load()" class="form-input w-auto">
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
              @if (selectedApp()!.reviewNotes) {
                <div class="bg-gray-50 rounded-lg p-4 mt-4">
                  <p class="text-xs text-gray-500">Review Notes</p>
                  <p class="text-sm">{{ selectedApp()!.reviewNotes }}</p>
                </div>
              }
            </div>
            @if (selectedApp()!.status === 'Submitted') {
              <div class="flex justify-end gap-3 p-6 border-t border-gray-200">
                <button (click)="reject(selectedApp()!.id); selectedApp.set(null)" class="btn btn-danger">Reject</button>
                <button (click)="approve(selectedApp()!.id); selectedApp.set(null)" class="btn btn-success">Approve</button>
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

  constructor(private api: ApiService) {}

  ngOnInit() { this.load(); }

  load() {
    this.api.getApplications({ status: this.statusFilter || undefined, search: this.search || undefined }).subscribe(r => {
      this.applications.set(r.items);
    });
  }

  viewDetail(id: string) {
    this.api.getApplication(id).subscribe(a => this.selectedApp.set(a));
  }

  approve(id: string) {
    this.api.reviewApplication(id, true, 'Application approved').subscribe(() => this.load());
  }

  reject(id: string) {
    const notes = prompt('Rejection reason (optional):');
    this.api.reviewApplication(id, false, notes || 'Application rejected').subscribe(() => this.load());
  }

  getStatusBadge(status: string): string {
    const map: Record<string, string> = {
      Submitted: 'badge-info', UnderReview: 'badge-warning', Approved: 'badge-success', Rejected: 'badge-danger', Enrolled: 'badge-success'
    };
    return map[status] || 'badge-gray';
  }
}
