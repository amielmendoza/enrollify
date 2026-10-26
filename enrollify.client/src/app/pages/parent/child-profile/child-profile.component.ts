import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { PsgcAddress, Student } from '../../../core/models';
import { emptyPsgcAddress } from '../../../core/learner';
import { GUARDIAN_RELATIONSHIP_SUGGESTIONS } from '../../../core/constants';
import { PsgcAddressComponent } from '../../../core/components/psgc-address.component';
import { LearnerSummaryComponent } from '../../../core/components/learner-summary.component';
import { formatApiError } from '../../../core/utils/format-api-error';

@Component({
  selector: 'app-child-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PsgcAddressComponent, LearnerSummaryComponent],
  template: `
    <div>
      <a routerLink="/parent/dashboard" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-2">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" /></svg>
        Back to my children
      </a>
      <h1 class="text-2xl font-bold text-gray-900">Child Profile</h1>
      <p class="mt-1 text-sm text-gray-500">View and update your child's information</p>

      <div class="mt-6 grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Profile Info -->
        <div class="lg:col-span-2">
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Personal Information</h2>

            @if (profileSuccess()) {
              <div class="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700">Profile updated successfully!</div>
            }

            @if (student()) {
              <div class="space-y-4">
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label">Full Name</label>
                    <input type="text" [value]="student()!.fullName" disabled class="form-input bg-gray-50 cursor-not-allowed" />
                  </div>
                  <div>
                    <label class="form-label">Student No.</label>
                    <input type="text" [value]="student()!.studentNumber" disabled class="form-input folio-mono bg-gray-50 cursor-not-allowed" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label">Birth Date</label>
                    <input type="text" [value]="student()!.birthDate | date:'mediumDate'" disabled class="form-input bg-gray-50 cursor-not-allowed" />
                  </div>
                  <div>
                    <label class="form-label">Gender</label>
                    <input type="text" [value]="student()!.gender" disabled class="form-input bg-gray-50 cursor-not-allowed" />
                  </div>
                </div>

                <hr class="border-gray-200" />
                <p class="text-sm font-medium text-gray-700">Editable fields</p>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label">Contact Number</label>
                    <input type="text" [(ngModel)]="editContact" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label">Email</label>
                    <input type="email" [(ngModel)]="editEmail" class="form-input" />
                  </div>
                </div>
                <div class="space-y-4">
                  <p class="form-label">Home Address</p>
                  <app-psgc-address [address]="editHome" idPrefix="child-addr" />
                  <div>
                    <label class="form-label" for="child-street">House no. / Street / Sitio / Purok</label>
                    <input id="child-street" type="text" maxlength="500" [(ngModel)]="editAddress" class="form-input" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label class="form-label" for="child-father-contact">Father's Contact Number</label>
                    <input id="child-father-contact" type="text" maxlength="50" [(ngModel)]="editFatherContact" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label" for="child-mother-contact">Mother's Contact Number</label>
                    <input id="child-mother-contact" type="text" maxlength="50" [(ngModel)]="editMotherContact" class="form-input" />
                  </div>
                </div>
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <label class="form-label">Guardian Name</label>
                    <input type="text" [(ngModel)]="editGuardianName" class="form-input" />
                  </div>
                  <div>
                    <label class="form-label" for="child-guardian-relationship">Relationship</label>
                    <input id="child-guardian-relationship" type="text" maxlength="50" list="child-guardian-relationships" [(ngModel)]="editGuardianRelationship" class="form-input" />
                    <datalist id="child-guardian-relationships">@for (r of guardianRelationships; track r) { <option [value]="r"></option> }</datalist>
                  </div>
                  <div>
                    <label class="form-label">Guardian Contact</label>
                    <input type="text" [(ngModel)]="editGuardianContact" class="form-input" />
                  </div>
                </div>
                <button (click)="saveProfile()" [disabled]="savingProfile()"
                        class="rounded-xl px-6 py-2.5 text-sm font-semibold text-white transition-colors disabled:opacity-60" style="background-color: #0038A8;">
                  {{ savingProfile() ? 'Saving...' : 'Save Changes' }}
                </button>
              </div>
            }
          </div>
          @if (student()) {
            <div class="mt-6 bg-white rounded-xl border border-[#E2D9C2] p-6">
              <h2 class="text-lg font-semibold text-gray-900 mb-4">DepEd Learner Details</h2>
              <app-learner-summary [student]="student()!" />
            </div>
          }
        </div>

        <!-- Change Password -->
        <div>
          <div class="bg-white rounded-xl border border-[#E2D9C2] p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Change Password</h2>

            @if (pwSuccess()) {
              <div class="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-sm text-emerald-700">Password changed!</div>
            }
            @if (pwError()) {
              <div class="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{{ pwError() }}</div>
            }

            <div class="space-y-4">
              <div>
                <label class="form-label">Current Password</label>
                <input type="password" [(ngModel)]="currentPw" class="form-input" />
              </div>
              <div>
                <label class="form-label">New Password</label>
                <input type="password" [(ngModel)]="newPw" class="form-input" />
              </div>
              <div>
                <label class="form-label">Confirm New Password</label>
                <input type="password" [(ngModel)]="confirmPw" class="form-input" />
              </div>
              <button (click)="changePassword()" [disabled]="changingPw() || !currentPw || !newPw || newPw !== confirmPw"
                      class="w-full rounded-xl py-2.5 text-sm font-semibold text-white transition-colors disabled:opacity-60" style="background-color: #0038A8;">
                {{ changingPw() ? 'Changing...' : 'Change Password' }}
              </button>
              @if (newPw && confirmPw && newPw !== confirmPw) {
                <p class="text-xs text-red-500">Passwords do not match</p>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ChildProfileComponent implements OnInit {
  studentId!: string;
  student = signal<Student | null>(null);
  editContact = '';
  editEmail = '';
  editAddress = '';
  editHome: PsgcAddress = emptyPsgcAddress();
  editFatherContact = '';
  editMotherContact = '';
  editGuardianName = '';
  editGuardianContact = '';
  editGuardianRelationship = '';
  readonly guardianRelationships = GUARDIAN_RELATIONSHIP_SUGGESTIONS;
  savingProfile = signal(false);
  profileSuccess = signal(false);

  currentPw = '';
  newPw = '';
  confirmPw = '';
  changingPw = signal(false);
  pwSuccess = signal(false);
  pwError = signal('');

  constructor(private api: ApiService, public auth: AuthService, private route: ActivatedRoute) {}

  ngOnInit() {
    this.studentId = this.route.snapshot.paramMap.get('id') ?? '';
    this.api.getChildProfile(this.studentId).subscribe(s => {
      this.student.set(s);
      this.editContact = s.contactNumber || '';
      this.editEmail = s.email || '';
      this.editAddress = s.address || '';
      this.editHome = { ...emptyPsgcAddress(), ...(s.profile?.homeAddress ?? {}) };
      this.editFatherContact = s.profile?.fatherContactNumber || '';
      this.editMotherContact = s.profile?.motherContactNumber || '';
      this.editGuardianName = s.guardianName || '';
      this.editGuardianContact = s.guardianContact || '';
      this.editGuardianRelationship = s.guardianRelationship || '';
    });
  }

  saveProfile() {
    this.savingProfile.set(true);
    this.profileSuccess.set(false);
    this.api.updateChildProfile(this.studentId, {
      contactNumber: this.editContact,
      email: this.editEmail,
      address: this.editAddress,
      guardianName: this.editGuardianName,
      guardianContact: this.editGuardianContact,
      guardianRelationship: this.editGuardianRelationship,
      homeAddress: this.editHome,
      fatherContactNumber: this.editFatherContact,
      motherContactNumber: this.editMotherContact
    }).subscribe({
      next: (s) => {
        this.student.set(s);
        this.savingProfile.set(false);
        this.profileSuccess.set(true);
        setTimeout(() => this.profileSuccess.set(false), 5000);
      },
      error: () => this.savingProfile.set(false)
    });
  }

  changePassword() {
    if (this.newPw !== this.confirmPw) return;
    this.changingPw.set(true);
    this.pwSuccess.set(false);
    this.pwError.set('');
    this.api.changePassword(this.currentPw, this.newPw).subscribe({
      next: () => {
        this.changingPw.set(false);
        this.pwSuccess.set(true);
        this.currentPw = '';
        this.newPw = '';
        this.confirmPw = '';
        setTimeout(() => this.pwSuccess.set(false), 5000);
      },
      error: (err) => {
        this.changingPw.set(false);
        this.pwError.set(formatApiError(err, 'Failed to change password'));
      }
    });
  }
}
