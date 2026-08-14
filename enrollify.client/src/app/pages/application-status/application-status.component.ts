import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ApplicationStatusDto, PublicTenant } from '../../core/models';

/// Anonymous application-status lookup. Applicants land here from the apply success screen
/// (or a bookmark) and check progress using the application number they were given.
@Component({
  selector: 'app-application-status',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  template: `
    <div class="min-h-screen bg-[#f8f9fc] px-6 py-12">
      <div class="mx-auto max-w-xl">
        <div class="flex items-center gap-3 mb-8">
          <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-[#4361ee]">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814" />
            </svg>
          </div>
          <span class="text-lg font-bold text-gray-900">Enrollify</span>
        </div>

        <h1 class="text-3xl font-bold text-gray-900">Application status</h1>
        <p class="mt-1 text-sm text-gray-500">
          @if (school()) { {{ school()!.name }} — }
          Enter the application number from your confirmation screen (e.g. APP-20260731-A1B2C3).
        </p>

        <form class="mt-6 flex gap-2" (ngSubmit)="lookup()">
          <input type="text" [(ngModel)]="applicationNumber" name="applicationNumber" required
                 placeholder="APP-YYYYMMDD-XXXXXX"
                 class="flex-1 rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-[#4361ee] focus:outline-none font-mono" />
          <button type="submit" [disabled]="loading() || !applicationNumber.trim()"
                  class="rounded-lg bg-[#4361ee] px-5 py-2.5 text-sm font-medium text-white hover:bg-[#3a53d4] disabled:opacity-50">
            {{ loading() ? 'Checking…' : 'Check status' }}
          </button>
        </form>

        @if (error()) {
          <div class="mt-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</div>
        }

        @if (result(); as r) {
          <div class="mt-6 bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-center justify-between">
              <p class="font-mono text-xs text-gray-400">{{ r.applicationNumber }}</p>
              <span class="rounded-full px-3 py-1 text-xs font-semibold"
                    [class]="statusBadgeClass(r.status)">{{ r.status }}</span>
            </div>
            <p class="mt-3 text-lg font-semibold text-gray-900">{{ r.applicantName }}</p>
            <p class="text-sm text-gray-500">{{ r.gradeLevel }} · {{ r.schoolYear }}</p>
            <div class="mt-4 border-t border-gray-100 pt-4 text-sm text-gray-600 space-y-1">
              <p>Submitted: {{ r.submittedAt | date:'mediumDate' }}</p>
              @if (r.reviewedAt) { <p>Reviewed: {{ r.reviewedAt | date:'mediumDate' }}</p> }
              @if (r.reviewNotes) { <p class="text-gray-500">Notes: {{ r.reviewNotes }}</p> }
            </div>
            @if (r.status === 'Approved') {
              <div class="mt-4 rounded-lg bg-green-50 border border-green-200 px-4 py-3 text-sm text-green-800">
                Your application was approved! A parent/student account was created with the email you
                provided and the temporary password shown on your application confirmation screen.
                <a routerLink="/login" class="font-medium underline">Sign in</a> and change your password,
                then upload the enrollment requirements.
              </div>
            } @else if (r.status === 'Rejected') {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
                Unfortunately this application was not approved. Please contact the school for details.
              </div>
            } @else {
              <div class="mt-4 rounded-lg bg-blue-50 border border-blue-200 px-4 py-3 text-sm text-blue-800">
                Your application is being reviewed by the registrar. Check back here for updates.
              </div>
            }
          </div>
        }

        <p class="mt-8 text-sm text-gray-500">
          <a [routerLink]="['/', slug, 'apply']" class="text-[#4361ee] font-medium hover:underline">Submit another application</a>
          · <a routerLink="/login" class="text-[#4361ee] font-medium hover:underline">Sign in</a>
        </p>
      </div>
    </div>
  `
})
export class ApplicationStatusComponent implements OnInit {
  slug = '';
  applicationNumber = '';
  loading = signal(false);
  error = signal('');
  result = signal<ApplicationStatusDto | null>(null);
  school = signal<PublicTenant | null>(null);

  constructor(private route: ActivatedRoute, private api: ApiService) {}

  ngOnInit() {
    this.slug = this.route.snapshot.paramMap.get('slug') ?? '';
    if (this.slug) {
      this.api.getSchoolBySlug(this.slug).subscribe({ next: s => this.school.set(s), error: () => {} });
    }
    const fromQuery = this.route.snapshot.queryParamMap.get('ref');
    if (fromQuery) {
      this.applicationNumber = fromQuery;
      this.lookup();
    }
  }

  lookup() {
    const number = this.applicationNumber.trim();
    if (!number) return;
    this.loading.set(true);
    this.error.set('');
    this.result.set(null);
    this.api.getApplicationStatus(this.slug, number).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: err => {
        this.error.set(err.error?.error || 'No application found with that number. Double-check and try again.');
        this.loading.set(false);
      }
    });
  }

  statusBadgeClass(status: string): string {
    switch (status) {
      case 'Approved': return 'bg-green-100 text-green-700';
      case 'Rejected': return 'bg-red-100 text-red-700';
      default: return 'bg-blue-100 text-blue-700';
    }
  }
}
