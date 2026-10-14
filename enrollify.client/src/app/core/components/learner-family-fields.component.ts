import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LearnerProfile } from '../models';
import { FieldConfigView } from '../learner';

/** Father's name and mother's maiden name (last / first / middle + contact). Mutates `profile`. */
@Component({
  selector: 'app-learner-family-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (config.visible('fatherName')) {
      <p class="text-sm font-medium text-gray-700 mb-2">{{ config.label('fatherName', "Father's Name") }} <span *ngIf="config.required('fatherName')">*</span></p>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3 mb-4">
        <input type="text" maxlength="100" class="form-input" placeholder="Last name" [attr.aria-label]="'Father last name'"
               [ngModel]="profile.fatherLastName" (ngModelChange)="profile.fatherLastName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="First name" [attr.aria-label]="'Father first name'"
               [ngModel]="profile.fatherFirstName" (ngModelChange)="profile.fatherFirstName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="Middle name" [attr.aria-label]="'Father middle name'"
               [ngModel]="profile.fatherMiddleName" (ngModelChange)="profile.fatherMiddleName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="50" class="form-input" placeholder="Contact number" [attr.aria-label]="'Father contact number'"
               [ngModel]="profile.fatherContactNumber" (ngModelChange)="profile.fatherContactNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
      </div>
    }
    @if (config.visible('motherMaidenName')) {
      <p class="text-sm font-medium text-gray-700 mb-2">{{ config.label('motherMaidenName', "Mother's Maiden Name") }} <span *ngIf="config.required('motherMaidenName')">*</span></p>
      <div class="grid grid-cols-1 md:grid-cols-4 gap-3">
        <input type="text" maxlength="100" class="form-input" placeholder="Last name" [attr.aria-label]="'Mother maiden last name'"
               [ngModel]="profile.motherMaidenLastName" (ngModelChange)="profile.motherMaidenLastName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="First name" [attr.aria-label]="'Mother first name'"
               [ngModel]="profile.motherMaidenFirstName" (ngModelChange)="profile.motherMaidenFirstName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="100" class="form-input" placeholder="Middle name" [attr.aria-label]="'Mother maiden middle name'"
               [ngModel]="profile.motherMaidenMiddleName" (ngModelChange)="profile.motherMaidenMiddleName = $event || null" [ngModelOptions]="{ standalone: true }" />
        <input type="text" maxlength="50" class="form-input" placeholder="Contact number" [attr.aria-label]="'Mother contact number'"
               [ngModel]="profile.motherContactNumber" (ngModelChange)="profile.motherContactNumber = $event || null" [ngModelOptions]="{ standalone: true }" />
      </div>
    }
  `
})
export class LearnerFamilyFieldsComponent {
  @Input({ required: true }) profile!: LearnerProfile;
  @Input({ required: true }) config!: FieldConfigView;
  @Input() namePrefix = 'family';
}
