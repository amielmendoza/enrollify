import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';

// Public password-recovery request page. Deliberately never reveals whether the
// email has an account: every submission lands on the same neutral success state.
@Component({
  selector: 'app-forgot-password',
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
          <a routerLink="/login" class="text-sm font-semibold text-[#0038A8] hover:underline">Back to sign in</a>
        </div>

        <div class="my-auto">
          <span class="folder-tab">Password reset</span>
          <div class="folio-card rounded-tl-none p-6 sm:p-8">
            @if (submitted()) {
              <div class="flex items-start gap-3">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 shrink-0 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.7">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                </svg>
                <div>
                  <h2 class="folio-display text-xl font-black tracking-tight">Check your inbox.</h2>
                  <p class="mt-1.5 text-sm text-gray-500">If that email has an account, a reset link is on its way.</p>
                  <a routerLink="/login" class="mt-4 inline-block text-sm font-semibold text-[#0038A8] hover:underline">&larr; Back to sign in</a>
                </div>
              </div>
            } @else {
              <h2 class="folio-display text-2xl font-black tracking-tight sm:text-3xl">Forgot your password?</h2>
              <p class="mt-1.5 text-sm text-gray-500">Enter your account email and we'll send you a reset link.</p>

              @if (error()) {
                <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ error() }}</div>
              }

              <form (ngSubmit)="onSubmit()" class="mt-8 space-y-5">
                <div>
                  <label class="form-label">Email</label>
                  <input type="email" [(ngModel)]="email" name="email" required [disabled]="loading()" autocomplete="email"
                         class="form-input" placeholder="you@example.com" />
                </div>
                <button type="submit" [disabled]="loading() || !email"
                        class="flex w-full items-center justify-center gap-2 rounded-md bg-[#0038A8] py-3 text-sm font-semibold text-white shadow-sm hover:bg-[#002B85] transition-colors disabled:opacity-60 disabled:cursor-not-allowed">
                  {{ loading() ? 'Sending...' : 'Send reset link' }}
                </button>
              </form>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class ForgotPasswordComponent {
  email = '';
  loading = signal(false);
  submitted = signal(false);
  error = signal('');

  constructor(private api: ApiService) {}

  onSubmit(): void {
    if (!this.email) return;
    this.loading.set(true);
    this.error.set('');
    this.api.forgotPassword(this.email).subscribe({
      next: () => {
        this.loading.set(false);
        this.submitted.set(true);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 0) {
          // The request never left the machine — saying "it's on its way" would be a lie.
          this.error.set("Can't reach the server. Please try again in a moment.");
        } else {
          // Anything the server answered gets the same neutral state — never
          // reveal whether the account exists.
          this.submitted.set(true);
        }
      }
    });
  }
}
