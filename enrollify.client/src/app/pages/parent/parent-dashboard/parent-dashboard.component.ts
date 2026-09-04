import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { ParentChild } from '../../../core/models';
import { enrollmentStatusName } from '../../../core/constants';

@Component({
  selector: 'app-parent-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div>
      <div class="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Welcome, {{ auth.user()?.fullName }}</h1>
          <p class="mt-1 text-sm text-gray-500">Manage your children's enrollments, requirements, and payments from one place.</p>
        </div>
        <a routerLink="/apply" class="inline-flex items-center gap-2 rounded-xl bg-[#0038A8] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#002B85] transition-colors">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          Enroll Another Child
        </a>
      </div>

      @if (loading()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-12 text-center text-gray-400">
          Loading children...
        </div>
      } @else if (children().length === 0) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-12 text-center">
          <div class="mx-auto w-14 h-14 bg-blue-50 rounded-full flex items-center justify-center mb-3">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-[#0038A8]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
            </svg>
          </div>
          <h2 class="text-lg font-semibold text-gray-900">No children yet</h2>
          <p class="mt-1 text-sm text-gray-500">Submit an application to enroll your first child.</p>
          <a routerLink="/apply" class="inline-block mt-4 rounded-xl bg-[#0038A8] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#002B85]">Start Application</a>
        </div>
      } @else {
        <div class="mt-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <!-- track must not use the ?? operator: Angular compiles track expressions into a
               separate function and the nullish-coalescing temp var isn't emitted there
               (ReferenceError: tmp_x_y is not defined at runtime). Use a method instead. -->
          @for (c of children(); track childKey(c)) {
            <div class="bg-white rounded-xl border border-[#E2D9C2] p-5 flex flex-col">
              <div class="flex items-start justify-between">
                <div class="flex items-center gap-3">
                  <div class="flex h-11 w-11 items-center justify-center rounded-full bg-[#0038A8]/10 text-[#0038A8] text-sm font-semibold">
                    {{ initials(c) }}
                  </div>
                  <div>
                    <p class="font-semibold text-gray-900">{{ c.firstName }} {{ c.lastName }}</p>
                    <p class="text-xs text-gray-500">{{ c.gradeLevel || 'Grade not set' }} &bull; {{ c.schoolYear || '—' }}</p>
                  </div>
                </div>
                <span [class]="badgeClass(c)" class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium">{{ c.status }}</span>
              </div>

              @if (c.source === 'Student' && c.studentId) {
                <div class="mt-5 grid grid-cols-3 gap-2">
                  <a [routerLink]="['/parent/children', c.studentId, 'enrollment']" class="text-center rounded-lg border border-gray-200 px-2 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">Enrollment</a>
                  @if (paymentsReady(c)) {
                    <a [routerLink]="['/parent/children', c.studentId, 'payments']" class="text-center rounded-lg border border-gray-200 px-2 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">Payments</a>
                  } @else {
                    <span class="text-center rounded-lg border border-gray-100 bg-gray-50 px-2 py-2 text-xs font-medium text-gray-300 cursor-not-allowed select-none"
                          title="Payments open once the enrollment is approved by the registrar.">Payments</span>
                  }
                  <a [routerLink]="['/parent/children', c.studentId, 'profile']" class="text-center rounded-lg border border-gray-200 px-2 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50">Profile</a>
                </div>
              } @else {
                <p class="mt-5 text-xs text-gray-500 italic">Application is being reviewed. You'll be able to manage enrollment once approved.</p>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class ParentDashboardComponent implements OnInit {
  children = signal<ParentChild[]>([]);
  loading = signal(true);

  constructor(private api: ApiService, public auth: AuthService) {}

  ngOnInit() {
    this.api.getMyChildren().subscribe({
      next: (list) => { this.children.set(list); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  childKey(c: ParentChild): string {
    return c.studentId ?? c.applicationId ?? c.fullName;
  }

  /** Payment happens AT the Approved stage (plan selection + down payment take the
   *  enrollment to Paid, then Enrolled), so the Payments page opens from Approved on. */
  paymentsReady(c: ParentChild): boolean {
    const s = enrollmentStatusName(c.status ?? '');
    return s === 'Approved' || s === 'Paid' || s === 'Enrolled';
  }

  initials(c: ParentChild): string {
    const f = c.firstName?.[0] ?? '';
    const l = c.lastName?.[0] ?? '';
    return (f + l).toUpperCase();
  }

  badgeClass(c: ParentChild): string {
    const s = (c.status ?? '').toString().toLowerCase();
    if (s === 'enrolled') return 'bg-emerald-100 text-emerald-700';
    if (s === 'approved' || s === 'paid') return 'bg-blue-100 text-blue-700';
    if (s === 'submitted' || s === 'assessed') return 'bg-yellow-100 text-yellow-700';
    if (s === 'rejected') return 'bg-red-100 text-red-700';
    if (s === 'draft') return 'bg-gray-100 text-gray-700';
    return 'bg-gray-100 text-gray-600';
  }
}
