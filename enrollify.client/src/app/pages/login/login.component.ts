import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex min-h-screen">
      <!-- LEFT: Blue branding panel -->
      <div class="hidden lg:flex lg:w-1/2 flex-col justify-between bg-[#4361ee] p-10">
        <div class="flex items-center gap-3">
          <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-white/20">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
            </svg>
          </div>
          <span class="text-lg font-bold text-white">Enrollify</span>
        </div>
        <div class="mb-8">
          <h1 class="text-4xl font-bold leading-tight text-white">Enrollment made<br />simple and modern.</h1>
          <p class="mt-4 text-base leading-relaxed text-white/70">A streamlined enrollment experience for students, registrars, and administrators across Philippine K-12 schools.</p>
        </div>
        <p class="text-sm text-white/40">&copy; 2026 Enrollify. All rights reserved.</p>
      </div>

      <!-- RIGHT: Login form -->
      <div class="flex flex-1 items-center justify-center bg-[#f8f9fc] px-6 py-12">
        <div class="w-full max-w-md">
          <!-- Mobile logo -->
          <div class="mb-8 flex items-center gap-3 lg:hidden">
            <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-[#4361ee]">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
              </svg>
            </div>
            <span class="text-lg font-bold text-gray-900">Enrollify</span>
          </div>

          <h2 class="text-2xl font-bold text-gray-900">Welcome back</h2>
          <p class="mt-1 text-sm text-gray-500">Sign in to your portal</p>

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
                    class="flex w-full items-center justify-center gap-2 rounded-xl bg-[#4361ee] py-3 text-sm font-semibold text-white shadow-sm hover:bg-[#3a56d4] focus:outline-none focus:ring-2 focus:ring-[#4361ee]/50 focus:ring-offset-2 transition-colors disabled:opacity-60 disabled:cursor-not-allowed">
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

          <p class="mt-8 text-center text-sm text-gray-400">Demo: admin&#64;mshs.edu.ph / Admin123!</p>
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
      this.router.navigate(['/dashboard']);
    }
  }

  onLogin() {
    this.loading.set(true);
    this.error.set('');
    this.authService.login({ email: this.email, password: this.password }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error || 'Login failed. Please check your credentials.');
      }
    });
  }
}
