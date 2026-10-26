import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';
import { PsgcPlace, PsgcService } from '../../core/services/psgc.service';
import { SchoolProfile } from '../../core/models';
import { formatApiError } from '../../core/utils/format-api-error';
import { LoadingComponent } from '../../core/components/loading.component';

/** Settings tab: the school's DepEd profile, printed in the header of the DepEd reports. */
@Component({
  selector: 'app-school-profile-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingComponent],
  template: `
    <div class="mt-6 space-y-6">
      <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
        <h2 class="text-lg font-semibold text-gray-900 mb-1">School Profile</h2>
        <p class="text-sm text-gray-500 mb-4">These details appear in the header of the DepEd reports (SF1, LIS worklist, quick count).</p>

        @if (loading()) {
          <app-loading label="Loading school profile..." />
        } @else if (loadError()) {
          <p class="text-sm text-red-600">{{ loadError() }}</p>
        } @else {
          @if (!isAdmin()) {
            <p class="mb-4 rounded-lg border border-[#E2D9C2] bg-[#FBF7EA] px-4 py-2.5 text-sm text-gray-600">Only an Admin can change the school profile.</p>
          }
          <form (ngSubmit)="save()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div class="sm:col-span-2">
              <label class="form-label" for="sp-name">School name</label>
              <input id="sp-name" type="text" class="form-input" [value]="schoolName()" disabled />
            </div>
            <div>
              <label class="form-label" for="sp-id">DepEd School ID</label>
              <input id="sp-id" name="depEdSchoolId" type="text" class="form-input" inputmode="numeric" maxlength="6"
                     placeholder="6 digits" [(ngModel)]="form.depEdSchoolId" [disabled]="!isAdmin()" />
            </div>
            <div>
              <label class="form-label" for="sp-region">Region</label>
              <select id="sp-region" name="depEdRegionCode" class="form-input"
                      [ngModel]="form.depEdRegionCode" (ngModelChange)="onRegionChange($event)" [disabled]="!isAdmin()">
                <option [ngValue]="null">Select region</option>
                @if (orphanRegion()) {
                  <option [ngValue]="form.depEdRegionCode">{{ form.depEdRegionName || form.depEdRegionCode }}</option>
                }
                @for (r of regions(); track r.code) { <option [ngValue]="r.code">{{ r.name }}</option> }
              </select>
            </div>
            <div>
              <label class="form-label" for="sp-division">Division</label>
              <input id="sp-division" name="depEdDivision" type="text" class="form-input" maxlength="150"
                     [(ngModel)]="form.depEdDivision" [disabled]="!isAdmin()" />
            </div>
            <div>
              <label class="form-label" for="sp-district">District</label>
              <input id="sp-district" name="depEdDistrict" type="text" class="form-input" maxlength="150"
                     [(ngModel)]="form.depEdDistrict" [disabled]="!isAdmin()" />
            </div>
            <div class="sm:col-span-2">
              <label class="form-label" for="sp-head">School head</label>
              <input id="sp-head" name="schoolHeadName" type="text" class="form-input" maxlength="150"
                     placeholder="Name of the school head / principal" [(ngModel)]="form.schoolHeadName" [disabled]="!isAdmin()" />
            </div>
            @if (error()) {
              <p class="sm:col-span-2 text-sm text-red-600">{{ error() }}</p>
            }
            @if (isAdmin()) {
              <div class="sm:col-span-2">
                <button type="submit" [disabled]="saving()"
                        class="rounded-xl px-6 py-2.5 text-sm font-semibold text-white disabled:opacity-50 transition-colors" style="background-color: #0038A8;">
                  {{ saving() ? 'Saving...' : 'Save' }}
                </button>
              </div>
            }
          </form>
        }
      </div>
    </div>
  `
})
export class SchoolProfileSettingsComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private notify = inject(NotificationService);
  private psgc = inject(PsgcService);

  loading = signal(true);
  saving = signal(false);
  loadError = signal('');
  error = signal('');
  schoolName = signal('');
  regions = signal<PsgcPlace[]>([]);

  isAdmin = computed(() => this.auth.userRole() === 'Admin');

  form: {
    depEdSchoolId: string | null;
    depEdRegionCode: string | null;
    depEdRegionName: string | null;
    depEdDivision: string | null;
    depEdDistrict: string | null;
    schoolHeadName: string | null;
  } = { depEdSchoolId: null, depEdRegionCode: null, depEdRegionName: null, depEdDivision: null, depEdDistrict: null, schoolHeadName: null };

  ngOnInit(): void {
    // Region list is a convenience: if it fails to load, the saved region still shows via the orphan option.
    this.psgc.index().subscribe({ next: idx => this.regions.set(idx.regions), error: () => {} });
    this.api.getSchoolProfile().subscribe({
      next: p => { this.apply(p); this.loading.set(false); },
      error: err => { this.loadError.set(formatApiError(err, 'Failed to load the school profile.')); this.loading.set(false); }
    });
  }

  /** True when a saved region isn't in the PSGC list (list still loading/failed, or legacy value). */
  orphanRegion(): boolean {
    const code = this.form.depEdRegionCode;
    return !!code && !this.regions().some(r => r.code === code);
  }

  onRegionChange(code: string | null): void {
    const region = this.regions().find(r => r.code === code);
    if (region) {
      this.form.depEdRegionCode = region.code;
      this.form.depEdRegionName = region.name;
    } else if (!code) {
      this.form.depEdRegionCode = null;
      this.form.depEdRegionName = null;
    }
  }

  save(): void {
    if (!this.isAdmin() || this.saving()) return;
    this.error.set('');
    this.saving.set(true);
    const f = this.form;
    const blank = (v: string | null) => (v?.trim() ? v.trim() : null);
    this.api.updateSchoolProfile({
      depEdSchoolId: blank(f.depEdSchoolId),
      depEdRegionCode: f.depEdRegionCode || null,
      depEdRegionName: f.depEdRegionName || null,
      depEdDivision: blank(f.depEdDivision),
      depEdDistrict: blank(f.depEdDistrict),
      schoolHeadName: blank(f.schoolHeadName)
    }).subscribe({
      next: p => { this.apply(p); this.saving.set(false); this.notify.success('School profile saved.'); },
      error: err => { this.saving.set(false); this.error.set(formatApiError(err, 'Failed to save the school profile.')); }
    });
  }

  private apply(p: SchoolProfile): void {
    this.schoolName.set(p.schoolName ?? '');
    this.form = {
      depEdSchoolId: p.depEdSchoolId ?? null,
      depEdRegionCode: p.depEdRegionCode ?? null,
      depEdRegionName: p.depEdRegionName ?? null,
      depEdDivision: p.depEdDivision ?? null,
      depEdDistrict: p.depEdDistrict ?? null,
      schoolHeadName: p.schoolHeadName ?? null
    };
  }
}
