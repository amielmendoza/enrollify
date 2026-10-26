import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SchoolYearService } from '../../../core/services/school-year.service';
import { AuthService } from '../../../core/services/auth.service';
import { LoadingComponent } from '../../../core/components/loading.component';
import { LisWorklist, WorklistLearner, WorklistSection } from '../../../core/models';
import { formatApiError } from '../../../core/utils/format-api-error';

const NONE_KEY = 'none';

// The registrar's per-section checklist for encoding learners in DepEd's LIS. The section list
// shows progress; picking a section loads its learners in two groups (Continuing / Search or
// register). The chosen section (or the no-section bucket) lives in the URL.
@Component({
  selector: 'app-lis-worklist',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingComponent],
  template: `
    <div>
      <div>
        <h1 class="text-2xl font-bold text-gray-900">LIS Worklist</h1>
        <p class="mt-1 text-sm text-gray-500">Track which learners you have encoded in DepEd's LIS, section by section</p>
      </div>

      <!-- School year -->
      <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <label class="form-label" for="lis-sy">School Year</label>
          <select id="lis-sy" [(ngModel)]="schoolYear" (ngModelChange)="onYearChange()" class="form-input w-auto">
            @for (sy of syService.schoolYears(); track sy.id) { <option [value]="sy.name">{{ sy.name }}{{ sy.isActive ? ' (active)' : '' }}</option> }
          </select>
        </div>
        @if (report(); as r) {
          <div class="text-sm text-gray-600 min-w-0">
            <p class="font-medium text-gray-900 truncate">{{ r.header.schoolName || 'School' }}</p>
            <p class="text-xs text-gray-500">
              @if (r.header.schoolId) { School ID <span class="folio-mono">{{ r.header.schoolId }}</span> }
              @else {
                School ID not set.
                @if (isAdmin()) { <a routerLink="/settings" class="text-[#0038A8] hover:text-[#002B85] font-medium">Add it in Settings &rarr; School Profile</a> }
              }
            </p>
          </div>
        }
      </div>

      @if (loadFailed() && !report()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]">
          <p class="px-6 py-8 text-center text-red-600">The worklist couldn't be loaded. Refresh to try again.</p>
        </div>
      } @else if (loading() && !report()) {
        <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2]"><app-loading label="Loading worklist..." /></div>
      } @else {
       @if (report(); as r) {
        <div class="mt-6 grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
          <!-- Section list -->
          <div class="bg-white rounded-xl border border-[#E2D9C2] lg:col-span-1 min-w-0">
            <p class="folio-eyebrow px-4 pt-4 pb-2">Sections</p>
            @if (r.sections.length === 0) {
              <p class="px-4 pb-6 text-sm text-gray-400">No listed learners in this school year yet. Learners appear here once their enrollment is approved.</p>
            } @else {
              <ul class="divide-y divide-gray-100">
                @for (s of r.sections; track key(s)) {
                  <li>
                    <button type="button" (click)="selectSection(s)"
                            class="w-full text-left px-4 py-3 hover:bg-gray-50 transition-colors border-l-4"
                            [class.border-l-[#0038A8]]="key(s) === selectedKey()"
                            [class.bg-blue-50]="key(s) === selectedKey()"
                            [class.border-l-transparent]="key(s) !== selectedKey()"
                            [attr.aria-current]="key(s) === selectedKey() ? 'true' : null">
                      <div class="flex items-baseline justify-between gap-2">
                        <span class="text-sm font-medium text-gray-900 truncate">{{ s.gradeLevel }} &middot; {{ s.sectionName }}</span>
                        <span class="text-xs folio-mono shrink-0" [class.text-green-700]="s.pending === 0" [class.text-gray-600]="s.pending > 0">{{ s.encoded }}/{{ s.listed }}</span>
                      </div>
                      <p class="text-xs text-gray-500 truncate">{{ s.adviser || (s.sectionId ? 'No adviser' : 'Not yet assigned to a section') }}</p>
                      <div class="mt-2 h-1.5 w-full rounded-full bg-gray-100 overflow-hidden"
                           role="progressbar" [attr.aria-valuenow]="s.encoded" aria-valuemin="0" [attr.aria-valuemax]="s.listed" [attr.aria-label]="'Encoded in LIS'">
                        <div class="h-full rounded-full" [class.bg-green-600]="s.pending === 0" [class.bg-[#0038A8]]="s.pending > 0" [style.width.%]="percent(s)"></div>
                      </div>
                      <p class="mt-1 text-xs text-gray-500">{{ s.pending === 0 ? 'All encoded' : s.pending + ' pending' }}</p>
                    </button>
                  </li>
                }
              </ul>
            }
          </div>

          <!-- Selected section -->
          <div class="lg:col-span-2 min-w-0 space-y-6">
            @if (loading()) {
              <div class="bg-white rounded-xl border border-[#E2D9C2]"><app-loading label="Loading section..." /></div>
            } @else {
             @if (r.selected; as sel) {
              <div class="bg-white rounded-xl border border-[#E2D9C2] p-4 sm:p-5">
                <p class="folio-eyebrow">{{ sel.gradeLevel }}</p>
                <h2 class="text-lg font-semibold text-gray-900">{{ sel.sectionName }}</h2>
                <p class="text-sm text-gray-500">Adviser: {{ sel.adviser || 'not assigned' }}</p>
              </div>

              @if (fixList().length > 0) {
                <div class="rounded-xl border border-amber-300 bg-amber-50 p-4 sm:p-5">
                  <p class="text-sm font-semibold text-amber-900">Fix before encoding ({{ fixList().length }})</p>
                  <p class="text-xs text-amber-800 mt-0.5">These learners are missing details the LIS needs. Open the student to correct them.</p>
                  <ul class="mt-3 divide-y divide-amber-200">
                    @for (l of fixList(); track l.enrollmentId) {
                      <li class="py-2">
                        <a [routerLink]="'/students/' + l.studentId" class="text-sm font-medium text-[#0038A8] hover:text-[#002B85]">{{ l.name }}</a>
                        <ul class="mt-0.5 text-xs text-amber-900 list-disc pl-4">
                          @for (reason of l.fixReasons; track reason) { <li>{{ reason }}</li> }
                        </ul>
                      </li>
                    }
                  </ul>
                </div>
              }

              <!-- Selection actions -->
              <div class="flex flex-wrap items-center gap-2">
                <button type="button" (click)="markSelected(true)" [disabled]="busy() || toEncode().length === 0"
                        class="rounded-lg bg-[#0038A8] px-4 py-2 text-sm font-semibold text-white hover:bg-[#002B85] disabled:opacity-50 disabled:cursor-not-allowed">
                  Mark encoded ({{ toEncode().length }})
                </button>
                <button type="button" (click)="markSelected(false)" [disabled]="busy() || toUndo().length === 0"
                        class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">
                  Undo ({{ toUndo().length }})
                </button>
                @if (selectedIds().size > 0) {
                  <button type="button" (click)="selectedIds.set(emptySet)" class="text-sm text-gray-500 hover:text-gray-700 underline">Clear selection</button>
                }
              </div>

              <ng-container *ngTemplateOutlet="group; context: { title: 'Continuing', learners: sel.continuing }" />
              <ng-container *ngTemplateOutlet="group; context: { title: 'Search or register', learners: sel.searchOrRegister }" />
            } @else {
              <div class="bg-white rounded-xl border border-[#E2D9C2]">
                <p class="px-6 py-10 text-center text-gray-400">Pick a section to see its learners.</p>
              </div>
             }
            }
          </div>
        </div>
       }
      }
    </div>

    <ng-template #group let-title="title" let-learners="learners">
      <div class="bg-white rounded-xl border border-[#E2D9C2] min-w-0">
        <div class="px-4 sm:px-5 pt-4 pb-3 flex flex-wrap items-start justify-between gap-3 border-b border-gray-100">
          <div class="min-w-0">
            <h3 class="text-sm font-semibold text-gray-900">{{ title }} <span class="font-normal text-gray-500">({{ learners.length }})</span></h3>
          </div>
          <button type="button" (click)="markAllInGroup(learners)" [disabled]="busy() || readyIn(learners).length === 0"
                  class="rounded-lg border border-[#0038A8] px-3 py-1.5 text-xs font-semibold text-[#0038A8] hover:bg-blue-50 disabled:opacity-50 disabled:cursor-not-allowed disabled:border-gray-200 disabled:text-gray-400"
                  title="Marks every pending learner in this group as encoded, except those listed under Fix before encoding">
            Mark all in group ({{ readyIn(learners).length }})
          </button>
        </div>
        @if (learners.length === 0) {
          <p class="px-5 py-6 text-sm text-gray-400">Nobody in this group.</p>
        } @else {
          <label class="flex items-center gap-2 px-4 sm:px-5 py-2 text-xs text-gray-500 bg-gray-50 border-b border-gray-100 cursor-pointer">
            <input type="checkbox" [checked]="allSelected(learners)" (change)="toggleGroup(learners, $any($event.target).checked)" />
            Select all in group
          </label>
          <ul class="divide-y divide-gray-100">
            @for (l of learners; track l.enrollmentId) {
              <li class="px-4 sm:px-5 py-3 flex items-start gap-3" [class.bg-blue-50]="selectedIds().has(l.enrollmentId)">
                <input type="checkbox" class="mt-1 shrink-0" [checked]="selectedIds().has(l.enrollmentId)" (change)="toggle(l.enrollmentId)" [attr.aria-label]="'Select ' + l.name" />
                <div class="min-w-0 flex-1">
                  <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <span class="text-sm font-medium text-gray-900 break-words">{{ l.name }}</span>
                    @if (l.withdrawalType === 'Dropped') {
                      <span class="inline-flex rounded px-1.5 py-0.5 text-xs font-semibold bg-red-100 text-red-800" title="Dropped{{ l.withdrawalDate ? ' on ' + (l.withdrawalDate | date:'mediumDate') : '' }}">DRP</span>
                    } @else if (l.withdrawalType === 'TransferredOut') {
                      <span class="inline-flex rounded px-1.5 py-0.5 text-xs font-semibold bg-orange-100 text-orange-800" title="Transferred out{{ l.withdrawalDate ? ' on ' + (l.withdrawalDate | date:'mediumDate') : '' }}">T/O</span>
                    }
                    @if (l.fixReasons.length > 0) {
                      <span class="inline-flex rounded px-1.5 py-0.5 text-xs font-medium bg-amber-100 text-amber-800">Needs fix</span>
                    }
                  </div>
                  <p class="mt-0.5 text-xs text-gray-600 flex flex-wrap gap-x-3 gap-y-0.5">
                    <span>LRN <span class="folio-mono">{{ l.lrn || '—' }}</span>
                      @if (l.lrnNeedsReview) { <span class="ml-1 inline-flex rounded px-1.5 py-0.5 text-xs font-medium bg-amber-100 text-amber-800">Check LRN</span> }
                    </span>
                    <span>{{ l.sex }}</span>
                    @if (l.learnerType) { <span>{{ l.learnerType }}</span> }
                  </p>
                  @if (l.tags.length > 0) {
                    <div class="mt-1 flex flex-wrap gap-1">
                      @for (t of l.tags; track t) { <span class="inline-flex rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">{{ t }}</span> }
                    </div>
                  }
                  <p class="mt-1 text-xs" [class.text-green-700]="!!l.lisEncodedAt" [class.text-gray-400]="!l.lisEncodedAt">
                    @if (l.lisEncodedAt) { Encoded {{ l.lisEncodedAt | date:'mediumDate' }}{{ l.lisEncodedBy ? ' by ' + l.lisEncodedBy : '' }} }
                    @else { Not encoded yet }
                  </p>
                </div>
              </li>
            }
          </ul>
        }
      </div>
    </ng-template>
  `
})
export class LisWorklistComponent implements OnInit {
  report = signal<LisWorklist | null>(null);
  loading = signal(true);
  loadFailed = signal(false);
  busy = signal(false);
  readonly emptySet = new Set<string>();
  selectedIds = signal<Set<string>>(this.emptySet);

  schoolYear = '';
  // '' = nothing chosen, NONE_KEY = the no-section bucket, otherwise a section id.
  selectedKey = signal('');
  private requestSeq = 0;

  isAdmin = computed(() => this.auth.userRole() === 'Admin');

  private allLearners = computed<WorklistLearner[]>(() => {
    const sel = this.report()?.selected;
    return sel ? [...sel.continuing, ...sel.searchOrRegister] : [];
  });
  fixList = computed(() => this.allLearners().filter(l => l.fixReasons.length > 0));
  // Selected rows that can still be marked / unmarked (idempotent filtering keeps the counts honest).
  toEncode = computed(() => this.allLearners().filter(l => this.selectedIds().has(l.enrollmentId) && !l.lisEncodedAt));
  toUndo = computed(() => this.allLearners().filter(l => this.selectedIds().has(l.enrollmentId) && !!l.lisEncodedAt));

  constructor(
    private api: ApiService,
    public syService: SchoolYearService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private notify: NotificationService) {}

  ngOnInit(): void {
    const qp = this.route.snapshot.queryParamMap;
    this.selectedKey.set(qp.get('noSection') === 'true' ? NONE_KEY : (qp.get('sectionId') ?? ''));
    this.syService.ensureLoaded().subscribe({
      next: () => {
        this.schoolYear = qp.get('schoolYear') ?? this.syService.activeName() ?? '';
        this.load();
      },
      // Year list unavailable: the server still resolves the active year (or explains why not).
      error: () => { this.schoolYear = qp.get('schoolYear') ?? ''; this.load(); }
    });
  }

  key(s: WorklistSection): string { return s.sectionId ?? NONE_KEY; }
  percent(s: WorklistSection): number { return s.listed > 0 ? Math.round((s.encoded / s.listed) * 100) : 0; }

  onYearChange(): void {
    this.selectedKey.set('');
    this.syncUrl();
    this.load();
  }

  selectSection(s: WorklistSection): void {
    if (this.key(s) === this.selectedKey()) return;
    this.selectedKey.set(this.key(s));
    this.syncUrl();
    this.load();
  }

  load(showSpinner = true): void {
    const seq = ++this.requestSeq;
    if (showSpinner) this.loading.set(true);
    this.selectedIds.set(this.emptySet);
    const key = this.selectedKey();
    this.api.getLisWorklist(this.schoolYear || undefined, key && key !== NONE_KEY ? key : undefined, key === NONE_KEY).subscribe({
      next: r => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(false);
        this.report.set(r);
      },
      error: (err) => {
        if (seq !== this.requestSeq) return;
        this.loading.set(false);
        this.loadFailed.set(true);
        this.report.set(null);
        this.notify.error(formatApiError(err, 'Failed to load the LIS worklist.'));
      }
    });
  }

  toggle(id: string): void {
    const next = new Set(this.selectedIds());
    if (!next.delete(id)) next.add(id);
    this.selectedIds.set(next);
  }

  allSelected(learners: WorklistLearner[]): boolean {
    return learners.length > 0 && learners.every(l => this.selectedIds().has(l.enrollmentId));
  }

  toggleGroup(learners: WorklistLearner[], checked: boolean): void {
    const next = new Set(this.selectedIds());
    for (const l of learners) { if (checked) next.add(l.enrollmentId); else next.delete(l.enrollmentId); }
    this.selectedIds.set(next);
  }

  // Pending learners with nothing to fix. Learners under "Fix before encoding" are skipped by the bulk action.
  readyIn(learners: WorklistLearner[]): WorklistLearner[] {
    return learners.filter(l => !l.lisEncodedAt && l.fixReasons.length === 0);
  }

  markAllInGroup(learners: WorklistLearner[]): void {
    this.setEncoded(this.readyIn(learners).map(l => l.enrollmentId), true);
  }

  markSelected(encoded: boolean): void {
    this.setEncoded((encoded ? this.toEncode() : this.toUndo()).map(l => l.enrollmentId), encoded);
  }

  private setEncoded(enrollmentIds: string[], encoded: boolean): void {
    if (enrollmentIds.length === 0 || this.busy()) return;
    this.busy.set(true);
    this.api.setLisEncoded({ enrollmentIds, encoded }).subscribe({
      next: res => {
        this.busy.set(false);
        this.notify.success(encoded
          ? `Marked ${res.changed} learner${res.changed === 1 ? '' : 's'} as encoded.`
          : `Undid encoding for ${res.changed} learner${res.changed === 1 ? '' : 's'}.`);
        this.load(false);
      },
      error: (err) => {
        this.busy.set(false);
        this.notify.error(formatApiError(err, 'Failed to update LIS encoding.'));
      }
    });
  }

  private syncUrl(): void {
    const q: Record<string, string> = {};
    if (this.schoolYear) q['schoolYear'] = this.schoolYear;
    const key = this.selectedKey();
    if (key === NONE_KEY) q['noSection'] = 'true';
    else if (key) q['sectionId'] = key;
    this.router.navigate([], { relativeTo: this.route, queryParams: q, replaceUrl: true });
  }
}
