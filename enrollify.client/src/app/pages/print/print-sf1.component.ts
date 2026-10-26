import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { PublicTenant, Sf1 } from '../../core/models';
import { formatApiError } from '../../core/utils/format-api-error';

// Printable DepEd SF1 working copy. Top-level (outside MainLayout) so no sidebar/nav prints.
// URL: /print/sf1/:sectionId?schoolYear. Landscape long bond (13in x 8.5in) — the one @page rule in the app.
@Component({
  selector: 'app-print-sf1',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    .document { font-family: 'Public Sans', ui-sans-serif, system-ui, sans-serif; }
    .doc-title { font-family: 'Archivo', ui-sans-serif, system-ui, sans-serif; }
    .sf1-table { font-size: 10px; }
    .sf1-table th, .sf1-table td { border: 1px solid #9ca3af; padding: 2px 4px; vertical-align: top; }
    .sf1-table thead th { background: #f3f4f6; text-align: left; font-weight: 600; }
    @page { size: 13in 8.5in; margin: 0.4in; }
    @media print {
      .no-print { display: none !important; }
      .print-root { background: white !important; }
      .document {
        border: none !important;
        box-shadow: none !important;
        margin: 0 auto !important;
        max-width: 100% !important;
        padding: 0 !important;
      }
      .sf1-table thead { display: table-header-group; }
      .sf1-table tr { break-inside: avoid; }
      .signatures { break-inside: avoid; }
    }
  `],
  template: `
    <div class="min-h-screen bg-gray-100 print-root">
      <div class="no-print sticky top-0 z-10 bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
        <p class="text-sm text-gray-600">Print preview — <span class="font-medium text-gray-900">SF1 Register</span> (landscape, long bond)</p>
        <div class="flex gap-2">
          <button (click)="print()" class="btn btn-primary">Print</button>
          <button (click)="close()" class="btn btn-secondary">Close</button>
        </div>
      </div>

      @if (error()) {
        <div class="no-print max-w-[800px] mx-auto mt-8 rounded-lg bg-red-50 border border-red-200 p-4 text-sm text-red-700">{{ error() }}</div>
      }

      @if (sf1(); as r) {
        <div class="document mx-auto my-8 max-w-[1248px] bg-white border border-[#E2D9C2] shadow-sm p-8">
          <div class="text-center border-b-2 border-gray-900 pb-3">
            <p class="doc-title text-base font-semibold uppercase tracking-[0.25em] text-gray-800">School Form 1 (SF1) — School Register</p>
            <p class="text-[11px] text-gray-500">Working copy for checking against LIS. The official SF1 is generated in LIS.</p>
            <h1 class="text-xl font-bold text-gray-900 mt-1">{{ r.header.school.schoolName || tenant()?.name || '' }}</h1>
          </div>

          <div class="mt-3 grid grid-cols-4 gap-x-6 gap-y-1 text-xs">
            <p><span class="text-gray-500">School ID:</span> <span class="folio-mono font-medium text-gray-900">{{ r.header.school.schoolId || '—' }}</span></p>
            <p><span class="text-gray-500">Region:</span> <span class="font-medium text-gray-900">{{ r.header.school.regionName || '—' }}</span></p>
            <p><span class="text-gray-500">Division:</span> <span class="font-medium text-gray-900">{{ r.header.school.division || '—' }}</span></p>
            <p><span class="text-gray-500">District:</span> <span class="font-medium text-gray-900">{{ r.header.school.district || '—' }}</span></p>
            <p><span class="text-gray-500">School Year:</span> <span class="folio-mono font-medium text-gray-900">{{ r.header.school.schoolYear }}</span></p>
            <p><span class="text-gray-500">Grade Level:</span> <span class="font-medium text-gray-900">{{ r.header.gradeLevel }}</span></p>
            <p><span class="text-gray-500">Section:</span> <span class="font-medium text-gray-900">{{ r.header.sectionName }}</span></p>
            <p><span class="text-gray-500">Adviser:</span> <span class="font-medium text-gray-900">{{ r.header.adviser || '—' }}</span></p>
          </div>

          <table class="sf1-table w-full mt-4 border-collapse">
            <thead>
              <tr>
                <th>#</th>
                @for (h of headings; track h) { <th>{{ h }}</th> }
              </tr>
            </thead>
            <tbody>
              @for (l of r.learners; track $index) {
                <tr>
                  <td class="folio-mono">{{ $index + 1 }}</td>
                  <td class="folio-mono whitespace-nowrap">{{ l.lrn || '' }}</td>
                  <td class="font-medium">{{ l.name }}</td>
                  <td>{{ l.sex }}</td>
                  <td class="whitespace-nowrap">{{ l.birthDate ? (l.birthDate | date:'MM/dd/yyyy') : '' }}</td>
                  <td>{{ l.age }}</td>
                  <td>{{ l.motherTongue }}</td>
                  <td>{{ l.ipGroup }}</td>
                  <td>{{ l.religion }}</td>
                  <td>{{ l.houseStreet }}</td>
                  <td>{{ l.barangay }}</td>
                  <td>{{ l.cityMunicipality }}</td>
                  <td>{{ l.province }}</td>
                  <td>{{ l.fatherName }}</td>
                  <td>{{ l.motherMaidenName }}</td>
                  <td>{{ l.guardianName }}</td>
                  <td>{{ l.guardianRelationship }}</td>
                  <td>{{ l.contact }}</td>
                  <td>{{ l.remarks }}</td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="headings.length + 1" class="text-center text-gray-400">No learners in this section.</td></tr>
              }
            </tbody>
          </table>

          <p class="mt-3 text-xs text-gray-700">
            Male: <span class="folio-mono font-semibold">{{ r.male }}</span> ·
            Female: <span class="folio-mono font-semibold">{{ r.female }}</span>
            @if (r.notSet > 0) { · Sex not set: <span class="folio-mono font-semibold">{{ r.notSet }}</span> }
            · Total: <span class="folio-mono font-semibold">{{ r.total }}</span>
          </p>

          <div class="signatures grid grid-cols-2 gap-16 mt-14 text-sm">
            <div class="text-center">
              <div class="border-t border-gray-900 pt-1 font-semibold text-gray-900">Prepared by: {{ r.header.adviser || '' }}</div>
              <p class="text-xs text-gray-500 mt-0.5">Adviser — signature over printed name</p>
            </div>
            <div class="text-center">
              <div class="border-t border-gray-900 pt-1 font-semibold text-gray-900">Certified correct: {{ r.header.schoolHeadName || '' }}</div>
              <p class="text-xs text-gray-500 mt-0.5">School Head — signature over printed name</p>
            </div>
          </div>

          <p class="mt-6 text-[10px] text-gray-400">Date printed: <span class="folio-mono">{{ today | date:'medium' }}</span></p>
        </div>
      }
    </div>
  `
})
export class PrintSf1Component implements OnInit {
  sf1 = signal<Sf1 | null>(null);
  tenant = signal<PublicTenant | null>(null);
  error = signal('');
  today = new Date();

  readonly headings = ['LRN', 'Name', 'Sex', 'Birth date', 'Age', 'Mother tongue', 'IP group', 'Religion',
    'House/Street', 'Barangay', 'City/Municipality', 'Province', "Father's name", "Mother's maiden name",
    'Guardian', 'Relationship', 'Contact', 'Remarks'];

  private tenantLoaded = signal(false);
  private printed = false;

  constructor(private api: ApiService, private auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const sectionId = this.route.snapshot.paramMap.get('sectionId') ?? '';
    const schoolYear = this.route.snapshot.queryParamMap.get('schoolYear') ?? undefined;

    this.api.getPublicTenant(this.auth.getTenantId()).subscribe({
      next: t => { this.tenant.set(t); this.tenantLoaded.set(true); this.maybeAutoPrint(); },
      error: () => { this.tenantLoaded.set(true); this.maybeAutoPrint(); }  // print without tenant name rather than never
    });

    this.api.getSf1(sectionId, schoolYear).subscribe({
      next: r => { this.sf1.set(r); this.maybeAutoPrint(); },
      error: (err) => this.error.set(formatApiError(err, 'Failed to load the SF1 register.'))
    });
  }

  print(): void { window.print(); }

  close(): void { window.close(); }

  private maybeAutoPrint(): void {
    if (this.printed) return;
    if (!this.sf1() || !this.tenantLoaded()) return;
    this.printed = true;
    setTimeout(() => window.print(), 400);
  }
}
