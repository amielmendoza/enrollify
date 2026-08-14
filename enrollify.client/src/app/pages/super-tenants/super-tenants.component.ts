import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { Tenant } from '../../core/models';

/// SuperAdmin-only page to manage the list of schools (tenants).
@Component({
  selector: 'app-super-tenants',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div>
      <div class="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Schools</h1>
          <p class="mt-1 text-sm text-gray-500">All schools using the Enrollify platform. Create new schools, deactivate ones that are no longer using the system, or update their contact info.</p>
        </div>
        <button (click)="openCreate()" class="inline-flex items-center gap-2 rounded-xl bg-[#4361ee] px-4 py-2 text-sm font-semibold text-white hover:bg-[#3a56d4]">
          <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>
          Add School
        </button>
      </div>

      @if (error()) {
        <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ error() }}</div>
      }

      <div class="mt-6 bg-white rounded-xl border border-gray-200 overflow-hidden">
        <table class="w-full">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Subdomain</th>
              <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Contact</th>
              <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Status</th>
              <th class="px-4 py-3 text-right"></th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            @for (t of tenants(); track t.id) {
              <tr class="hover:bg-gray-50">
                <td class="px-4 py-3">
                  <p class="font-medium text-gray-900">{{ t.name }}</p>
                  <p class="text-xs text-gray-500">{{ t.address || '—' }}</p>
                </td>
                <td class="px-4 py-3 text-sm font-mono text-gray-600">{{ t.subdomain }}</td>
                <td class="px-4 py-3 text-sm text-gray-600">
                  <p>{{ t.contactEmail || '—' }}</p>
                  <p class="text-xs text-gray-400">{{ t.contactPhone || '' }}</p>
                </td>
                <td class="px-4 py-3 text-center">
                  @if (t.isActive) {
                    <span class="inline-flex items-center rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700">Active</span>
                  } @else {
                    <span class="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600">Inactive</span>
                  }
                </td>
                <td class="px-4 py-3 text-right">
                  <div class="inline-flex items-center gap-2">
                    <a [routerLink]="['/super/tenants', t.id, 'admins']" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Manage admins</a>
                    <button (click)="openEdit(t)" class="text-xs px-3 py-1.5 rounded-lg border border-gray-200 text-gray-700 hover:bg-gray-50">Edit</button>
                  </div>
                </td>
              </tr>
            }
            @empty {
              <tr><td colspan="5" class="px-4 py-12 text-center text-gray-400">No schools yet.</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (showModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/50" (click)="close()">
          <div class="bg-white rounded-xl shadow-xl w-full max-w-lg p-6" (click)="$event.stopPropagation()">
            <h3 class="text-lg font-semibold text-gray-900">{{ editing() ? 'Edit School' : 'Add School' }}</h3>

            <div class="mt-5 space-y-4">
              <div>
                <label class="form-label">Name *</label>
                <input type="text" [(ngModel)]="form.name" class="form-input" placeholder="Quezon City Science HS" />
              </div>
              <div>
                <label class="form-label">Subdomain *</label>
                <input type="text" [(ngModel)]="form.subdomain" class="form-input font-mono" placeholder="qcshs" />
                <p class="text-xs text-gray-400 mt-1">Lowercase letters, digits, hyphens. Used as a stable identifier — change with care.</p>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label class="form-label">Contact Email</label>
                  <input type="email" [(ngModel)]="form.contactEmail" class="form-input" />
                </div>
                <div>
                  <label class="form-label">Contact Phone</label>
                  <input type="text" [(ngModel)]="form.contactPhone" class="form-input" />
                </div>
              </div>
              <div>
                <label class="form-label">Address</label>
                <input type="text" [(ngModel)]="form.address" class="form-input" />
              </div>
              @if (editing()) {
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [(ngModel)]="form.isActive" />
                  Active (schools that aren't active won't appear in the public school directory)
                </label>
              }
            </div>

            <div class="mt-6 flex items-center justify-end gap-3">
              <button (click)="close()" class="text-sm px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
              <button (click)="save()" [disabled]="saving()" class="text-sm px-5 py-2 rounded-lg bg-[#4361ee] text-white font-semibold hover:bg-[#3a56d4] disabled:opacity-60">
                {{ saving() ? 'Saving...' : (editing() ? 'Save' : 'Add School') }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SuperTenantsComponent implements OnInit {
  tenants = signal<Tenant[]>([]);
  showModal = signal(false);
  editing = signal<Tenant | null>(null);
  saving = signal(false);
  error = signal('');

  form = { name: '', subdomain: '', contactEmail: '', contactPhone: '', address: '', isActive: true };

  constructor(private api: ApiService) {}

  ngOnInit() { this.load(); }

  load() {
    this.api.getAllTenants().subscribe(list => this.tenants.set(list));
  }

  openCreate() {
    this.editing.set(null);
    this.form = { name: '', subdomain: '', contactEmail: '', contactPhone: '', address: '', isActive: true };
    this.error.set('');
    this.showModal.set(true);
  }

  openEdit(t: Tenant) {
    this.editing.set(t);
    this.form = {
      name: t.name, subdomain: t.subdomain,
      contactEmail: t.contactEmail ?? '', contactPhone: t.contactPhone ?? '', address: t.address ?? '',
      isActive: t.isActive
    };
    this.error.set('');
    this.showModal.set(true);
  }

  close() { this.showModal.set(false); }

  save() {
    this.error.set('');
    if (!this.form.name || !this.form.subdomain) {
      this.error.set('Name and subdomain are required.');
      return;
    }
    this.saving.set(true);

    const payload = {
      name: this.form.name,
      subdomain: this.form.subdomain,
      contactEmail: this.form.contactEmail || null,
      contactPhone: this.form.contactPhone || null,
      address: this.form.address || null
    };

    const target = this.editing();
    const obs = target
      ? this.api.updateTenant(target.id, { ...payload, isActive: this.form.isActive })
      : this.api.createTenant(payload);

    obs.subscribe({
      next: () => { this.saving.set(false); this.showModal.set(false); this.load(); },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Failed to save school.');
      }
    });
  }
}
