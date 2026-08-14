import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { TenantUser, PublicTenant } from '../../core/models';

/// SuperAdmin-only page to manage Admin/Registrar accounts for one school.
/// Reached from /super/tenants → "Manage admins" on a row.
@Component({
  selector: 'app-tenant-admins',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div>
      <a routerLink="/super/tenants" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" /></svg>
        Back to schools
      </a>

      <div class="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">School Admins</h1>
          <p class="mt-1 text-sm text-gray-500">
            @if (tenant()) {
              Manage admin accounts for <span class="font-semibold text-gray-700">{{ tenant()!.name }}</span>. Each Admin can then create and manage Registrars within their own school.
            } @else {
              Loading school...
            }
          </p>
        </div>
        <button (click)="openCreate()" class="inline-flex items-center gap-2 rounded-xl bg-[#4361ee] px-4 py-2 text-sm font-semibold text-white hover:bg-[#3a56d4]">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>
          Add Admin
        </button>
      </div>

      @if (error()) {
        <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ error() }}</div>
      }
      @if (notice()) {
        <div class="mt-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700">{{ notice() }}</div>
      }

      <div class="mt-6 bg-white rounded-xl border border-gray-200 overflow-hidden">
        <table class="w-full">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
              <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Status</th>
              <th class="px-4 py-3 text-right"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            @for (u of users(); track u.id) {
              <tr class="hover:bg-gray-50" [class.opacity-60]="!u.isActive">
                <td class="px-4 py-3 text-sm text-gray-900">{{ u.firstName }} {{ u.lastName }}</td>
                <td class="px-4 py-3 text-sm text-gray-700">{{ u.email }}</td>
                <td class="px-4 py-3 text-center">
                  @if (u.isActive) {
                    <span class="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">Active</span>
                  } @else {
                    <span class="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600">Inactive</span>
                  }
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="inline-flex items-center gap-2">
                    @if (u.role !== 'SuperAdmin') {
                      <button (click)="openEdit(u)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Edit</button>
                      <button (click)="openReset(u)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Reset password</button>
                    }
                  </div>
                </td>
              </tr>
            }
            @empty {
              <tr><td colspan="4" class="px-4 py-12 text-center text-gray-400">No admins yet. Add one to get started.</td></tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Add / Edit modal -->
      @if (showUserModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeUserModal()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">{{ editing() ? 'Edit Admin' : 'Add Admin' }}</h3>

            <div class="mt-5 space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">First Name *</label>
                  <input type="text" [(ngModel)]="form.firstName" class="form-input" />
                </div>
                <div>
                  <label class="form-label">Last Name *</label>
                  <input type="text" [(ngModel)]="form.lastName" class="form-input" />
                </div>
              </div>
              <div>
                <label class="form-label">Email *</label>
                <input type="email" [(ngModel)]="form.email" class="form-input" [disabled]="!!editing()" />
                @if (editing()) { <p class="text-xs text-gray-400 mt-1">Email cannot be changed after creation.</p> }
              </div>
              @if (!editing()) {
                <div>
                  <label class="form-label">Initial Password *</label>
                  <input type="text" [(ngModel)]="form.password" class="form-input font-mono" placeholder="At least 8 characters" />
                  <p class="text-xs text-gray-400 mt-1">The admin can change this on their first sign-in.</p>
                </div>
              }
              @if (editing()) {
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [(ngModel)]="form.isActive" />
                  Active (inactive admins can't sign in)
                </label>
              }
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeUserModal()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="saveUser()" [disabled]="saving()" class="text-sm px-5 py-2 rounded-lg bg-[#4361ee] text-white font-semibold hover:bg-[#3a56d4] disabled:opacity-60">
                {{ saving() ? 'Saving...' : (editing() ? 'Save' : 'Add Admin') }}
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Reset password modal -->
      @if (resettingUser()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="closeReset()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">Reset Password</h3>
            <p class="mt-1 text-sm text-gray-500">For <span class="font-medium">{{ resettingUser()!.firstName }} {{ resettingUser()!.lastName }}</span> ({{ resettingUser()!.email }}).</p>

            <div class="mt-4">
              <label class="form-label">New Password *</label>
              <input type="text" [(ngModel)]="resetPassword" class="form-input font-mono" placeholder="At least 8 characters" />
              <p class="text-xs text-gray-400 mt-1">Communicate this password to the user out of band.</p>
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="closeReset()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="confirmReset()" [disabled]="saving()" class="text-sm px-5 py-2 rounded-lg bg-[#4361ee] text-white font-semibold hover:bg-[#3a56d4] disabled:opacity-60">
                {{ saving() ? 'Saving...' : 'Reset Password' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class TenantAdminsComponent implements OnInit {
  tenantId!: string;
  tenant = signal<PublicTenant | null>(null);
  users = signal<TenantUser[]>([]);

  showUserModal = signal(false);
  editing = signal<TenantUser | null>(null);
  saving = signal(false);
  error = signal('');
  notice = signal('');

  form = { email: '', firstName: '', lastName: '', password: '', isActive: true };

  resettingUser = signal<TenantUser | null>(null);
  resetPassword = '';

  constructor(private api: ApiService, private route: ActivatedRoute) {}

  ngOnInit() {
    this.tenantId = this.route.snapshot.paramMap.get('tenantId') ?? '';
    if (!this.tenantId) return;

    this.api.getPublicTenant(this.tenantId).subscribe(t => this.tenant.set(t));
    this.load();
  }

  load() {
    this.api.getTenantUsers(this.tenantId).subscribe(list => this.users.set(list));
  }

  badgeClass(role: string): string {
    if (role === 'SuperAdmin') return 'bg-purple-100 text-purple-700';
    if (role === 'Admin') return 'bg-blue-100 text-blue-700';
    if (role === 'Registrar') return 'bg-amber-100 text-amber-700';
    return 'bg-gray-100 text-gray-600';
  }

  openCreate() {
    this.editing.set(null);
    this.form = { email: '', firstName: '', lastName: '', password: '', isActive: true };
    this.error.set(''); this.notice.set('');
    this.showUserModal.set(true);
  }

  openEdit(u: TenantUser) {
    if (u.role === 'SuperAdmin') return;
    this.editing.set(u);
    this.form = { email: u.email, firstName: u.firstName, lastName: u.lastName, password: '', isActive: u.isActive };
    this.error.set(''); this.notice.set('');
    this.showUserModal.set(true);
  }

  closeUserModal() { this.showUserModal.set(false); }

  saveUser() {
    this.error.set('');
    if (!this.form.firstName || !this.form.lastName) { this.error.set('Name is required.'); return; }
    if (!this.editing() && (!this.form.email || !this.form.password)) { this.error.set('Email and initial password are required.'); return; }
    if (!this.editing() && this.form.password.length < 8) { this.error.set('Password must be at least 8 characters.'); return; }

    this.saving.set(true);
    const target = this.editing();
    const obs = target
      ? this.api.updateTenantUser(this.tenantId, target.id, {
          firstName: this.form.firstName, lastName: this.form.lastName,
          isActive: this.form.isActive
        })
      : this.api.createTenantUser(this.tenantId, {
          email: this.form.email, firstName: this.form.firstName, lastName: this.form.lastName,
          password: this.form.password
        });

    obs.subscribe({
      next: () => { this.saving.set(false); this.showUserModal.set(false); this.notice.set(target ? 'Admin updated.' : 'Admin created.'); this.load(); },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to save admin.');
      }
    });
  }

  openReset(u: TenantUser) {
    this.resettingUser.set(u);
    this.resetPassword = '';
    this.error.set(''); this.notice.set('');
  }

  closeReset() { this.resettingUser.set(null); }

  confirmReset() {
    const u = this.resettingUser();
    if (!u) return;
    if (!this.resetPassword || this.resetPassword.length < 8) { this.error.set('Password must be at least 8 characters.'); return; }
    this.saving.set(true);
    this.api.resetTenantUserPassword(this.tenantId, u.id, this.resetPassword).subscribe({
      next: () => { this.saving.set(false); this.resettingUser.set(null); this.notice.set(`Password reset for ${u.email}.`); },
      error: (err) => { this.saving.set(false); this.error.set(err.error?.error || 'Failed to reset password.'); }
    });
  }
}
