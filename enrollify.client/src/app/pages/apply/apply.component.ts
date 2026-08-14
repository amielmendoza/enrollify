import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SchoolYearService } from '../../core/services/school-year.service';
import { ApplicantData, ApplicationFormField, FormFieldSection, PublicTenant, SubmitApplicationRequest } from '../../core/models';
import { GRADE_LEVELS } from '../../core/constants';

type ApplyMode = 'Parent' | 'Student';

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
          <p class="mt-4 text-base leading-relaxed text-white/70">Submit an application online. Once approved, login credentials will be sent so you can complete the enrollment process.</p>
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

          @if (tenant()) {
            <p class="mb-4 text-xs uppercase tracking-wider text-gray-400">Applying to</p>
            <p class="-mt-3 mb-6 text-sm font-semibold text-[#4361ee]">{{ tenant()!.name }}</p>
          }
          @if (submitted()) {
            <div class="bg-white rounded-xl border border-gray-200 p-8 text-center">
              <div class="mx-auto w-16 h-16 bg-emerald-50 rounded-full flex items-center justify-center mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <h2 class="text-2xl font-bold text-gray-900">{{ submittedAppNumbers().length === 1 ? 'Application Submitted!' : 'Applications Submitted!' }}</h2>
              <p class="mt-2 text-sm text-gray-500">
                @if (submittedAppNumbers().length === 1) {
                  Your application number is <span class="font-mono font-semibold text-[#4361ee]">{{ submittedAppNumbers()[0] }}</span>
                } @else {
                  {{ submittedAppNumbers().length }} applications were submitted:
                }
              </p>
              @if (submittedAppNumbers().length > 1) {
                <ul class="mt-3 text-sm text-gray-700 inline-block text-left">
                  @for (n of submittedAppNumbers(); track n) {
                    <li class="font-mono text-[#4361ee]">{{ n }}</li>
                  }
                </ul>
              }
              @if (isParentLoggedIn()) {
                <p class="mt-4 text-sm text-gray-500">Once approved, the {{ submittedAppNumbers().length === 1 ? 'child' : 'children' }} will appear in your dashboard.</p>
                <a routerLink="/parent/dashboard" class="inline-block mt-6 btn btn-primary px-8">Back to Dashboard</a>
              } @else {
                <p class="mt-4 text-sm text-gray-500">Save your application {{ submittedAppNumbers().length === 1 ? 'number' : 'numbers' }} — you'll need {{ submittedAppNumbers().length === 1 ? 'it' : 'them' }} to check your status. Once approved, an account is created with the email you provided and the temporary password <span class="font-mono font-medium">ChangeMe123!</span> (change it after your first login).</p>
                <div class="mt-6 flex items-center justify-center gap-3">
                  @if (statusSlug()) {
                    <a [routerLink]="['/', statusSlug(), 'status']" [queryParams]="{ ref: submittedAppNumbers()[0] }" class="inline-block btn btn-primary px-8">Check Status</a>
                  }
                  <a routerLink="/login" class="inline-block btn btn-secondary px-8">Go to Login</a>
                </div>
              }
            </div>
          } @else {
            @if (isParentLoggedIn()) {
              <h2 class="text-2xl font-bold text-gray-900">Enroll {{ applicants().length === 1 ? 'Another Child' : 'Children' }}</h2>
              <p class="mt-1 text-sm text-gray-500">Add one or more children to enroll under your existing parent account.</p>
            } @else {
              <h2 class="text-2xl font-bold text-gray-900">Application Form</h2>
              <p class="mt-1 text-sm text-gray-500">Fill in the details below to apply for enrollment</p>
            }

            @if (error()) {
              <div class="mt-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700 whitespace-pre-line">{{ error() }}</div>
            }

            <form (ngSubmit)="onSubmit()" class="mt-6 space-y-6">
              <!-- Mode toggle (anonymous applicants only) -->
              @if (!isParentLoggedIn()) {
                <div class="bg-white rounded-xl border border-gray-200 p-6">
                  <h3 class="text-base font-semibold text-gray-900 mb-1">Who is applying?</h3>
                  <p class="text-xs text-gray-500 mb-4">Pick the option that matches you. This decides what kind of account is created on approval.</p>
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <button type="button" (click)="setMode('Parent')"
                            class="relative border-2 rounded-xl p-4 text-left transition-all"
                            [class]="mode() === 'Parent' ? 'border-[#4361ee] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                      @if (mode() === 'Parent') {
                        <div class="absolute top-3 right-3 w-5 h-5 rounded-full flex items-center justify-center text-white bg-[#4361ee]">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                        </div>
                      }
                      <p class="text-sm font-semibold text-gray-900">I am a parent</p>
                      <p class="text-xs text-gray-500 mt-1">I'm enrolling one or more children. I'll be able to manage all of them from one parent account.</p>
                    </button>
                    <button type="button" (click)="setMode('Student')"
                            class="relative border-2 rounded-xl p-4 text-left transition-all"
                            [class]="mode() === 'Student' ? 'border-[#4361ee] bg-blue-50/50' : 'border-gray-200 hover:border-gray-300'">
                      @if (mode() === 'Student') {
                        <div class="absolute top-3 right-3 w-5 h-5 rounded-full flex items-center justify-center text-white bg-[#4361ee]">
                          <svg xmlns="http://www.w3.org/2000/svg" class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"><path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" /></svg>
                        </div>
                      }
                      <p class="text-sm font-semibold text-gray-900">I am a student</p>
                      <p class="text-xs text-gray-500 mt-1">I'm applying for myself. I'll get a student account once my application is approved.</p>
                    </button>
                  </div>
                </div>
              }

              <!-- Parent Account section (Parent mode only, anonymous only) -->
              @if (mode() === 'Parent' && !isParentLoggedIn()) {
                <div class="bg-white rounded-xl border border-gray-200 p-6">
                  <h3 class="text-base font-semibold text-gray-900 mb-1">Parent Information</h3>
                  <p class="text-xs text-gray-500 mb-4">A single parent account will be created on approval and will be linked to all children below. The parent will also be listed as the guardian for each child. Default password: <span class="font-mono">ChangeMe123!</span></p>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label class="form-label">{{ fieldLabel('parentFirstName', 'Parent First Name') }} *</label>
                      <input type="text" [(ngModel)]="parentFirstName" name="parentFirstName" required class="form-input" />
                    </div>
                    <div>
                      <label class="form-label">{{ fieldLabel('parentLastName', 'Parent Last Name') }} *</label>
                      <input type="text" [(ngModel)]="parentLastName" name="parentLastName" required class="form-input" />
                    </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                    <div>
                      <label class="form-label">{{ fieldLabel('parentEmail', 'Parent Email') }} *</label>
                      <input type="email" [(ngModel)]="parentEmail" name="parentEmail" required class="form-input" />
                      <p class="mt-1 text-xs text-gray-400">You'll use this email to sign in once approved.</p>
                    </div>
                    @if (isFieldVisible('parentContactNumber')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('parentContactNumber', 'Parent Contact') }} <span *ngIf="isFieldRequired('parentContactNumber')">*</span></label>
                        <input type="text" [(ngModel)]="parentContactNumber" name="parentContactNumber" [required]="isFieldRequired('parentContactNumber')" class="form-input" />
                      </div>
                    }
                  </div>
                  @if (isFieldVisible('parentRelationship')) {
                    <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                      <div>
                        <label class="form-label">{{ fieldLabel('parentRelationship', 'Relationship to children') }} <span *ngIf="isFieldRequired('parentRelationship')">*</span></label>
                        <select [(ngModel)]="parentRelationship" name="parentRelationship" [required]="isFieldRequired('parentRelationship')" class="form-input">
                          <option value="">Select</option>
                          @for (opt of fieldOptions('parentRelationship'); track opt) { <option [value]="opt">{{ opt }}</option> }
                        </select>
                      </div>
                    </div>
                  }
                  <!-- Parent-section custom fields (admin-defined) -->
                  @for (cf of customFieldsFor('Parent'); track cf.id) {
                    <div class="mt-4">
                      <label class="form-label">{{ cf.label }} <span *ngIf="cf.isRequired">*</span></label>
                      @switch (cf.fieldType) {
                        @case ('TextArea') {
                          <textarea [ngModel]="parentCustomValue(cf.fieldKey)" (ngModelChange)="setParentCustomValue(cf.fieldKey, $event)"
                                    [name]="'parent_' + cf.fieldKey" [required]="cf.isRequired" rows="3" class="form-input"></textarea>
                        }
                        @case ('Number') {
                          <input type="number" [ngModel]="parentCustomValue(cf.fieldKey)" (ngModelChange)="setParentCustomValue(cf.fieldKey, $event)"
                                 [name]="'parent_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" />
                        }
                        @case ('Date') {
                          <input type="date" [ngModel]="parentCustomValue(cf.fieldKey)" (ngModelChange)="setParentCustomValue(cf.fieldKey, $event)"
                                 [name]="'parent_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" />
                        }
                        @case ('Checkbox') {
                          <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700">
                            <input type="checkbox" [ngModel]="parentCustomCheckboxValue(cf.fieldKey)" (ngModelChange)="setParentCustomCheckboxValue(cf.fieldKey, $event)" [name]="'parent_' + cf.fieldKey" />
                            Yes
                          </label>
                        }
                        @case ('Dropdown') {
                          <select [ngModel]="parentCustomValue(cf.fieldKey)" (ngModelChange)="setParentCustomValue(cf.fieldKey, $event)"
                                  [name]="'parent_' + cf.fieldKey" [required]="cf.isRequired" class="form-input">
                            <option value="">Select</option>
                            @for (opt of customDropdownOptions(cf); track opt) { <option [value]="opt">{{ opt }}</option> }
                          </select>
                        }
                        @default {
                          <input type="text" [ngModel]="parentCustomValue(cf.fieldKey)" (ngModelChange)="setParentCustomValue(cf.fieldKey, $event)"
                                 [name]="'parent_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" />
                        }
                      }
                      @if (cf.helpText) { <p class="mt-1 text-xs text-gray-400">{{ cf.helpText }}</p> }
                    </div>
                  }
                </div>
              }

              <!-- Applicants (children in Parent mode, single student in Student mode) -->
              @for (a of applicants(); track $index; let i = $index) {
                <div class="bg-white rounded-xl border border-gray-200 p-6">
                  <div class="flex items-center justify-between mb-4">
                    <h3 class="text-base font-semibold text-gray-900">
                      @if (mode() === 'Parent') {
                        Child {{ i + 1 }}
                      } @else {
                        Student Information
                      }
                    </h3>
                    @if (mode() === 'Parent' && applicants().length > 1) {
                      <button type="button" (click)="removeApplicant(i)"
                              class="text-sm text-red-500 hover:text-red-700 font-medium inline-flex items-center gap-1">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" /></svg>
                        Remove
                      </button>
                    }
                  </div>

                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                      <label class="form-label">{{ fieldLabel('firstName', 'First Name') }} *</label>
                      <input type="text" [(ngModel)]="a.firstName" [name]="'firstName_' + i" required class="form-input" />
                    </div>
                    @if (isFieldVisible('middleName')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('middleName', 'Middle Name') }} <span *ngIf="isFieldRequired('middleName')">*</span></label>
                        <input type="text" [ngModel]="a.middleName" (ngModelChange)="a.middleName = $event" [name]="'middleName_' + i" [required]="isFieldRequired('middleName')" class="form-input" />
                      </div>
                    }
                    <div>
                      <label class="form-label">{{ fieldLabel('lastName', 'Last Name') }} *</label>
                      <input type="text" [(ngModel)]="a.lastName" [name]="'lastName_' + i" required class="form-input" />
                    </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
                    <div>
                      <label class="form-label">{{ fieldLabel('dateOfBirth', 'Date of Birth') }} *</label>
                      <input type="date" [(ngModel)]="a.dateOfBirth" [name]="'dateOfBirth_' + i" required class="form-input" />
                    </div>
                    <div>
                      <label class="form-label">{{ fieldLabel('gender', 'Gender') }} *</label>
                      <select [(ngModel)]="a.gender" [name]="'gender_' + i" required class="form-input">
                        <option value="">Select</option>
                        @for (opt of fieldOptions('gender'); track opt) { <option [value]="opt">{{ opt }}</option> }
                      </select>
                    </div>
                    <div>
                      <label class="form-label">{{ fieldLabel('email', mode() === 'Parent' ? "Child's Email" : 'Email') }} <span *ngIf="mode() === 'Student' || isFieldRequired('email')">*</span></label>
                      <input type="email" [(ngModel)]="a.email" [name]="'email_' + i" [required]="mode() === 'Student' || isFieldRequired('email')" class="form-input" />
                      @if (mode() === 'Student') {
                        <p class="mt-1 text-xs text-gray-400">You'll sign in with this email once approved.</p>
                      } @else {
                        <p class="mt-1 text-xs text-gray-400">Optional — the parent account above is used to sign in.</p>
                      }
                    </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                    @if (isFieldVisible('contactNumber')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('contactNumber', 'Contact Number') }} <span *ngIf="isFieldRequired('contactNumber')">*</span></label>
                        <input type="text" [ngModel]="a.contactNumber" (ngModelChange)="a.contactNumber = $event" [name]="'contactNumber_' + i" [required]="isFieldRequired('contactNumber')" class="form-input" />
                      </div>
                    }
                    @if (isFieldVisible('address')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('address', 'Address') }} <span *ngIf="isFieldRequired('address')">*</span></label>
                        <input type="text" [ngModel]="a.address" (ngModelChange)="a.address = $event" [name]="'address_' + i" [required]="isFieldRequired('address')" class="form-input" />
                      </div>
                    }
                  </div>

                  <!-- Student-section custom fields (admin-defined) -->
                  @for (cf of customFieldsFor('Student'); track cf.id) {
                    <div class="mt-4">
                      <label class="form-label">{{ cf.label }} <span *ngIf="cf.isRequired">*</span></label>
                      @switch (cf.fieldType) {
                        @case ('TextArea') { <textarea [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" rows="3" class="form-input"></textarea> }
                        @case ('Number')   { <input type="number" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                        @case ('Date')     { <input type="date" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                        @case ('Checkbox') { <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700"><input type="checkbox" [ngModel]="customCheckboxValue(i, cf.fieldKey)" (ngModelChange)="setCustomCheckboxValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" />Yes</label> }
                        @case ('Dropdown') { <select [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input"><option value="">Select</option>@for (opt of customDropdownOptions(cf); track opt) { <option [value]="opt">{{ opt }}</option> }</select> }
                        @default           { <input type="text" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                      }
                      @if (cf.helpText) { <p class="mt-1 text-xs text-gray-400">{{ cf.helpText }}</p> }
                    </div>
                  }

                  <hr class="my-5 border-gray-100" />
                  <p class="text-sm font-medium text-gray-700 mb-3">Enrollment Details</p>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label class="form-label">{{ fieldLabel('gradeLevel', 'Grade Level') }} *</label>
                      <select [(ngModel)]="a.gradeLevel" [name]="'gradeLevel_' + i" required class="form-input">
                        <option value="">Select</option>
                        @for (g of gradeLevels; track g) { <option [value]="g">{{ g }}</option> }
                      </select>
                    </div>
                    <div>
                      <label class="form-label">{{ fieldLabel('schoolYear', 'School Year') }} *</label>
                      <input type="text" [value]="a.schoolYear || ''" [name]="'schoolYear_' + i" readonly class="form-input bg-gray-50 cursor-not-allowed" />
                    </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                    @if (isFieldVisible('previousSchool')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('previousSchool', 'Previous School') }} <span *ngIf="isFieldRequired('previousSchool')">*</span></label>
                        <input type="text" [ngModel]="a.previousSchool" (ngModelChange)="a.previousSchool = $event" [name]="'previousSchool_' + i" [required]="isFieldRequired('previousSchool')" class="form-input" />
                      </div>
                    }
                    @if (isFieldVisible('previousSchoolAddress')) {
                      <div>
                        <label class="form-label">{{ fieldLabel('previousSchoolAddress', 'Previous School Address') }} <span *ngIf="isFieldRequired('previousSchoolAddress')">*</span></label>
                        <input type="text" [ngModel]="a.previousSchoolAddress" (ngModelChange)="a.previousSchoolAddress = $event" [name]="'previousSchoolAddress_' + i" [required]="isFieldRequired('previousSchoolAddress')" class="form-input" />
                      </div>
                    }
                  </div>

                  <!-- Enrollment-section custom fields -->
                  @for (cf of customFieldsFor('Enrollment'); track cf.id) {
                    <div class="mt-4">
                      <label class="form-label">{{ cf.label }} <span *ngIf="cf.isRequired">*</span></label>
                      @switch (cf.fieldType) {
                        @case ('TextArea') { <textarea [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" rows="3" class="form-input"></textarea> }
                        @case ('Number')   { <input type="number" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                        @case ('Date')     { <input type="date" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                        @case ('Checkbox') { <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700"><input type="checkbox" [ngModel]="customCheckboxValue(i, cf.fieldKey)" (ngModelChange)="setCustomCheckboxValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" />Yes</label> }
                        @case ('Dropdown') { <select [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input"><option value="">Select</option>@for (opt of customDropdownOptions(cf); track opt) { <option [value]="opt">{{ opt }}</option> }</select> }
                        @default           { <input type="text" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                      }
                      @if (cf.helpText) { <p class="mt-1 text-xs text-gray-400">{{ cf.helpText }}</p> }
                    </div>
                  }

                  @if (mode() === 'Student') {
                    <hr class="my-5 border-gray-100" />
                    <p class="text-sm font-medium text-gray-700 mb-3">Guardian Information</p>
                    <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                      @if (isFieldVisible('guardianName')) {
                        <div>
                          <label class="form-label">{{ fieldLabel('guardianName', 'Guardian Name') }} <span *ngIf="isFieldRequired('guardianName')">*</span></label>
                          <input type="text" [ngModel]="a.guardianName" (ngModelChange)="a.guardianName = $event" [name]="'guardianName_' + i" [required]="isFieldRequired('guardianName')" class="form-input" />
                        </div>
                      }
                      @if (isFieldVisible('guardianContact')) {
                        <div>
                          <label class="form-label">{{ fieldLabel('guardianContact', 'Guardian Contact') }} <span *ngIf="isFieldRequired('guardianContact')">*</span></label>
                          <input type="text" [ngModel]="a.guardianContact" (ngModelChange)="a.guardianContact = $event" [name]="'guardianContact_' + i" [required]="isFieldRequired('guardianContact')" class="form-input" />
                        </div>
                      }
                      @if (isFieldVisible('guardianRelationship')) {
                        <div>
                          <label class="form-label">{{ fieldLabel('guardianRelationship', 'Relationship') }} <span *ngIf="isFieldRequired('guardianRelationship')">*</span></label>
                          <select [ngModel]="a.guardianRelationship" (ngModelChange)="a.guardianRelationship = $event" [name]="'guardianRelationship_' + i" [required]="isFieldRequired('guardianRelationship')" class="form-input">
                            <option [ngValue]="null">Select</option>
                            @for (opt of fieldOptions('guardianRelationship'); track opt) { <option [value]="opt">{{ opt }}</option> }
                          </select>
                        </div>
                      }
                    </div>

                    <!-- Guardian-section custom fields -->
                    @for (cf of customFieldsFor('Guardian'); track cf.id) {
                      <div class="mt-4">
                        <label class="form-label">{{ cf.label }} <span *ngIf="cf.isRequired">*</span></label>
                        @switch (cf.fieldType) {
                          @case ('TextArea') { <textarea [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" rows="3" class="form-input"></textarea> }
                          @case ('Number')   { <input type="number" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                          @case ('Date')     { <input type="date" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                          @case ('Checkbox') { <label class="inline-flex items-center gap-2 mt-1 text-sm text-gray-700"><input type="checkbox" [ngModel]="customCheckboxValue(i, cf.fieldKey)" (ngModelChange)="setCustomCheckboxValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" />Yes</label> }
                          @case ('Dropdown') { <select [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input"><option value="">Select</option>@for (opt of customDropdownOptions(cf); track opt) { <option [value]="opt">{{ opt }}</option> }</select> }
                          @default           { <input type="text" [ngModel]="customValue(i, cf.fieldKey)" (ngModelChange)="setCustomValue(i, cf.fieldKey, $event)" [name]="'cf_' + i + '_' + cf.fieldKey" [required]="cf.isRequired" class="form-input" /> }
                        }
                        @if (cf.helpText) { <p class="mt-1 text-xs text-gray-400">{{ cf.helpText }}</p> }
                      </div>
                    }
                  } @else {
                    <hr class="my-5 border-gray-100" />
                    <p class="text-sm text-gray-500 italic">Guardian: the parent above will be set as this child's guardian automatically.</p>
                  }
                </div>
              }

              <!-- Add Another Child (Parent mode only) -->
              @if (mode() === 'Parent') {
                <button type="button" (click)="addApplicant()"
                        class="w-full rounded-xl border-2 border-dashed border-gray-300 hover:border-[#4361ee] hover:bg-blue-50/30 transition-colors py-4 text-sm font-medium text-gray-600 hover:text-[#4361ee] inline-flex items-center justify-center gap-2">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" /></svg>
                  Add Another Child
                </button>
              }

              <div class="flex items-center justify-between">
                @if (isParentLoggedIn()) {
                  <a routerLink="/parent/dashboard" class="text-sm text-gray-500 hover:text-gray-700">Cancel</a>
                } @else {
                  <a routerLink="/login" class="text-sm text-gray-500 hover:text-gray-700">Already have an account? Sign in</a>
                }
                <button type="submit" [disabled]="saving()"
                        class="flex items-center gap-2 rounded-xl bg-[#4361ee] px-8 py-3 text-sm font-semibold text-white hover:bg-[#3a56d4] transition-colors disabled:opacity-60">
                  @if (saving()) {
                    <svg class="h-5 w-5 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
                    Submitting...
                  } @else {
                    {{ applicants().length > 1 ? 'Submit ' + applicants().length + ' Applications' : 'Submit Application' }}
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
  submittedAppNumbers = signal<string[]>([]);
  mode = signal<ApplyMode>('Parent');

  // Parent-context fields (used in Parent mode for anonymous applicants)
  parentFirstName = '';
  parentLastName = '';
  parentEmail = '';
  parentContactNumber = '';
  parentRelationship = '';

  // Must be declared BEFORE `applicants` so blankApplicant() reads the right value during field init.
  private activeSchoolYear = '';

  // The tenant (school) the applicant is applying to — driven by the :slug route param
  // (e.g. /tenants/mshs/apply). Authenticated parents adding another child can land on bare
  // /apply where slug is null; in that case we resolve their tenant from auth and call the
  // legacy GUID-based endpoints.
  tenant = signal<PublicTenant | null>(null);
  private slug: string | null = null;

  // Exposed to the success screen so it can link to the public /:slug/status page.
  statusSlug(): string | null { return this.slug; }
  private tenantId: string | null = null;

  // Form-field config from the admin's settings, fetched on init.
  fieldConfig = signal<ApplicationFormField[]>([]);
  // Parent-section custom field values (filled once, copied to each application on submit).
  parentCustomValues: Record<string, string | null> = {};

  applicants = signal<ApplicantData[]>([this.blankApplicant()]);

  isParentLoggedIn = computed(() => this.auth.isLoggedIn() && this.auth.userRole() === 'Parent');

  gradeLevels = GRADE_LEVELS;

  constructor(private api: ApiService, private auth: AuthService, private router: Router, private route: ActivatedRoute, public syService: SchoolYearService) {
    this.slug = this.route.snapshot.paramMap.get('slug');

    if (this.slug) {
      // Anonymous applicant on /tenants/:slug/apply — use the slug-based public endpoints.
      this.api.getSchoolBySlug(this.slug).subscribe({
        next: t => {
          this.tenant.set(t);
          this.tenantId = t.id;
        },
        error: () => this.router.navigate(['/tenants'])
      });

      this.api.getSchoolFormFields(this.slug).subscribe(fields => {
        this.fieldConfig.set(fields);
      });
    } else if (this.auth.isLoggedIn()) {
      // Authenticated parent re-applying for another child — resolve via auth.
      this.tenantId = this.auth.getTenantId();
      this.api.getPublicTenant(this.tenantId).subscribe({
        next: t => this.tenant.set(t),
        error: () => this.router.navigate(['/tenants'])
      });
      this.api.getPublicApplicationFormFields(this.tenantId).subscribe(fields => {
        this.fieldConfig.set(fields);
      });
    } else {
      // No tenant context at all — bounce to the directory.
      this.router.navigate(['/tenants']);
      return;
    }

    // Anonymous slug flow uses the public slug endpoint (no tenant header required);
    // the authenticated bare-/apply flow keeps the tenant-scoped endpoint (tenant from auth).
    const schoolYears$ = this.slug
      ? this.api.getSchoolSchoolYears(this.slug)
      : this.api.getSchoolYears();
    schoolYears$.subscribe(list => {
      this.syService.setList(list);
      const active = list.find(sy => sy.isActive);
      if (active) {
        this.activeSchoolYear = active.name;
        this.applicants.update(arr => arr.map(a => ({ ...a, schoolYear: a.schoolYear || active.name })));
      }
    });
  }

  // ----- Field config helpers (used by template) -----

  private getField(key: string): ApplicationFormField | undefined {
    return this.fieldConfig().find(f => f.fieldKey === key);
  }

  isFieldVisible(key: string): boolean {
    const f = this.getField(key);
    if (!f) return true;            // unknown / unconfigured field defaults to visible
    if (!this.appliesToCurrentMode(f.appliesTo)) return false;
    return f.isVisible;
  }

  isFieldRequired(key: string): boolean {
    return this.getField(key)?.isRequired ?? false;
  }

  fieldLabel(key: string, fallback: string): string {
    return this.getField(key)?.label ?? fallback;
  }

  fieldOptions(key: string): string[] {
    const f = this.getField(key);
    if (!f?.options) return [];
    try {
      const parsed = JSON.parse(f.options);
      return Array.isArray(parsed) ? parsed.map(String) : [];
    } catch { return []; }
  }

  customFieldsFor(section: FormFieldSection): ApplicationFormField[] {
    return this.fieldConfig()
      .filter(f => !f.isBuiltIn && f.section === section && this.appliesToCurrentMode(f.appliesTo))
      .sort((a, b) => a.displayOrder - b.displayOrder);
  }

  customDropdownOptions(field: ApplicationFormField): string[] {
    if (!field.options) return [];
    try {
      const parsed = JSON.parse(field.options);
      return Array.isArray(parsed) ? parsed.map(String) : [];
    } catch { return []; }
  }

  private appliesToCurrentMode(applies: string): boolean {
    const effectiveMode = this.isParentLoggedIn() ? 'Parent' : this.mode();
    if (applies === 'Both') return true;
    if (applies === 'ParentMode') return effectiveMode === 'Parent';
    if (applies === 'StudentMode') return effectiveMode === 'Student';
    return true;
  }

  // Two-way value accessors so the same template can drive textboxes, numbers, dates, checkboxes, dropdowns.
  customValue(applicantIndex: number, key: string): string {
    const a = this.applicants()[applicantIndex];
    return a?.customFieldValues?.[key] ?? '';
  }

  setCustomValue(applicantIndex: number, key: string, value: string) {
    this.applicants.update(arr => arr.map((a, i) => {
      if (i !== applicantIndex) return a;
      const map = { ...(a.customFieldValues ?? {}) };
      map[key] = value;
      return { ...a, customFieldValues: map };
    }));
  }

  customCheckboxValue(applicantIndex: number, key: string): boolean {
    return this.customValue(applicantIndex, key) === 'true';
  }

  setCustomCheckboxValue(applicantIndex: number, key: string, value: boolean) {
    this.setCustomValue(applicantIndex, key, value ? 'true' : 'false');
  }

  parentCustomValue(key: string): string {
    return this.parentCustomValues[key] ?? '';
  }

  setParentCustomValue(key: string, value: string) {
    this.parentCustomValues = { ...this.parentCustomValues, [key]: value };
  }

  parentCustomCheckboxValue(key: string): boolean {
    return this.parentCustomValue(key) === 'true';
  }

  setParentCustomCheckboxValue(key: string, value: boolean) {
    this.setParentCustomValue(key, value ? 'true' : 'false');
  }

  setMode(m: ApplyMode) {
    this.mode.set(m);
    if (m === 'Student') {
      // Student mode is single-applicant
      const first = this.applicants()[0] ?? this.blankApplicant();
      this.applicants.set([first]);
      // Clear parent fields
      this.parentFirstName = '';
      this.parentLastName = '';
      this.parentEmail = '';
      this.parentContactNumber = '';
    }
  }

  addApplicant() {
    this.applicants.update(arr => [...arr, this.blankApplicant()]);
  }

  removeApplicant(i: number) {
    this.applicants.update(arr => arr.filter((_, idx) => idx !== i));
  }

  onSubmit() {
    this.saving.set(true);
    this.error.set('');

    const isParentMode = this.isParentLoggedIn() || this.mode() === 'Parent';

    // In Parent mode, the parent IS the guardian — auto-populate per-child guardian fields
    // from the parent (anonymous: from form fields; authenticated: from the logged-in user).
    // Parent-section custom values are merged into every applicant's customFieldValues so the
    // server stores a self-contained snapshot per application.
    const parentCustomValuesSnapshot = { ...this.parentCustomValues };
    const applicantsForSubmit = this.applicants().map(a => {
      const mergedCustom = { ...parentCustomValuesSnapshot, ...(a.customFieldValues ?? {}) };
      const customFieldValues = Object.keys(mergedCustom).length > 0 ? mergedCustom : null;

      if (!isParentMode) return { ...a, customFieldValues };

      const guardianName = this.isParentLoggedIn()
        ? (this.auth.user()?.fullName ?? null)
        : `${this.parentFirstName} ${this.parentLastName}`.trim() || null;
      const guardianContact = this.isParentLoggedIn() ? null : (this.parentContactNumber || null);
      const guardianRelationship = this.isParentLoggedIn() ? null : (this.parentRelationship || null);
      return { ...a, guardianName, guardianContact, guardianRelationship, customFieldValues };
    });

    const payload: SubmitApplicationRequest = {
      applicationType: this.isParentLoggedIn() ? 'Parent' : this.mode(),
      parentFirstName: this.mode() === 'Parent' && !this.isParentLoggedIn() ? this.parentFirstName : null,
      parentLastName: this.mode() === 'Parent' && !this.isParentLoggedIn() ? this.parentLastName : null,
      parentEmail: this.mode() === 'Parent' && !this.isParentLoggedIn() ? this.parentEmail : null,
      parentContactNumber: this.mode() === 'Parent' && !this.isParentLoggedIn() ? this.parentContactNumber : null,
      applicants: applicantsForSubmit
    };

    // Slug-based POST when the URL provides a slug; legacy ID-based otherwise (authenticated parent re-apply).
    const obs = this.slug
      ? this.api.applyToSchool(this.slug, payload)
      : this.api.submitApplication(payload, this.tenantId ?? undefined);

    obs.subscribe({
      next: (res) => {
        this.saving.set(false);
        this.submitted.set(true);
        this.submittedAppNumbers.set(res.map(r => r.applicationNumber));
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(this.formatApiError(err));
      }
    });
  }

  /** Turns the API's validation payload into readable, field-level lines.
   *  The middleware serializes FluentValidation failures with PascalCase keys
   *  ({ PropertyName, ErrorMessage }) — tolerate both casings. */
  private formatApiError(err: any): string {
    const details = err?.error?.details;
    if (Array.isArray(details) && details.length > 0) {
      const lines = details.map((d: any) => {
        const prop: string = d.PropertyName ?? d.propertyName ?? '';
        const msg: string = d.ErrorMessage ?? d.errorMessage ?? 'Invalid value.';
        const m = /^Applicants\[(\d+)\]/.exec(prop);
        const who = m ? `${this.mode() === 'Parent' ? 'Child' : 'Applicant'} ${Number(m[1]) + 1}: ` : '';
        return `• ${who}${msg}`;
      });
      return lines.join('\n');
    }
    return err?.error?.error || 'Submission failed. Please check your inputs.';
  }

  private blankApplicant(): ApplicantData {
    return {
      firstName: '', middleName: null, lastName: '', email: '',
      contactNumber: null, gender: '', dateOfBirth: '', address: null,
      gradeLevel: '', schoolYear: this.activeSchoolYear,
      previousSchool: null, previousSchoolAddress: null,
      guardianName: null, guardianContact: null, guardianRelationship: null,
      customFieldValues: null
    };
  }
}
