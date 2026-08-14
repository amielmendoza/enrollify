import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { PublicTenant } from '../../core/models';

/// Public directory of active schools. Anonymous applicants land here from the bare /apply route
/// and pick a school to start their application.
@Component({
  selector: 'app-tenants-directory',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="min-h-screen bg-[#f8f9fc] px-6 py-12">
      <div class="mx-auto max-w-3xl">
        <div class="flex items-center gap-3 mb-8">
          <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-[#4361ee]">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814" />
            </svg>
          </div>
          <span class="text-lg font-bold text-gray-900">Enrollify</span>
        </div>

        <h1 class="text-3xl font-bold text-gray-900">Find your school</h1>
        <p class="mt-1 text-sm text-gray-500">Pick the school you'd like to apply to. You'll create a parent account or apply as a student on the next step.</p>

        @if (loading()) {
          <div class="mt-8 bg-white rounded-xl border border-gray-200 p-12 text-center text-gray-400">Loading schools...</div>
        } @else if (tenants().length === 0) {
          <div class="mt-8 bg-white rounded-xl border border-gray-200 p-12 text-center text-gray-400">No schools are currently available.</div>
        } @else {
          <div class="mt-8 grid grid-cols-1 sm:grid-cols-2 gap-4">
            @for (t of tenants(); track t.id) {
              <a [routerLink]="['/', t.subdomain, 'apply']" class="bg-white rounded-xl border border-gray-200 p-5 hover:border-[#4361ee] hover:shadow-sm transition-all flex items-start gap-3">
                <div class="flex h-10 w-10 items-center justify-center rounded-full bg-[#4361ee]/10 text-[#4361ee] text-sm font-semibold shrink-0">
                  {{ initials(t.name) }}
                </div>
                <div class="flex-1">
                  <p class="font-semibold text-gray-900">{{ t.name }}</p>
                  <p class="text-xs text-gray-400 font-mono">{{ t.subdomain }}</p>
                </div>
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 text-gray-400 mt-1" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" /></svg>
              </a>
            }
          </div>
        }

        <p class="mt-8 text-sm text-gray-500">Already have an account? <a routerLink="/login" class="text-[#4361ee] font-medium hover:underline">Sign in</a></p>
      </div>
    </div>
  `
})
export class TenantsDirectoryComponent implements OnInit {
  tenants = signal<PublicTenant[]>([]);
  loading = signal(true);

  constructor(private api: ApiService) {}

  ngOnInit() {
    this.api.getActivePublicTenants().subscribe({
      next: list => { this.tenants.set(list); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  initials(name: string): string {
    return name.split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}
