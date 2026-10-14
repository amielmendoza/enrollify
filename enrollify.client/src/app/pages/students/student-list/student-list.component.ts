import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { LoadingComponent } from '../../../core/components/loading.component';
import { formatApiError } from '../../../core/utils/format-api-error';
import { Student } from '../../../core/models';

@Component({
  selector: 'app-student-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, LoadingComponent],
  template: `
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold text-gray-900">Students</h2>
        <a routerLink="/students/new" class="btn btn-primary">
          + Add Student
        </a>
      </div>

      <div class="bg-white rounded-xl border border-[#E2D9C2]">
        <div class="p-4 border-b border-gray-200 flex flex-wrap items-center gap-4">
          <input type="text" [(ngModel)]="search" (ngModelChange)="onSearch()" placeholder="Search by name, student no., or LRN..."
                 class="form-input max-w-sm" />
          <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="onSearch()" />
            Show inactive
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="missingLrn" (ngModelChange)="onSearch()" />
            Missing LRN
          </label>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" [(ngModel)]="lrnNeedsReview" (ngModelChange)="onSearch()" />
            Check LRN
          </label>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Student No.</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">LRN</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Gender</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Contact</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Guardian</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Login</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              @for (student of students(); track student.id) {
                <tr class="hover:bg-gray-50 transition-colors" [class.opacity-60]="student.isActive === false">
                  <td class="px-6 py-4 text-sm folio-mono text-gray-700 whitespace-nowrap">{{ student.studentNumber }}</td>
                  <td class="px-6 py-4 text-sm whitespace-nowrap">
                    @if (!student.lrn) {
                      <span class="text-gray-400">No LRN yet</span>
                    } @else {
                      <span class="folio-mono text-gray-700">{{ student.lrn }}</span>
                      @if (student.lrnNeedsReview) {
                        <span class="ml-2 inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">Check LRN</span>
                      }
                    }
                  </td>
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">
                    <div class="flex flex-wrap items-center gap-2">
                      <a [routerLink]="['/students', student.id]" class="hover:text-[#0038A8] hover:underline">{{ student.fullName }}</a>
                      @if (student.isActive === false) {
                        <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500">Inactive</span>
                      }
                    </div>
                  </td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.gender }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.contactNumber }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.guardianName }}</td>
                  <td class="px-6 py-4 text-sm">
                    @if (student.userId) {
                      <span class="inline-flex items-center gap-1 text-xs font-medium text-emerald-600">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                        Has account
                      </span>
                    }
                  </td>
                  <td class="px-6 py-4 text-sm text-right whitespace-nowrap space-x-3">
                    <a [routerLink]="['/students', student.id, 'edit']" class="text-[#0038A8] hover:text-[#002B85] font-medium">Edit</a>
                    @if (!student.userId && !student.parentUserId) {
                      <button (click)="createAccount(student)" [disabled]="creatingId() === student.id"
                              class="text-[#0038A8] hover:text-[#002B85] font-medium disabled:opacity-50">
                        {{ creatingId() === student.id ? 'Creating...' : 'Create account' }}
                      </button>
                    }
                    @if (student.isActive === false) {
                      <button (click)="setActive(student, true)" class="text-emerald-600 hover:text-emerald-700 font-medium">Reactivate</button>
                    } @else {
                      <button (click)="setActive(student, false)" class="text-red-600 hover:text-red-700 font-medium">Deactivate</button>
                    }
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="8">
                  @if (loading()) { <app-loading label="Loading students..." /> }
                  @else { <p class="px-6 py-8 text-center text-gray-400">No students found</p> }
                </td></tr>
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

      <!-- Account-created modal: the one moment the temporary password is visible -->
      @if (accountResult(); as r) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="accountResult.set(null)">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Account created</h3>
            <p class="mt-1 text-sm text-gray-500">Login credentials for <span class="font-medium text-gray-900">{{ r.studentName }}</span>. Share them securely &mdash; they'll be required to change the password at first login.</p>

            <div class="mt-5 space-y-3 rounded-lg border border-[#E2D9C2] bg-gray-50 p-4">
              <div>
                <p class="text-xs text-gray-500">Email</p>
                <p class="text-sm folio-mono text-gray-900">{{ r.email }}</p>
              </div>
              <div>
                <p class="text-xs text-gray-500">Temporary password</p>
                <p class="text-sm folio-mono text-gray-900">{{ r.temporaryPassword }}</p>
              </div>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="copyCredentials()" class="text-sm px-4 py-2 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Copy credentials</button>
              <button (click)="accountResult.set(null)" class="text-sm px-5 py-2 rounded-lg bg-[#0038A8] text-white font-semibold hover:bg-[#002B85]">Done</button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class StudentListComponent implements OnInit {
  students = signal<Student[]>([]);
  loading = signal(true);
  search = '';
  showInactive = false;
  missingLrn = false;
  lrnNeedsReview = false;
  page = signal(1);
  totalCount = signal(0);
  totalPages = signal(0);
  creatingId = signal<string | null>(null);
  accountResult = signal<{ studentName: string; email: string; temporaryPassword: string } | null>(null);

  constructor(private api: ApiService, private notify: NotificationService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.getStudents(this.search, this.page(), 20, this.showInactive, { missingLrn: this.missingLrn, lrnNeedsReview: this.lrnNeedsReview }).subscribe({
      next: result => {
        this.loading.set(false);
        this.students.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages);
      },
      error: (err) => {
        this.loading.set(false);
        this.notify.error(formatApiError(err, 'Failed to load students.'));
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.load();
  }

  loadPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  async createAccount(student: Student): Promise<void> {
    // The prompt doubles as the confirm step: prefilled with the stored email when
    // there is one (editable, in case it's stale), required before anything is sent.
    const entered = await this.notify.prompt(
      `Enter the email address for ${student.fullName}'s login account. A temporary password will be generated, and they'll be required to change it at first login.`,
      {
        title: 'Create Student Account',
        confirmLabel: 'Create Account',
        required: true,
        placeholder: 'student@example.com',
        initialValue: student.email?.trim() ?? ''
      });
    if (entered === null) return;
    const email = entered.trim();
    if (!email) return;
    this.creatingId.set(student.id);
    this.api.createStudentAccount(student.id, email).subscribe({
      next: (res) => {
        this.creatingId.set(null);
        this.accountResult.set({ studentName: student.fullName, email: res.email, temporaryPassword: res.temporaryPassword });
        this.load();
      },
      error: (err) => {
        this.creatingId.set(null);
        this.notify.error(formatApiError(err, 'Failed to create the account.'));
      }
    });
  }

  copyCredentials(): void {
    const r = this.accountResult();
    if (!r) return;
    navigator.clipboard.writeText(`Email: ${r.email}\nTemporary password: ${r.temporaryPassword}`).then(
      () => this.notify.success('Credentials copied to clipboard.'),
      () => this.notify.error('Could not copy — select and copy the values manually.')
    );
  }

  async setActive(student: Student, isActive: boolean): Promise<void> {
    if (!isActive) {
      const ok = await this.notify.confirm(
        `Deactivate ${student.fullName}? Inactive students are hidden from lists by default and lose portal access.`,
        { title: 'Deactivate Student', confirmLabel: 'Deactivate', danger: true });
      if (!ok) return;
    }
    this.api.setStudentActive(student.id, isActive).subscribe({
      next: () => {
        this.notify.success(isActive ? 'Student reactivated.' : 'Student deactivated.');
        this.load();
      },
      // e.g. the server's clear 400 when an active-year enrollment blocks deactivation
      error: (err) => this.notify.error(formatApiError(err, 'Failed to update the student.'))
    });
  }
}
