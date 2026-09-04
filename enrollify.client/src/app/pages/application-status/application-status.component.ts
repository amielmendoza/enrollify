import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ApplicationStatusDto, PublicTenant } from '../../core/models';

/// Anonymous application-status lookup. Applicants land here from the apply success screen
/// (or a bookmark) and check progress using the application number they were given.
/// Folio document language: the result is the applicant's record card, and the current
/// status lands on it as a rubber stamp.
@Component({
  selector: 'app-application-status',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  template: `
    <div class="folio min-h-screen px-6 py-8 sm:py-12">
      <div class="mx-auto max-w-xl">
        <div class="mb-10 flex items-center justify-between gap-4">
          <div class="flex items-center gap-2.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-[#0038A8]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.7">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814" />
            </svg>
            <span class="folio-display text-lg font-extrabold tracking-tight">Enrollify</span>
          </div>
          <a routerLink="/login" class="text-sm font-semibold text-[#0038A8] hover:underline">Sign in</a>
        </div>

        <p class="folio-eyebrow mb-3">Application status</p>
        <h1 class="folio-display text-3xl sm:text-4xl font-black tracking-tight leading-none">Check your application.</h1>
        <p class="mt-3 text-sm text-gray-500">
          @if (school()) { {{ school()!.name }} — }
          enter the application number from your confirmation screen.
        </p>

        <form class="mt-6 flex gap-2" (ngSubmit)="lookup()">
          <input type="text" [(ngModel)]="applicationNumber" name="applicationNumber" required
                 placeholder="APP-YYYYMMDD-XXXXXX"
                 class="folio-mono flex-1 rounded-md border border-[#D8D0BB] bg-white px-4 py-2.5 text-sm tracking-wide outline-none transition-colors focus:border-[#0038A8] focus:shadow-[0_0_0_3px_rgba(0,56,168,0.14)]" />
          <button type="submit" [disabled]="loading() || !applicationNumber.trim()"
                  class="rounded-md bg-[#0038A8] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#002B85] disabled:opacity-50">
            {{ loading() ? 'Checking…' : 'Check status' }}
          </button>
        </form>

        @if (error()) {
          <div class="mt-6 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</div>
        }

        @if (result(); as r) {
          <div class="mt-8">
            <span class="folder-tab">{{ r.applicationNumber }}</span>
            <div class="folio-card relative rounded-tl-none p-6 pt-8">
              <span class="stamp stamp-animate absolute -top-3 right-4" [class]="stampClass(r.status)">{{ stampLabel(r.status) }}</span>
              <p class="folio-display mt-2 text-xl font-bold">{{ r.applicantName }}</p>
              <p class="mt-0.5 text-sm text-gray-500">{{ r.gradeLevel }} · SY {{ r.schoolYear }}</p>
              <div class="folio-mono mt-4 space-y-1 border-t border-[#EFE9D8] pt-4 text-[13px] text-[#56617C]">
                <p>Submitted&nbsp;&nbsp;{{ r.submittedAt | date:'mediumDate' }}</p>
                @if (r.reviewedAt) { <p>Reviewed&nbsp;&nbsp;&nbsp;{{ r.reviewedAt | date:'mediumDate' }}</p> }
              </div>
              @if (r.reviewNotes) { <p class="mt-3 text-sm text-gray-500">Notes: {{ r.reviewNotes }}</p> }

              @if (r.status === 'Approved') {
                <div class="mt-5 rounded-md border-l-4 border-[#0E7A4E] bg-[#F0F7F3] px-4 py-3 text-sm text-[#155239]">
                  Your application was approved. An account was created with the email you provided and the temporary password from your confirmation screen.
                  <a routerLink="/login" class="font-semibold underline">Sign in</a>, change your password, then upload the enrollment requirements.
                </div>
              } @else if (r.status === 'Rejected') {
                <div class="mt-5 rounded-md border-l-4 border-[#CE1126] bg-red-50 px-4 py-3 text-sm text-red-800">
                  This application was not approved. Please contact the school for details.
                </div>
              } @else {
                <div class="mt-5 rounded-md border-l-4 border-[#0038A8] bg-[#F0F4FF] px-4 py-3 text-sm text-[#1D3B7A]">
                  Your application is with the registrar for review. Check back here for updates.
                </div>
              }
            </div>
          </div>
        }

        <p class="mt-10 text-sm text-gray-500">
          <a [routerLink]="['/', slug, 'apply']" class="font-semibold text-[#0038A8] hover:underline">Submit another application</a>
          · <a routerLink="/login" class="font-semibold text-[#0038A8] hover:underline">Sign in</a>
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

  stampLabel(status: string): string {
    switch (status) {
      case 'Approved': return 'Approved';
      case 'Rejected': return 'Rejected';
      case 'UnderReview': return 'In review';
      default: return 'Received';
    }
  }

  stampClass(status: string): string {
    switch (status) {
      case 'Approved': return 'stamp stamp-green stamp-animate absolute -top-3 right-4';
      case 'Rejected': return 'stamp stamp-red stamp-animate absolute -top-3 right-4';
      default: return 'stamp stamp-blue stamp-animate absolute -top-3 right-4';
    }
  }
}
