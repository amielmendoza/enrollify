import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { NotificationService } from '../../core/services/notification.service';
import { formatApiError } from '../../core/utils/format-api-error';

// Public reset-completion page, reached from the emailed link (?email=&token=).
@Component({
  selector: 'app-reset-password',
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
            @if (!email || !token) {
              <h2 class="folio-display text-2xl font-black tracking-tight">Invalid reset link.</h2>
              <p class="mt-1.5 text-sm text-gray-500">This link is missing its reset code — it may have been cut short by your mail client.</p>
              <a routerLink="/forgot-password" class="mt-4 inline-block text-sm font-semibold text-[#0038A8] hover:underline">Request a new link &rarr;</a>
            } @else {
              <h2 class="folio-display text-2xl font-black tracking-tight sm:text-3xl">Set a new password.</h2>
              <p class="mt-1.5 text-sm text-gray-500">For <span class="folio-mono">{{ email }}</span></p>

              @if (error()) {
                <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700 whitespace-pre-line">
                  {{ error() }}
                  <p class="mt-2">The link may have expired &mdash; <a routerLink="/forgot-password" class="font-semibold underline">request a new one</a>.</p>
                </div>
              }

              <form (ngSubmit)="onSubmit()" class="mt-8 space-y-5">
                <div>
                  <label class="form-label">New password</label>
                  <input type="password" [(ngModel)]="newPassword" name="newPassword" required [disabled]="loading()" autocomplete="new-password"
                         class="form-input" placeholder="At least 8 characters" />
                </div>
                <div>
                  <label class="form-label">Confirm new password</label>
                  <input type="password" [(ngModel)]="confirmPassword" name="confirmPassword" required [disabled]="loading()" autocomplete="new-password"
                         class="form-input" placeholder="Repeat the new password" />
                  @if (newPassword && confirmPassword && newPassword !== confirmPassword) {
                    <p class="mt-1 text-xs text-red-500">Passwords do not match</p>
                  }
                </div>
                <button type="submit" [disabled]="loading() || !newPassword || newPassword !== confirmPassword"
                        class="flex w-full items-center justify-center gap-2 rounded-md bg-[#0038A8] py-3 text-sm font-semibold text-white shadow-sm hover:bg-[#002B85] transition-colors disabled:opacity-60 disabled:cursor-not-allowed">
                  {{ loading() ? 'Resetting...' : 'Reset password' }}
                </button>
              </form>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class ResetPasswordComponent implements OnInit {
  email = '';
  token = '';
  newPassword = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router,
    private notify: NotificationService
  ) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.email = qp.get('email') ?? '';
    this.token = qp.get('token') ?? '';
  }

  onSubmit(): void {
    if (!this.newPassword || this.newPassword !== this.confirmPassword) return;
    this.loading.set(true);
    this.error.set('');
    this.api.resetPassword(this.email, this.token, this.newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.notify.success('Password reset — sign in with your new password.');
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.status === 0
          ? "Can't reach the server. Please try again in a moment."
          : formatApiError(err, 'Password reset failed.'));
      }
    });
  }
}
