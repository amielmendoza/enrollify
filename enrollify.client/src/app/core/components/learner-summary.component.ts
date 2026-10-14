import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Student } from '../models';

/** Read-only DepEd details for parent/student profile pages. Families can't edit these. */
@Component({
  selector: 'app-learner-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <dl class="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-3 text-sm">
      <div><dt class="text-xs text-gray-500">Student No.</dt><dd class="folio-mono text-gray-900">{{ student.studentNumber || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">LRN</dt><dd class="folio-mono text-gray-900">{{ student.lrn || 'No LRN yet' }}</dd></div>
      <div><dt class="text-xs text-gray-500">PSA Birth Certificate No.</dt><dd class="folio-mono text-gray-900">{{ p?.psaBirthCertificateNumber || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Place of Birth</dt><dd class="text-gray-900">{{ p?.placeOfBirth || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Mother Tongue</dt><dd class="text-gray-900">{{ p?.motherTongue || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Religion</dt><dd class="text-gray-900">{{ p?.religion || '—' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Indigenous Peoples community</dt><dd class="text-gray-900">{{ p?.isIndigenousPeople ? (p?.ipCommunity || 'Yes') : 'No' }}</dd></div>
      <div><dt class="text-xs text-gray-500">4Ps beneficiary</dt><dd class="text-gray-900">{{ p?.is4PsBeneficiary ? 'Yes' : 'No' }}</dd></div>
      <div class="md:col-span-2"><dt class="text-xs text-gray-500">Learner with disability</dt><dd class="text-gray-900">{{ p?.hasDisability ? (p?.disabilityTypes?.join(', ') || 'Yes') : 'No' }}</dd></div>
      <div><dt class="text-xs text-gray-500">Father</dt><dd class="text-gray-900">{{ fullName(p?.fatherLastName, p?.fatherFirstName, p?.fatherMiddleName) }}</dd></div>
      <div><dt class="text-xs text-gray-500">Mother (maiden name)</dt><dd class="text-gray-900">{{ fullName(p?.motherMaidenLastName, p?.motherMaidenFirstName, p?.motherMaidenMiddleName) }}</dd></div>
    </dl>
    <p class="mt-4 text-xs text-gray-500">Contact the registrar to correct these details.</p>
  `
})
export class LearnerSummaryComponent {
  @Input({ required: true }) student!: Student;

  get p() { return this.student.profile; }

  fullName(last?: string | null, first?: string | null, middle?: string | null): string {
    if (!last && !first) return '—';
    return `${last ?? ''}, ${first ?? ''} ${middle ?? ''}`.trim();
  }
}
