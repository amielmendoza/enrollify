import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchoolYearService } from '../../../core/services/school-year.service';
import { AuthService } from '../../../core/services/auth.service';
import { LoadingComponent } from '../../../core/components/loading.component';
import { Sf1, WorklistSection } from '../../../core/models';
import { formatApiError } from '../../../core/utils/format-api-error';

// Working copy of DepEd School Form 1 (school register) for one section, for checking against LIS.
// The section picker is fed by the LIS worklist's year-scoped section list.
@Component({
  selector: 'app-sf1-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingComponent],
  template: `
    <div>
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">SF1 Register</h1>
          <p class="mt-1 text-sm text-gray-500">School Form 1 working copy for one section</p>
        </div>
        <div class="flex gap-2">
          <button type="button" (click)="exportCsv()" [disabled]="exporting() || !sf1()"
                  class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-60">
            {{ exporting() ? 'Exporting...' : 'Download CSV' }}
          </button>
          <button type="button" (click)="print()" [disabled]="!sf1()"
                  class="rounded-lg bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85] disabled:opacity-60">
            Print
          </button>
        </div>
      </div>

      <div class="mt-4 rounded-lg border border-[#E2D9C2] bg-[#FBF7EA] px-4 py-3 text-sm text-gray-700">
        Working copy for checking against LIS. The official SF1 is generated in LIS.
      </div>

      <!-- Filters -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-4 flex flex-wrap items-end justify-between gap-3">
        <div class="flex flex-wrap items-end gap-3 min-w-0">
          <div>
            <label class="form-label" for="sf1-sy">School Year</label>
            <select id="sf1-sy" [(ngModel)]="schoolYear" (ngModelChange)="onYearChange()" class="form-input w-auto">
              @for (sy of syService.schoolYears(); track sy.id) { <option [value]="sy.name">{{ sy.name }}{{ sy.isActive ? ' (active)' : '' }}</option> }
            </select>
          </div>
          <div class="min-w-0">
            <label class="form-label" for="sf1-section">Section</label>
            <select id="sf1-section" [(ngModel)]="sectionId" (ngModelChange)="onSectionChange()" [disabled]="sectionsLoading()" class="form-input w-auto max-w-full">
              <option value="">{{ sectionsLoading() ? 'Loading sections...' : (sections().length ? 'Select a section' : 'No sections this year') }}</option>
              @for (s of sections(); track s.sectionId) { <option [value]="s.sectionId">{{ s.gradeLevel }} — {{ s.sectionName }}</option> }
            </select>
          </div>
        </div>
        @if (sf1(); as r) {
          <div class="text-sm text-gray-600 min-w-0">
            <p class="font-medium text-gray-900 truncate">{{ r.header.school.schoolName || 'School' }}</p>
            <p class="text-xs text-gray-500">
              @if (r.header.school.schoolId) { School ID <span class="folio-mono">{{ r.header.school.schoolId }}</span> }
              @else {
                School ID not set.
                @if (isAdmin()) { <a routerLink="/settings" class="text-[#0038A8] hover:text-[#002B85] font-medium">Add it in Settings &rarr; School Profile</a> }
              }
            </p>
          </div>
        }
      </div>

      @if (sectionsFailed()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
          <p class="px-6 py-8 text-center text-red-600">The sections couldn't be loaded. Refresh to try again.</p>
        </div>
      } @else if (!sectionId) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
          <p class="px-6 py-8 text-center text-gray-400">Choose a section to see its register.</p>
        </div>
      } @else if (loadFailed() && !sf1()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
          <p class="px-6 py-8 text-center text-red-600">The register couldn't be loaded. Refresh to try again.</p>
        </div>
      } @else if (loading() && !sf1()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]"><app-loading label="Loading register..." /></div>
      } @else {
        @if (sf1(); as r) {
          <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] min-w-0" [class.opacity-60]="loading()">
            <div class="px-4 sm:px-5 pt-4 pb-3 border-b border-gray-100 flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
              <div>
                <p class="folio-eyebrow">{{ r.header.gradeLevel }} — {{ r.header.sectionName }}</p>
                <p class="text-xs text-gray-500 mt-0.5">
                  Adviser: {{ r.header.adviser || 'not set' }} · School year <span class="folio-mono">{{ r.header.school.schoolYear }}</span>
                </p>
              </div>
              <p class="text-xs text-gray-600">
                Male <span class="folio-mono font-semibold text-gray-900">{{ r.male }}</span> ·
                Female <span class="folio-mono font-semibold text-gray-900">{{ r.female }}</span> ·
                Sex not set <span class="folio-mono font-semibold text-gray-900">{{ r.notSet }}</span> ·
                Total <span class="folio-mono font-semibold text-gray-900">{{ r.total }}</span>
              </p>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="text-left border-b border-gray-200 bg-gray-50">
                    <th scope="col" class="px-3 py-2 font-semibold text-gray-700 whitespace-nowrap">#</th>
                    @for (h of headings; track h) {
                      <th scope="col" class="px-3 py-2 font-semibold text-gray-700 whitespace-nowrap">{{ h }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (l of r.learners; track $index) {
                    <tr class="border-b border-gray-100">
                      <td class="px-3 py-2 text-gray-500 folio-mono">{{ $index + 1 }}</td>
                      <td class="px-3 py-2 folio-mono whitespace-nowrap">{{ l.lrn || '' }}</td>
                      <td class="px-3 py-2 font-medium text-gray-900 whitespace-nowrap">{{ l.name }}</td>
                      <td class="px-3 py-2">{{ l.sex }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.birthDate ? (l.birthDate | date:'MM/dd/yyyy') : '' }}</td>
                      <td class="px-3 py-2">{{ l.age }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.motherTongue }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.ipGroup }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.religion }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.houseStreet }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.barangay }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.cityMunicipality }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.province }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.fatherName }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.motherMaidenName }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.guardianName }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.guardianRelationship }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.contact }}</td>
                      <td class="px-3 py-2 whitespace-nowrap">{{ l.remarks }}</td>
                    </tr>
                  } @empty {
                    <tr><td [attr.colspan]="headings.length + 1" class="px-5 py-6 text-center text-gray-400">No learners in this section.</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      }
    </div>
  `
})
export class Sf1RegisterComponent implements OnInit {
  sf1 = signal<Sf1 | null>(null);
  sections = signal<WorklistSection[]>([]);
  sectionsLoading = signal(true);
  sectionsFailed = signal(false);
  loading = signal(false);
  loadFailed = signal(false);
  exporting = signal(false);

  schoolYear = '';
  sectionId = '';
  private sectionSeq = 0;
  private requestSeq = 0;

  readonly headings = ['LRN', 'Name', 'Sex', 'Birth date', 'Age', 'Mother tongue', 'IP group', 'Religion',
    'House/Street', 'Barangay', 'City/Municipality', 'Province', "Father's name", "Mother's maiden name",
    'Guardian', 'Relationship', 'Contact', 'Remarks'];

  isAdmin = computed(() => this.auth.userRole() === 'Admin');

  constructor(
    private api: ApiService,
    public syService: SchoolYearService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private notify: NotificationService) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    const initialSection = qp.get('sectionId') ?? '';
    this.syService.ensureLoaded().subscribe({
      next: () => {
        this.schoolYear = qp.get('schoolYear') ?? this.syService.activeName() ?? '';
        this.loadSections(initialSection);
      },
      // Year list unavailable: the server still resolves the active year (or explains why not).
      error: () => { this.schoolYear = qp.get('schoolYear') ?? ''; this.loadSections(initialSection); }
    });
  }

  onYearChange(): void {
    // Sections belong to a year; keep the selection only if it exists in the new year.
    this.loadSections(this.sectionId);
  }

  onSectionChange(): void {
    this.syncUrl();
    this.loadRegister();
  }

  private loadSections(preferredSectionId: string): void {
    const seq = ++this.sectionSeq;
    this.sectionsLoading.set(true);
    this.sectionsFailed.set(false);
    this.api.getLisWorklist(this.schoolYear || undefined).subscribe({
      next: w => {
        if (seq !== this.sectionSeq) return;
        // The "no section" row has a null id and has no SF1.
        const list = w.sections.filter(s => !!s.sectionId);
        this.sections.set(list);
        this.sectionsLoading.set(false);
        this.sectionId = list.some(s => s.sectionId === preferredSectionId) ? preferredSectionId : '';
        this.syncUrl();
        this.loadRegister();
      },
      error: (err) => {
        if (seq !== this.sectionSeq) return;
        this.sectionsLoading.set(false);
        this.sectionsFailed.set(true);
        this.sections.set([]);
        this.sectionId = '';
        this.sf1.set(null);
        this.notify.error(formatApiError(err, 'Failed to load the sections.'));
      }
    });
  }

  private loadRegister(): void {
    const seq = ++this.requestSeq;
    if (!this.sectionId) {
      this.sf1.set(null);
      this.loading.set(false);
      this.loadFailed.set(false);
      return;
    }
    this.loading.set(true);
    this.api.getSf1(this.sectionId, this.schoolYear || undefined).subscribe({
      next: r => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(false);
        this.sf1.set(r);
      },
      error: (err) => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(true);
        this.sf1.set(null);
        this.notify.error(formatApiError(err, 'Failed to load the register.'));
      }
    });
  }

  print(): void {
    if (!this.sectionId) return;
    const sy = this.schoolYear ? '?schoolYear=' + encodeURIComponent(this.schoolYear) : '';
    window.open('/print/sf1/' + this.sectionId + sy, '_blank');
  }

  exportCsv(): void {
    if (this.exporting() || !this.sectionId) return;
    this.exporting.set(true);
    const h = this.sf1()?.header;
    const name = h ? `sf1_${h.gradeLevel}_${h.sectionName}`.replace(/[^A-Za-z0-9]+/g, '-') : `sf1_${this.sectionId}`;
    this.api.exportSf1(this.sectionId, this.schoolYear || undefined).subscribe({
      next: blob => {
        this.exporting.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${name}.csv`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => {
        this.exporting.set(false);
        this.notify.error(formatApiError(err, 'Failed to export CSV.'));
      }
    });
  }

  private syncUrl(): void {
    const q: Record<string, string> = {};
    if (this.sectionId) q['sectionId'] = this.sectionId;
    if (this.schoolYear) q['schoolYear'] = this.schoolYear;
    this.router.navigate([], { relativeTo: this.route, queryParams: q, replaceUrl: true });
  }
}
