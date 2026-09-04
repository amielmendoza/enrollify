import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="folio min-h-screen px-6 py-8 sm:py-12">
      <div class="mx-auto flex min-h-[80vh] w-full max-w-md flex-col">
        <div class="mb-10 flex items-center justify-between gap-4">
          <div class="flex items-center gap-2.5">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-[#0038A8]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.7">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814" />
            </svg>
            <span class="folio-display text-lg font-extrabold tracking-tight">Enrollify</span>
          </div>
          <a routerLink="/tenants" class="text-sm font-semibold text-[#0038A8] hover:underline">Find your school</a>
        </div>

        <div class="my-auto">
          <span class="folder-tab">Sign in</span>
          <div class="folio-card rounded-tl-none p-6 sm:p-8">
          <h2 class="folio-display text-2xl font-black tracking-tight sm:text-3xl">Welcome back.</h2>
          <p class="mt-1.5 text-sm text-gray-500">Sign in to your portal.</p>

          @if (error()) {
            <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700 flex items-start gap-2">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
              </svg>
              <span>{{ error() }}</span>
            </div>
          }

          <form (ngSubmit)="onLogin()" class="mt-8 space-y-5">
            <div>
              <label class="form-label">Email</label>
              <input type="email" [(ngModel)]="email" name="email" required [disabled]="loading()" autocomplete="email"
                     class="form-input" placeholder="admin@mshs.edu.ph" />
            </div>
            <div>
              <label class="form-label">Password</label>
              <input type="password" [(ngModel)]="password" name="password" required [disabled]="loading()" autocomplete="current-password"
                     class="form-input" placeholder="Enter your password" />
            </div>
            <button type="submit" [disabled]="loading()"
                    class="flex w-full items-center justify-center gap-2 rounded-md bg-[#0038A8] py-3 text-sm font-semibold text-white shadow-sm hover:bg-[#002B85] transition-colors disabled:opacity-60 disabled:cursor-not-allowed">
              @if (loading()) {
                <svg class="h-5 w-5 animate-spin text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                </svg>
                <span>Signing in...</span>
              } @else {
                <span>Sign in</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3" />
                </svg>
              }
            </button>
          </form>

          </div>

          <p class="mt-6 text-center text-sm text-gray-500">New parent? <a routerLink="/tenants" class="font-semibold text-[#0038A8] hover:underline">Enroll your child &rarr;</a></p>

          <div class="folio-mono mt-8 space-y-1 text-center text-[11px] tracking-wide text-gray-400">
            <p>super&#64;enrollify.app / SuperAdmin123!</p>
            <p>admin&#64;mshs.edu.ph / Admin123!</p>
            <p>pedro.delacruz&#64;example.com / Parent123!</p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  email = '';
  password = '';
  loading = signal(false);
  error = signal('');

  constructor(private authService: AuthService, private router: Router) {
    if (this.authService.isLoggedIn()) {
      this.router.navigate([this.landingRouteForRole(this.authService.userRole())]);
    }
  }

  onLogin() {
    this.loading.set(true);
    this.error.set('');
    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: (res) => this.router.navigate([this.landingRouteForRole(res.role)]),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || 'Login failed. Please check your credentials.');
      }
    });
  }

  private landingRouteForRole(role: string): string {
    if (role === 'Parent') return '/parent/dashboard';
    if (role === 'SuperAdmin') return '/super/tenants';
    return '/dashboard';
  }
}
