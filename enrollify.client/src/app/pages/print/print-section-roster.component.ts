import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { PublicTenant, SectionRoster } from '../../core/models';
import { formatApiError } from '../../core/utils/format-api-error';

// Printable class list for one section (advisers' roster). Top-level (outside MainLayout) like
// the other print pages. URL: /print/section-roster/:id. Cancelled enrollments never appear.
@Component({
  selector: 'app-print-section-roster',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    .document { font-family: 'Public Sans', ui-sans-serif, system-ui, sans-serif; }
    .doc-title { font-family: 'Archivo', ui-sans-serif, system-ui, sans-serif; }
    @media print {
      .no-print { display: none !important; }
      .print-root { background: white !important; }
      .document {
        border: none !important;
        box-shadow: none !important;
        margin: 0 auto !important;
        max-width: 100% !important;
      }
    }
  `],
  template: `
    <div class="min-h-screen bg-gray-100 print-root">
      <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">Class List</span></p>
        <div class="flex gap-2">
          <button (click)="print()" class="btn btn-primary">Print</button>
          <button (click)="close()" class="btn btn-secondary">Close</button>
        </div>
      </div>

      @if (error()) {
        <div class="no-print max-w-[800px] mx-auto mt-8 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (roster(); as r) {
        <div class="document mx-auto my-8 max-w-[800px] bg-white border border-[#E2D9C2] shadow-sm p-12">
          <div class="text-center border-b-2 border-gray-900 pb-5">
            <h1 class="text-2xl font-bold text-gray-900">{{ tenant()?.name || '' }}</h1>
            <p class="doc-title mt-2 text-base font-semibold uppercase tracking-[0.25em] text-gray-800">Class List</p>
            <p class="mt-1 text-sm text-gray-500">
              {{ r.section.gradeLevel }} — {{ r.section.name }} · School Year <span class="folio-mono">{{ r.section.schoolYear }}</span>
            </p>
          </div>

          <div class="mt-6 grid grid-cols-3 gap-4 text-sm">
            <div><p class="text-xs text-gray-500">Adviser</p><p class="font-medium text-gray-900">{{ r.section.adviser || '—' }}</p></div>
            <div><p class="text-xs text-gray-500">Learners</p><p class="font-medium text-gray-900 folio-mono">{{ r.section.currentCount }} / {{ r.section.capacity }}</p></div>
            <div><p class="text-xs text-gray-500">Male / Female</p><p class="font-medium text-gray-900 folio-mono">{{ maleCount() }} / {{ femaleCount() }}</p></div>
          </div>

          <table class="w-full mt-6 text-sm">
            <thead>
              <tr class="border-b-2 border-gray-900 text-left">
                <th class="py-2 pr-2 font-semibold text-gray-900 w-10">No.</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Student No.</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">LRN</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Name</th>
                <th class="py-2 pr-2 font-semibold text-gray-900">Sex</th>
                <th class="py-2 font-semibold text-gray-900">Status</th>
              </tr>
            </thead>
            <tbody>
              @for (s of r.students; track s.enrollmentId; let i = $index) {
                <tr class="border-b border-gray-100">
                  <td class="py-1.5 pr-2 text-gray-500 folio-mono">{{ i + 1 }}</td>
                  <td class="py-1.5 pr-2 text-gray-700 folio-mono whitespace-nowrap">{{ s.studentNumber }}</td>
                  <td class="py-1.5 pr-2 text-gray-700 folio-mono whitespace-nowrap">{{ s.lrn || '—' }}</td>
                  <td class="py-1.5 pr-2 text-gray-900">{{ s.name }}</td>
                  <td class="py-1.5 pr-2 text-gray-700">{{ s.gender || '—' }}</td>
                  <td class="py-1.5 text-gray-700">{{ s.status }}</td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="py-4 text-center text-gray-400">No learners assigned to this section yet.</td></tr>
              }
            </tbody>
          </table>

          <div class="grid grid-cols-2 mt-20">
            <div></div>
            <div class="text-center">
              <div class="border-t border-gray-900 pt-2 text-sm font-semibold text-gray-900">{{ r.section.adviser || 'Class Adviser' }}</div>
              <p class="text-xs text-gray-500 mt-0.5">Class Adviser</p>
            </div>
          </div>

          <p class="mt-10 text-xs text-gray-400">Date printed: <span class="folio-mono">{{ today | date:'medium' }}</span></p>
        </div>
      }
    </div>
  `
})
export class PrintSectionRosterComponent implements OnInit {
  roster = signal<SectionRoster | null>(null);
  tenant = signal<PublicTenant | null>(null);
  error = signal('');
  today = new Date();

  maleCount = computed(() => (this.roster()?.students ?? []).filter(s => s.gender === 'Male').length);
  femaleCount = computed(() => (this.roster()?.students ?? []).filter(s => s.gender === 'Female').length);

  private tenantLoaded = signal(false);
  private printed = false;

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without header name rather than never
    });

    this.api.getSectionRoster(id).subscribe({
      next: r => { this.roster.set(r); this.maybeAutoPrint(); },
      error: (err) => this.error.set(formatApiError(err, 'Failed to load the class list.'))
    });
  }

  print(): void { window.print(); }

  close(): void { window.close(); }

  private maybeAutoPrint(): void {
    if (this.printed) return;
    if (!this.roster() || !this.tenantLoaded()) return;
    this.printed = true;
    setTimeout(() => window.print(), 400);
  }
}
