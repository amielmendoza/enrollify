import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Enrollment } from '../../../core/models';

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

      <div class="bg-white rounded-xl border border-gray-200">
        <div class="p-4 border-b border-gray-200 flex flex-wrap gap-3">
          <input type="text" [(ngModel)]="search" (ngModelChange)="load()" placeholder="Search student..."
                 class="form-input max-w-sm" />
          <select [(ngModel)]="statusFilter" (ngModelChange)="load()" class="form-input w-auto">
            <option value="">All Statuses</option>
            <option value="Draft">Draft</option>
            <option value="Submitted">Submitted</option>
            <option value="Assessed">Assessed</option>
            <option value="Approved">Approved</option>
            <option value="Paid">Paid</option>
            <option value="Enrolled">Enrolled</option>
          </select>
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
                    <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                          [ngClass]="getStatusClass(getStatusName(e.status))">{{ getStatusName(e.status) }}</span>
                  </td>
                  <td class="px-6 py-4 text-sm text-right">
                    <a [routerLink]="['/enrollments', e.id]" class="text-[#4361ee] hover:text-[#3a56d4] font-medium">View</a>
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="6" class="px-6 py-8 text-center text-gray-400">No enrollments found</td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class EnrollmentListComponent implements OnInit {
  enrollments = signal<Enrollment[]>([]);
  search = '';
  statusFilter = '';

  constructor(private api: ApiService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getEnrollments({ search: this.search, status: this.statusFilter || undefined }).subscribe(r => {
      this.enrollments.set(r.items);
    });
  }

  private statusNames = ['Draft', 'Submitted', 'Assessed', 'Approved', 'Paid', 'Enrolled'];

  getStatusName(status: string | number): string {
    if (typeof status === 'number') return this.statusNames[status] ?? 'Draft';
    return status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'bg-gray-100 text-gray-700',
      Submitted: 'bg-blue-100 text-blue-700',
      Assessed: 'bg-yellow-100 text-yellow-700',
      Approved: 'bg-purple-100 text-purple-700',
      Paid: 'bg-green-100 text-green-700',
      Enrolled: 'bg-emerald-100 text-emerald-800'
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }
}
