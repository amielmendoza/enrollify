import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LearnerProfile } from '../models';
import { DISABILITY_TYPES } from '../constants';
import { FieldConfigView } from '../learner';

/**
 * DepEd background fields (BEEF): PSA no., place of birth, mother tongue, religion, and the
 * IP / 4Ps / disability yes-no questions with their follow-ups. Mutates `profile` in place.
 * Visibility/labels come from `config` (school form settings on /apply, everything on staff forms).
 */
@Component({
  selector: 'app-learner-background-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      @if (config.visible('psaBirthCertificateNumber')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-psa'">{{ config.label('psaBirthCertificateNumber', 'PSA Birth Certificate No.') }} <span *ngIf="config.required('psaBirthCertificateNumber')">*</span></label>
          <input type="text" maxlength="30" class="form-input folio-mono" [id]="namePrefix + '-psa'"
                 [ngModel]="profile.psaBirthCertificateNumber" (ngModelChange)="profile.psaBirthCertificateNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('placeOfBirth')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-pob'">{{ config.label('placeOfBirth', 'Place of Birth (City/Municipality)') }} <span *ngIf="config.required('placeOfBirth')">*</span></label>
          <input type="text" maxlength="200" class="form-input" [id]="namePrefix + '-pob'"
                 [ngModel]="profile.placeOfBirth" (ngModelChange)="profile.placeOfBirth = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('motherTongue')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-mt'">{{ config.label('motherTongue', 'Mother Tongue') }} <span *ngIf="config.required('motherTongue')">*</span></label>
          <input type="text" maxlength="100" class="form-input" [id]="namePrefix + '-mt'"
                 [ngModel]="profile.motherTongue" (ngModelChange)="profile.motherTongue = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
      @if (config.visible('religion')) {
        <div>
          <label class="form-label" [attr.for]="namePrefix + '-rel'">{{ config.label('religion', 'Religion') }} <span *ngIf="config.required('religion')">*</span></label>
          <input type="text" maxlength="100" class="form-input" [id]="namePrefix + '-rel'"
                 [ngModel]="profile.religion" (ngModelChange)="profile.religion = $event || null" [ngModelOptions]="{ standalone: true }" />
        </div>
      }
    </div>

    <div class="mt-4 space-y-3">
      @if (config.visible('indigenousPeople')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.isIndigenousPeople" (ngModelChange)="profile.isIndigenousPeople = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('indigenousPeople', 'Belongs to an Indigenous Peoples (IP) community') }}
          </label>
          @if (profile.isIndigenousPeople) {
            <input type="text" maxlength="100" class="form-input mt-2" placeholder="Which community?"
                   [ngModel]="profile.ipCommunity" (ngModelChange)="profile.ipCommunity = $event || null" [ngModelOptions]="{ standalone: true }" />
          }
        </div>
      }
      @if (config.visible('fourPs')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.is4PsBeneficiary" (ngModelChange)="profile.is4PsBeneficiary = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('fourPs', '4Ps beneficiary household') }}
          </label>
          @if (profile.is4PsBeneficiary) {
            <input type="text" maxlength="30" class="form-input folio-mono mt-2" placeholder="4Ps household ID"
                   [ngModel]="profile.fourPsHouseholdId" (ngModelChange)="profile.fourPsHouseholdId = $event || null" [ngModelOptions]="{ standalone: true }" />
          }
        </div>
      }
      @if (config.visible('disability')) {
        <div>
          <label class="inline-flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" [ngModel]="profile.hasDisability" (ngModelChange)="profile.hasDisability = $event" [ngModelOptions]="{ standalone: true }" />
            {{ config.label('disability', 'Learner with disability') }}
          </label>
          @if (profile.hasDisability) {
            <div class="mt-2 grid grid-cols-1 sm:grid-cols-2 gap-1.5 rounded-md border border-[#E2D9C2] p-3">
              @for (t of disabilityTypes; track t) {
                <label class="inline-flex items-center gap-2 text-sm text-gray-700">
                  <input type="checkbox" [checked]="hasType(t)" (change)="toggleType(t, $any($event.target).checked)" />
                  {{ t }}
                </label>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class LearnerBackgroundFieldsComponent {
  @Input({ required: true }) profile!: LearnerProfile;
  @Input({ required: true }) config!: FieldConfigView;
  @Input() namePrefix = 'learner';

  readonly disabilityTypes = DISABILITY_TYPES;

  hasType(t: string): boolean {
    return (this.profile.disabilityTypes ?? []).includes(t);
  }

  toggleType(t: string, on: boolean): void {
    const current = new Set(this.profile.disabilityTypes ?? []);
    if (on) current.add(t); else current.delete(t);
    this.profile.disabilityTypes = DISABILITY_TYPES.filter(x => current.has(x));
  }
}
