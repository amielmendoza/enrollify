import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { SchoolYearService } from '../../../core/services/school-year.service';
import { Student } from '../../../core/models';
import { GRADE_LEVELS } from '../../../core/constants';

@Component({
  selector: 'app-enrollment-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-2xl">
      <h2 class="text-2xl font-bold text-gray-900 mb-6">New Enrollment</h2>

      @if (error()) {
        <div class="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg mb-4 text-sm">{{ error() }}</div>
      }

      <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
        <!-- Step indicator -->
        <div class="flex items-center mb-8">
          @for (s of stepLabels; track s; let i = $index) {
            <div class="flex items-center" [class.flex-1]="i < 2">
              <div class="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold"
                   [ngClass]="step() > i ? 'bg-[#0038A8] text-white' : step() === i ? 'bg-blue-50 text-[#0038A8] ring-2 ring-[#0038A8]' : 'bg-gray-200 text-gray-500'">
                {{ i + 1 }}
              </div>
              <span class="ml-2 text-sm font-medium" [ngClass]="step() >= i ? 'text-gray-900' : 'text-gray-400'">{{ s }}</span>
              @if (i < 2) { <div class="flex-1 h-px mx-4" [ngClass]="step() > i ? 'bg-[#0038A8]' : 'bg-gray-200'"></div> }
            </div>
          }
        </div>

        <!-- Step 0: Select Student -->
        @if (step() === 0) {
          <div>
            <label class="form-label">Search Student</label>
            <input type="text" [(ngModel)]="studentSearch" (ngModelChange)="searchStudents()"
                   (focus)="loadStudents()" placeholder="Type name or LRN..." class="form-input mb-3" />
            <div class="space-y-2 max-h-60 overflow-auto">
              @for (s of studentResults(); track s.id) {
                <button (click)="selectStudent(s)" class="w-full text-left p-3 border border-gray-200 rounded-lg hover:bg-blue-50 transition-colors"
                        [class.border-[#0038A8]]="selectedStudent()?.id === s.id" [class.bg-blue-50]="selectedStudent()?.id === s.id">
                  <span class="font-medium text-gray-900">{{ s.fullName }}</span>
                  <span class="text-gray-400 text-sm ml-2">LRN: <span class="folio-mono">{{ s.lrn }}</span></span>
                </button>
              }
            </div>
            <div class="mt-4 flex justify-end">
              <button (click)="step.set(1)" [disabled]="!selectedStudent()" class="btn btn-primary disabled:opacity-50">Next</button>
            </div>
          </div>
        }

        <!-- Step 1: Details -->
        @if (step() === 1) {
          <div class="space-y-4">
            <div>
              <label class="form-label">School Year *</label>
              <select [(ngModel)]="schoolYear" class="form-input">
                @for (sy of syService.schoolYears(); track sy.id) {
                  <option [value]="sy.name">{{ sy.name }}</option>
                }
              </select>
            </div>
            <div>
              <label class="form-label">Grade Level *</label>
              <select [(ngModel)]="gradeLevel" class="form-input">
                @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
              </select>
            </div>
            <div class="flex justify-between mt-4">
              <button (click)="step.set(0)" class="btn btn-secondary">Back</button>
              <button (click)="step.set(2)" [disabled]="!schoolYear || !gradeLevel" class="btn btn-primary disabled:opacity-50">Next</button>
            </div>
          </div>
        }

        <!-- Step 2: Confirm -->
        @if (step() === 2) {
          <div class="space-y-3">
            <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
              <p class="text-sm text-gray-500">Student</p>
              <p class="font-medium text-gray-900">{{ selectedStudent()?.fullName }} (<span class="folio-mono text-sm">{{ selectedStudent()?.lrn }}</span>)</p>
            </div>
            <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
              <p class="text-sm text-gray-500">School Year</p>
              <p class="font-medium text-gray-900">{{ schoolYear }}</p>
            </div>
            <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
              <p class="text-sm text-gray-500">Grade Level</p>
              <p class="font-medium text-gray-900">{{ gradeLevel }}</p>
            </div>
            <div class="flex justify-between mt-4">
              <button (click)="step.set(1)" class="btn btn-secondary">Back</button>
              <button (click)="submit()" [disabled]="saving()" class="btn btn-primary disabled:opacity-50">
                {{ saving() ? 'Creating...' : 'Create Enrollment' }}
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class EnrollmentWizardComponent {
  step = signal(0);
  error = signal('');
  saving = signal(false);
  studentSearch = '';
  studentResults = signal<Student[]>([]);
  selectedStudent = signal<Student | null>(null);
  schoolYear = '';
  gradeLevel = 'Grade 7';
  gradeLevels = GRADE_LEVELS;
  stepLabels = ['Select Student', 'Details', 'Confirm'];

  constructor(private api: ApiService, private router: Router, public syService: SchoolYearService) {
    this.syService.ensureLoaded().subscribe(list => {
      const active = list.find(sy => sy.isActive);
      if (active && !this.schoolYear) this.schoolYear = active.name;
    });
  }

  loadStudents(): void {
    if (this.studentResults().length === 0) {
      this.api.getStudents('', 1, 50).subscribe(r => this.studentResults.set(r.items));
    }
  }

  searchStudents(): void {
    this.api.getStudents(this.studentSearch, 1, 50).subscribe(r => this.studentResults.set(r.items));
  }

  selectStudent(s: Student): void { this.selectedStudent.set(s); }

  submit(): void {
    if (!this.selectedStudent()) return;
    this.saving.set(true);
    this.error.set('');
    this.api.createEnrollment({
      studentId: this.selectedStudent()!.id,
      schoolYear: this.schoolYear,
      gradeLevel: this.gradeLevel
    }).subscribe({
      next: (e) => this.router.navigate(['/enrollments', e.id]),
      error: (err) => { this.saving.set(false); this.error.set(err.error?.error || 'Failed to create enrollment'); }
    });
  }
}
