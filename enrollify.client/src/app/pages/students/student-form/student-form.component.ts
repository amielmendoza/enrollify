import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CreateStudentRequest, LearnerProfile } from '../../../core/models';
import { ALL_FIELDS_VISIBLE, editableProfile, emptyLearnerProfile, isPsgcMatched } from '../../../core/learner';
import { EXTENSION_NAME_SUGGESTIONS } from '../../../core/constants';
import { formatApiError } from '../../../core/utils/format-api-error';
import { PsgcAddressComponent } from '../../../core/components/psgc-address.component';
import { LearnerBackgroundFieldsComponent } from '../../../core/components/learner-background-fields.component';
import { LearnerFamilyFieldsComponent } from '../../../core/components/learner-family-fields.component';

type StudentForm = Omit<CreateStudentRequest, 'profile'> & { profile: LearnerProfile };

@Component({
  selector: 'app-student-form',
  standalone: true,
  imports: [CommonModule, FormsModule, PsgcAddressComponent, LearnerBackgroundFieldsComponent, LearnerFamilyFieldsComponent],
  template: `
    <div class="max-w-3xl">
      <h2 class="text-2xl font-bold text-gray-900">{{ isEdit() ? 'Edit' : 'New' }} Student</h2>
      @if (isEdit() && studentNumber()) {
        <p class="mt-1 text-sm text-gray-500">Student No. <span class="folio-mono text-gray-800">{{ studentNumber() }}</span></p>
      }

      <div class="mt-6 space-y-4">
        @if (error()) {
          <div class="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-sm whitespace-pre-line">{{ error() }}</div>
        }
        @if (lrnNeedsReview()) {
          <div class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
            <span><strong>Check LRN.</strong> This LRN was recorded before Enrollify tracked DepEd-issued LRNs. Confirm it matches the learner's LIS record, or correct it below and save.</span>
            <button type="button" (click)="confirmLrn()" [disabled]="confirming()" class="btn btn-secondary">{{ confirming() ? 'Confirming…' : 'Confirm LRN' }}</button>
          </div>
        }
      </div>

      <form (ngSubmit)="onSubmit()" class="mt-4 space-y-6">
        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Identity</h3>
          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <label class="form-label" for="firstName">First Name *</label>
              <input id="firstName" name="firstName" type="text" required maxlength="100" class="form-input" [(ngModel)]="form.firstName" />
            </div>
            <div>
              <label class="form-label" for="middleName">Middle Name</label>
              <input id="middleName" name="middleName" type="text" maxlength="100" class="form-input" [(ngModel)]="form.middleName" />
            </div>
            <div>
              <label class="form-label" for="lastName">Last Name *</label>
              <input id="lastName" name="lastName" type="text" required maxlength="100" class="form-input" [(ngModel)]="form.lastName" />
            </div>
            <div>
              <label class="form-label" for="extensionName">Extension</label>
              <input id="extensionName" name="extensionName" type="text" maxlength="10" list="staff-extension-names" class="form-input"
                     [ngModel]="form.profile.extensionName" (ngModelChange)="form.profile.extensionName = $event || null" />
            </div>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="form-label" for="lrn">LRN</label>
              <input id="lrn" name="lrn" type="text" inputmode="numeric" maxlength="20" class="form-input folio-mono" [(ngModel)]="form.lrn" />
              <p class="mt-1 text-xs text-gray-400">12 digits from DepEd LIS. Leave blank if none yet.</p>
            </div>
            <div>
              <label class="form-label" for="birthDate">Birth Date *</label>
              <input id="birthDate" name="birthDate" type="date" required class="form-input" [(ngModel)]="form.birthDate" />
            </div>
            <div>
              <label class="form-label" for="gender">Sex</label>
              <select id="gender" name="gender" class="form-input" [(ngModel)]="form.gender">
                <option [ngValue]="null">Select</option>
                <option value="Male">Male</option>
                <option value="Female">Female</option>
              </select>
            </div>
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Background</h3>
          <app-learner-background-fields [profile]="form.profile" [config]="allFields" namePrefix="staff-bg" />
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Address</h3>
          @if (showLegacyAddressHint()) {
            <p class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-3">
              Address not matched to PSGC yet. On file: <strong>{{ form.address }}</strong>. Choose the region, city/municipality and barangay below.
            </p>
          }
          <app-psgc-address [address]="form.profile.homeAddress!" idPrefix="staff-addr" [required]="!isEdit()" />
          <div>
            <label class="form-label" for="street">House no. / Street / Sitio / Purok</label>
            <input id="street" name="address" type="text" maxlength="500" class="form-input" [(ngModel)]="form.address" />
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
          <h3 class="font-semibold text-gray-900">Family</h3>
          <app-learner-family-fields [profile]="form.profile" [config]="allFields" namePrefix="staff-fam" />
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="guardianName">Guardian Name</label>
              <input id="guardianName" name="guardianName" type="text" maxlength="200" class="form-input" [(ngModel)]="form.guardianName" />
            </div>
            <div>
              <label class="form-label" for="guardianContact">Guardian Contact</label>
              <input id="guardianContact" name="guardianContact" type="text" maxlength="50" class="form-input" [(ngModel)]="form.guardianContact" />
            </div>
          </div>
        </section>

        <section class="bg-white rounded-xl border border-[#E2D9C2] p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Contact</h3>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="contactNumber">Contact Number</label>
              <input id="contactNumber" name="contactNumber" type="text" maxlength="50" class="form-input" [(ngModel)]="form.contactNumber" />
            </div>
            <div>
              <label class="form-label" for="email">Email</label>
              <input id="email" name="email" type="email" maxlength="200" class="form-input" [(ngModel)]="form.email" />
            </div>
          </div>
        </section>

        <div class="flex gap-3">
          <button type="submit" [disabled]="saving()" class="btn btn-primary">
            {{ saving() ? 'Saving...' : (isEdit() ? 'Update' : 'Create') }} Student
          </button>
          <button type="button" (click)="onCancel()" class="btn btn-secondary">Cancel</button>
        </div>
      </form>
      <datalist id="staff-extension-names">@for (x of extensionNames; track x) { <option [value]="x"></option> }</datalist>
    </div>
  `
})
export class StudentFormComponent implements OnInit {
  isEdit = signal(false);
  saving = signal(false);
  confirming = signal(false);
  error = signal('');
  studentNumber = signal('');
  lrnNeedsReview = signal(false);
  studentId = '';
  readonly allFields = ALL_FIELDS_VISIBLE;
  readonly extensionNames = EXTENSION_NAME_SUGGESTIONS;

  form: StudentForm = this.blankForm();

  constructor(private api: ApiService, private route: ActivatedRoute, private router: Router, private notify: NotificationService) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.studentId = id;
      this.api.getStudent(id).subscribe(s => {
        this.studentNumber.set(s.studentNumber);
        this.lrnNeedsReview.set(s.lrnNeedsReview);
        this.form = {
          lrn: s.lrn, firstName: s.firstName, middleName: s.middleName, lastName: s.lastName,
          birthDate: s.birthDate.substring(0, 10), gender: s.gender, address: s.address,
          contactNumber: s.contactNumber, email: s.email, guardianName: s.guardianName, guardianContact: s.guardianContact,
          profile: editableProfile(s.profile),
        };
      });
    }
  }

  showLegacyAddressHint(): boolean {
    return this.isEdit() && !!this.form.address?.trim() && !isPsgcMatched(this.form.profile.homeAddress);
  }

  confirmLrn(): void {
    this.confirming.set(true);
    this.api.confirmLrn(this.studentId).subscribe({
      next: s => { this.confirming.set(false); this.lrnNeedsReview.set(s.lrnNeedsReview); this.notify.success('LRN confirmed.'); },
      error: err => { this.confirming.set(false); this.notify.error(formatApiError(err, 'Could not confirm the LRN.')); },
    });
  }

  onSubmit(): void {
    this.saving.set(true);
    this.error.set('');
    const payload: CreateStudentRequest = { ...this.form, lrn: this.form.lrn?.trim() || null };
    const obs = this.isEdit()
      ? this.api.updateStudent(this.studentId, payload)
      : this.api.createStudent(payload);
    obs.subscribe({
      next: () => this.router.navigate(['/students']),
      error: (err) => { this.saving.set(false); this.error.set(formatApiError(err, 'Save failed')); },
    });
  }

  onCancel(): void { this.router.navigate(['/students']); }

  private blankForm(): StudentForm {
    return {
      lrn: null, firstName: '', middleName: '', lastName: '', birthDate: '', gender: null, address: '',
      contactNumber: null, email: null, guardianName: null, guardianContact: null,
      profile: emptyLearnerProfile(),
    };
  }
}
