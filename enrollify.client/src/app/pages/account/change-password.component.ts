import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { formatApiError } from '../../core/utils/format-api-error';

// Change password for every authenticated role. Two modes:
// - voluntary: reached from the shell's user menu; has a cancel affordance.
// - forced: mustChangePassword sessions are quarantined here by authGuard until
//   they set their own password (temporary-password accounts).
@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-md">
      <h1 class="text-2xl font-bold text-gray-900">Change Password</h1>
      @if (forced()) {
        <p class="mt-1 text-sm text-gray-500">You're using a temporary password &mdash; set your own to continue.</p>
      } @else {
        <p class="mt-1 text-sm text-gray-500">Choose a new password for your account.</p>
      }

      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
        @if (error()) {
          <div class="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700 whitespace-pre-line">{{ error() }}</div>
        }

        <form (ngSubmit)="onSubmit()" class="space-y-4">
          <div>
            <label class="form-label">Current password</label>
            <input type="password" [(ngModel)]="currentPassword" name="currentPassword" required [disabled]="saving()"
                   autocomplete="current-password" class="form-input" />
            @if (forced()) {
              <p class="mt-1 text-xs text-gray-400">This is the temporary password you signed in with.</p>
            }
          </div>
          <div>
            <label class="form-label">New password</label>
            <input type="password" [(ngModel)]="newPassword" name="newPassword" required [disabled]="saving()"
                   autocomplete="new-password" class="form-input" placeholder="At least 8 characters" />
          </div>
          <div>
            <label class="form-label">Confirm new password</label>
            <input type="password" [(ngModel)]="confirmPassword" name="confirmPassword" required [disabled]="saving()"
                   autocomplete="new-password" class="form-input" />
            @if (newPassword && confirmPassword && newPassword !== confirmPassword) {
              <p class="mt-1 text-xs text-red-500">Passwords do not match</p>
            }
          </div>

          <div class="flex items-center gap-3 pt-2">
            <button type="submit" [disabled]="saving() || !currentPassword || !newPassword || newPassword !== confirmPassword"
                    class="btn btn-primary disabled:opacity-50">
              {{ saving() ? 'Saving...' : 'Change password' }}
            </button>
            @if (!forced()) {
              <button type="button" (click)="goHome()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
            }
          </div>
        </form>
      </div>
    </div>
  `
})
export class ChangePasswordComponent {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  saving = signal(false);
  error = signal('');

  forced = computed(() => this.auth.mustChangePassword());

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private router: Router,
    private notify: NotificationService
  ) {}

  onSubmit(): void {
    if (!this.currentPassword || !this.newPassword || this.newPassword !== this.confirmPassword) return;
    this.saving.set(true);
    this.error.set('');
    this.api.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.saving.set(false);
        // The server cleared the temporary-password flag; mirror it so the
        // authGuard quarantine lifts, then land on the role's home.
        this.auth.clearMustChangePassword();
        this.notify.success('Password changed.');
        this.goHome();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(formatApiError(err, 'Failed to change password.'));
      }
    });
  }

  goHome(): void {
    this.router.navigate([this.landingRouteForRole(this.auth.userRole())]);
  }

  private landingRouteForRole(role: string): string {
    if (role === 'Parent') return '/parent/dashboard';
    if (role === 'SuperAdmin') return '/super/tenants';
    return '/dashboard';
  }
}
