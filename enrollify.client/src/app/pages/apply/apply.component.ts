import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { SubmitApplicationRequest } from '../../core/models';

@Component({
  selector: 'app-apply',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex min-h-screen">
      <!-- LEFT: Branding -->
      <div class="hidden lg:flex lg:w-5/12 flex-col justify-between bg-[#4361ee] p-10">
        <div class="flex items-center gap-3">
          <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-white/20">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
            </svg>
          </div>
          <span class="text-lg font-bold text-white">Enrollify</span>
        </div>
        <div class="mb-8">
          <h1 class="text-4xl font-bold leading-tight text-white">Start your<br />enrollment journey.</h1>
          <p class="mt-4 text-base leading-relaxed text-white/70">Submit your application online. Once approved, you'll receive your login credentials to complete the enrollment process.</p>
        </div>
        <p class="text-sm text-white/40">&copy; 2026 Enrollify. All rights reserved.</p>
      </div>

      <!-- RIGHT: Application Form -->
      <div class="flex-1 overflow-y-auto bg-[#f8f9fc] px-6 py-10">
        <div class="mx-auto max-w-2xl">
          <!-- Mobile logo -->
          <div class="mb-6 flex items-center gap-3 lg:hidden">
            <div class="flex h-9 w-9 items-center justify-center rounded-lg bg-[#4361ee]">
              <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342M6.75 15a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm0 0v-3.675A55.378 55.378 0 0 1 12 8.443m-7.007 11.55A5.981 5.981 0 0 0 6.75 15.75v-1.5" />
              </svg>
            </div>
            <span class="text-lg font-bold text-gray-900">Enrollify</span>
          </div>

          @if (submitted()) {
            <div class="bg-white rounded-xl border border-gray-200 p-8 text-center">
              <div class="mx-auto w-16 h-16 bg-emerald-50 rounded-full flex items-center justify-center mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <h2 class="text-2xl font-bold text-gray-900">Application Submitted!</h2>
              <p class="mt-2 text-sm text-gray-500">Your application number is <span class="font-mono font-semibold text-[#4361ee]">{{ appNumber() }}</span></p>
              <p class="mt-4 text-sm text-gray-500">Once your application is approved, you will receive your login credentials via email. The default password is <span class="font-mono font-medium">ChangeMe123!</span></p>
              <a routerLink="/login" class="inline-block mt-6 btn btn-primary px-8">Go to Login</a>
            </div>
          } @else {
            <h2 class="text-2xl font-bold text-gray-900">Application Form</h2>
            <p class="mt-1 text-sm text-gray-500">Fill in the details below to apply for enrollment</p>

            @if (error()) {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ error() }}</div>
            }

            <form (ngSubmit)="onSubmit()" class="mt-6 space-y-6">
              <!-- Student Info -->
              <div class="bg-white rounded-xl border border-gray-200 p-6">
                <h3 class="text-base font-semibold text-gray-900 mb-4">Student Information</h3>
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <label class="form-label">First Name *</label>
                    <input type="text" [(ngModel)]="form.firstName" name="firstName" required class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Middle Name</label>
                    <input type="text" [(ngModel)]="form.middleName" name="middleName" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Last Name *</label>
                    <input type="text" [(ngModel)]="form.lastName" name="lastName" required class="form-input" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
                  <div>
                    <label class="form-label">Date of Birth *</label>
                    <input type="date" [(ngModel)]="form.dateOfBirth" name="dateOfBirth" required class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Gender *</label>
                    <select [(ngModel)]="form.gender" name="gender" required class="form-input">
                      <option value="">Select</option>
                      <option value="Male">Male</option>
                      <option value="Female">Female</option>
                    </select>
                  </div>
                  <div>
                    <label class="form-label">Email *</label>
                    <input type="email" [(ngModel)]="form.email" name="email" required class="form-input" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                  <div>
                    <label class="form-label">Contact Number</label>
                    <input type="text" [(ngModel)]="form.contactNumber" name="contactNumber" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Address</label>
                    <input type="text" [(ngModel)]="form.address" name="address" class="form-input" />
                  </div>
                </div>
              </div>

              <!-- Enrollment Info -->
              <div class="bg-white rounded-xl border border-gray-200 p-6">
                <h3 class="text-base font-semibold text-gray-900 mb-4">Enrollment Details</h3>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label">Grade Level *</label>
                    <select [(ngModel)]="form.gradeLevel" name="gradeLevel" required class="form-input">
                      <option value="">Select</option>
                      @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                    </select>
                  </div>
                  <div>
                    <label class="form-label">School Year *</label>
                    <select [(ngModel)]="form.schoolYear" name="schoolYear" required class="form-input">
                      <option value="2024-2025">2024-2025</option>
                      <option value="2025-2026">2025-2026</option>
                    </select>
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                  <div>
                    <label class="form-label">Previous School</label>
                    <input type="text" [(ngModel)]="form.previousSchool" name="previousSchool" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Previous School Address</label>
                    <input type="text" [(ngModel)]="form.previousSchoolAddress" name="previousSchoolAddress" class="form-input" />
                  </div>
                </div>
              </div>

              <!-- Guardian Info -->
              <div class="bg-white rounded-xl border border-gray-200 p-6">
                <h3 class="text-base font-semibold text-gray-900 mb-4">Guardian Information</h3>
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <label class="form-label">Guardian Name</label>
                    <input type="text" [(ngModel)]="form.guardianName" name="guardianName" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Guardian Contact</label>
                    <input type="text" [(ngModel)]="form.guardianContact" name="guardianContact" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Relationship</label>
                    <select [(ngModel)]="form.guardianRelationship" name="guardianRelationship" class="form-input">
                      <option [ngValue]="null">Select</option>
                      <option value="Mother">Mother</option>
                      <option value="Father">Father</option>
                      <option value="Guardian">Guardian</option>
                      <option value="Other">Other</option>
                    </select>
                  </div>
                </div>
              </div>

              <div class="flex items-center justify-between">
                <a routerLink="/login" class="text-sm text-gray-500 hover:text-gray-700">Already have an account? Sign in</a>
                <button type="submit" [disabled]="saving()"
                        class="flex items-center gap-2 rounded-xl bg-[#4361ee] px-8 py-3 text-sm font-semibold text-white hover:bg-[#3a56d4] transition-colors disabled:opacity-60">
                  @if (saving()) {
                    <svg class="h-5 w-5 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                    Submitting...
                  } @else {
                    Submit Application
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3" /></svg>
                  }
                </button>
              </div>
            </form>
          }
        </div>
      </div>
    </div>
  `
})
export class ApplyComponent {
  saving = signal(false);
  error = signal('');
  submitted = signal(false);
  appNumber = signal('');

  gradeLevels = ['Kindergarten','Grade 1','Grade 2','Grade 3','Grade 4','Grade 5','Grade 6','Grade 7','Grade 8','Grade 9','Grade 10','Grade 11','Grade 12'];

  form: SubmitApplicationRequest = {
    firstName: '', middleName: null, lastName: '', email: '',
    contactNumber: null, gender: '', dateOfBirth: '', address: null,
    gradeLevel: '', schoolYear: '2024-2025',
    previousSchool: null, previousSchoolAddress: null,
    guardianName: null, guardianContact: null, guardianRelationship: null
  };

  constructor(private api: ApiService, private router: Router) {}

  onSubmit() {
    this.saving.set(true);
    this.error.set('');
    this.api.submitApplication(this.form).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.submitted.set(true);
        this.appNumber.set(res.applicationNumber);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err.error?.error || err.error?.details?.[0]?.errorMessage || 'Submission failed. Please check your inputs.');
      }
    });
  }
}
