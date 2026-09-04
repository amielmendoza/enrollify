import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { PublicTenant } from '../../core/models';

/// Public directory of active schools. Anonymous applicants land here from the bare /apply route
/// and pick a school to start their application. Wears the "folio" document language: each
/// school is a manila-tabbed folder in the drawer.
@Component({
  selector: 'app-tenants-directory',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="folio min-h-screen px-6 py-8 sm:py-12">
      <div class="mx-auto max-w-3xl">
        <div class="mb-12 flex items-center justify-between gap-4">
          <div class="flex items-center gap-2.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-[#0038A8]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.7">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814" />
            </svg>
            <span class="folio-display text-lg font-extrabold tracking-tight">Enrollify</span>
          </div>
          <a routerLink="/login" class="text-sm font-semibold text-[#0038A8] hover:underline">Sign in</a>
        </div>

        <p class="folio-eyebrow mb-3">Online enrollment</p>
        <h1 class="folio-display text-4xl sm:text-5xl font-black tracking-tight leading-none">Find your school.</h1>
        <p class="mt-4 max-w-md text-[15px] leading-relaxed text-gray-500">Pick your school to start an application. No account needed — the school creates one for you when your application is approved.</p>

        @if (loading()) {
          <div class="folio-card mt-10 p-12 text-center text-gray-400">Loading schools…</div>
        } @else if (tenants().length === 0) {
          <div class="folio-card mt-10 p-12 text-center text-gray-400">No schools are accepting online applications right now.</div>
        } @else {
          <div class="mt-10 grid grid-cols-1 gap-x-5 gap-y-8 sm:grid-cols-2">
            @for (t of tenants(); track t.id) {
              <a [routerLink]="['/', t.subdomain, 'apply']" class="group block">
                <span class="folder-tab">{{ t.subdomain }}</span>
                <div class="folio-card rounded-tl-none p-5 transition-colors group-hover:border-[#0038A8]">
                  <p class="folio-display text-[17px] font-bold leading-snug">{{ t.name }}</p>
                  <p class="mt-3 text-sm font-semibold text-[#0038A8]">Open application <span aria-hidden="true">&rarr;</span></p>
                </div>
              </a>
            }
          </div>
        }

        <p class="mt-12 text-sm text-gray-500">Already applied? Open your school above and use its status page with your application number. Have an account? <a routerLink="/login" class="font-semibold text-[#0038A8] hover:underline">Sign in</a></p>
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
}
