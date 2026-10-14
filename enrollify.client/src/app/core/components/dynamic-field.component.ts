import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApplicationFormField } from '../models';

/** One admin-defined (custom) application-form field. Values are strings; checkboxes use 'true'/'false'. */
@Component({
  selector: 'app-dynamic-field',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <label class="form-label" [attr.for]="name">{{ field.label }} <span *ngIf="field.isRequired">*</span></label>
    @switch (field.fieldType) {
      @case ('TextArea') {
        <textarea [id]="name" [name]="name" rows="3" class="form-input" [required]="field.isRequired"
                  [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }"></textarea>
      }
      @case ('Number') {
        <input type="number" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
      @case ('Date') {
        <input type="date" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
      @case ('Checkbox') {
        <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700">
          <input type="checkbox" [id]="name" [name]="name"
                 [ngModel]="value === 'true'" (ngModelChange)="emit($event ? 'true' : 'false')" [ngModelOptions]="{ standalone: true }" />
          Yes
        </label>
      }
      @case ('Dropdown') {
        <select [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
                [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }">
          <option value="">Select</option>
          @for (opt of options(); track opt) { <option [value]="opt">{{ opt }}</option> }
        </select>
      }
      @default {
        <input type="text" [id]="name" [name]="name" class="form-input" [required]="field.isRequired"
               [ngModel]="value" (ngModelChange)="emit($event)" [ngModelOptions]="{ standalone: true }" />
      }
    }
    @if (field.helpText) { <p class="mt-1 text-xs text-gray-400">{{ field.helpText }}</p> }
  `
})
export class DynamicFieldComponent {
  @Input({ required: true }) field!: ApplicationFormField;
  @Input({ required: true }) name!: string;
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();

  options(): string[] {
    if (!this.field.options) return [];
    try {
      const parsed = JSON.parse(this.field.options);
      return Array.isArray(parsed) ? parsed.map(String) : [];
    } catch { return []; }
  }

  emit(v: unknown): void {
    this.valueChange.emit(v === null || v === undefined ? '' : String(v));
  }
}
