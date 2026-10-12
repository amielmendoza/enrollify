import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { LoadingComponent } from '../../../core/components/loading.component';
import { formatApiError } from '../../../core/utils/format-api-error';
import { ApplicationListDto, Enrollment, Student } from '../../../core/models';
import { enrollmentStatusBadgeClass, enrollmentStatusName } from '../../../core/constants';

// The student hub: who they are, every enrollment across years, and the application(s)
// that created or linked to the record. Each row links onward, so student, enrollment and
// application are never more than one click apart.
@Component({
  selector: 'app-student-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LoadingComponent],
  template: `
    <div class="max-w-4xl">
      <a routerLink="/students" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" /></svg>
        Students
      </a>

      @if (loading()) {
        <app-loading label="Loading student..." />
      } @else if (error()) {
        <div class="rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">{{ error() }}</div>
      }
      @if (!loading() && student(); as s) {
        <div class="flex flex-wrap items-start justify-between gap-4 mb-6">
          <div>
            <div class="flex flex-wrap items-center gap-2">
              <h2 class="text-2xl font-bold text-gray-900">{{ s.fullName }}</h2>
              @if (!s.isActive) {
                <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Inactive</span>
              }
            </div>
            <p class="mt-1 text-sm text-gray-500">LRN <span class="folio-mono text-gray-700">{{ s.lrn || '—' }}</span></p>
          </div>
          <div class="flex flex-wrap gap-2">
            <a [routerLink]="['/students', s.id, 'edit']" class="btn btn-secondary">Edit</a>
            <a routerLink="/enrollments/new" class="btn btn-primary">New Enrollment</a>
          </div>
        </div>

        <div class="bg-white rounded-xl border border-[#E2D9C2] p-6 mb-6">
          <h3 class="font-semibold text-gray-900 mb-4">Details</h3>
          <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 text-sm">
            <div><dt class="text-xs text-gray-500">Birth Date</dt><dd class="text-gray-900">{{ s.birthDate | date:'mediumDate' }}</dd></div>
            <div><dt class="text-xs text-gray-500">Gender</dt><dd class="text-gray-900">{{ s.gender || '—' }}</dd></div>
            <div class="sm:col-span-2"><dt class="text-xs text-gray-500">Address</dt><dd class="text-gray-900">{{ s.address || '—' }}</dd></div>
            <div><dt class="text-xs text-gray-500">Contact Number</dt><dd class="text-gray-900">{{ s.contactNumber || '—' }}</dd></div>
            <div><dt class="text-xs text-gray-500">Email</dt><dd class="text-gray-900 break-all">{{ s.email || '—' }}</dd></div>
            <div><dt class="text-xs text-gray-500">Guardian</dt><dd class="text-gray-900">{{ s.guardianName || '—' }}</dd></div>
            <div><dt class="text-xs text-gray-500">Guardian Contact</dt><dd class="text-gray-900">{{ s.guardianContact || '—' }}</dd></div>
            <div class="sm:col-span-2">
              <dt class="text-xs text-gray-500">Portal access</dt>
              <dd class="text-gray-900">
                @if (s.userId) { Has a student login }
                @else if (s.parentUserId) { Managed through a parent account }
                @else { No login yet }
              </dd>
            </div>
          </dl>
        </div>

        <div class="bg-white rounded-xl border border-[#E2D9C2] mb-6">
          <div class="p-4 border-b border-gray-100">
            <h3 class="font-semibold text-gray-900">Enrollments</h3>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">School Year</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Grade</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Section</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  <th class="px-6 py-3"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                @for (e of enrollments(); track e.id) {
                  <tr class="hover:bg-gray-50">
                    <td class="px-6 py-4 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ e.schoolYear }}</td>
                    <td class="px-6 py-4 text-sm text-gray-900 whitespace-nowrap">{{ e.gradeLevel }}</td>
                    <td class="px-6 py-4 text-sm text-gray-600">{{ e.sectionName || '—' }}</td>
                    <td class="px-6 py-4 text-sm">
                      <span class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium" [ngClass]="statusClass(e.status)">{{ statusName(e.status) }}</span>
                    </td>
                    <td class="px-6 py-4 text-sm text-right">
                      <a [routerLink]="['/enrollments', e.id]" class="text-[#0038A8] hover:text-[#002B85] font-medium">View</a>
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="5" class="px-6 py-8 text-center text-sm text-gray-400">No enrollments yet.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="bg-white rounded-xl border border-[#E2D9C2]">
          <div class="p-4 border-b border-gray-100">
            <h3 class="font-semibold text-gray-900">Applications</h3>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Application No.</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">For</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Submitted</th>
                  <th class="px-6 py-3"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                @for (a of applications(); track a.id) {
                  <tr class="hover:bg-gray-50">
                    <td class="px-6 py-4 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ a.applicationNumber }}</td>
                    <td class="px-6 py-4 text-sm text-gray-900 whitespace-nowrap">{{ a.gradeLevel }} · <span class="folio-mono">{{ a.schoolYear }}</span></td>
                    <td class="px-6 py-4 text-sm"><span class="badge" [ngClass]="applicationBadge(a.status)">{{ a.status }}</span></td>
                    <td class="px-6 py-4 text-sm text-gray-500 whitespace-nowrap">{{ a.createdAt | date:'mediumDate' }}</td>
                    <td class="px-6 py-4 text-sm text-right">
                      <a routerLink="/admissions" [queryParams]="{ view: a.id }" class="text-[#0038A8] hover:text-[#002B85] font-medium">View</a>
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="5" class="px-6 py-8 text-center text-sm text-gray-400">No applications linked to this student (added by staff).</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `
})
export class StudentDetailComponent implements OnInit {
  student = signal<Student | null>(null);
  enrollments = signal<Enrollment[]>([]);
  applications = signal<ApplicationListDto[]>([]);
  loading = signal(true);
  error = signal('');

  constructor(private api: ApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      student: this.api.getStudent(id),
      enrollments: this.api.getEnrollments({ studentId: id, pageSize: 100 }),
      applications: this.api.getApplications({ studentId: id, pageSize: 50 }),
    }).subscribe({
      next: ({ student, enrollments, applications }) => {
        this.student.set(student);
        this.enrollments.set(enrollments.items);
        this.applications.set(applications.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(formatApiError(err, 'Failed to load this student.'));
      }
    });
  }

  statusName(status: string | number): string { return enrollmentStatusName(status); }

  statusClass(status: string | number): string { return enrollmentStatusBadgeClass(enrollmentStatusName(status)); }

  // Same palette as the Admissions page.
  applicationBadge(status: string): string {
    const map: Record<string, string> = {
      Submitted: 'badge-info', UnderReview: 'badge-warning', Approved: 'badge-success', Rejected: 'badge-danger'
    };
    return map[status] || 'badge-gray';
  }
}
