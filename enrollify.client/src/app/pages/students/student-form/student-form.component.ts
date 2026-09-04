import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { CreateStudentRequest } from '../../../core/models';

@Component({
  selector: 'app-student-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-2xl">
      <h2 class="text-2xl font-bold text-gray-900 mb-6">{{ isEdit() ? 'Edit' : 'New' }} Student</h2>

      @if (error()) {
        <div class="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg mb-4 text-sm">{{ error() }}</div>
      }

      <form (ngSubmit)="onSubmit()" class="bg-white rounded-xl border border-[#E2D9C2] p-6 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="form-label">LRN *</label>
            <input type="text" [(ngModel)]="form.lrn" name="lrn" required class="form-input folio-mono" />
          </div>
          <div>
            <label class="form-label">Gender</label>
            <select [(ngModel)]="form.gender" name="gender" class="form-input">
              <option [ngValue]="null">Select</option>
              <option value="Male">Male</option>
              <option value="Female">Female</option>
            </select>
          </div>
        </div>

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

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="form-label">Birth Date *</label>
            <input type="date" [(ngModel)]="form.birthDate" name="birthDate" required class="form-input" />
          </div>
          <div>
            <label class="form-label">Contact Number</label>
            <input type="text" [(ngModel)]="form.contactNumber" name="contactNumber" class="form-input" />
          </div>
        </div>

        <div>
          <label class="form-label">Address *</label>
          <input type="text" [(ngModel)]="form.address" name="address" required class="form-input" />
        </div>

        <div>
          <label class="form-label">Email</label>
          <input type="email" [(ngModel)]="form.email" name="email" class="form-input" />
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="form-label">Guardian Name</label>
            <input type="text" [(ngModel)]="form.guardianName" name="guardianName" class="form-input" />
          </div>
          <div>
            <label class="form-label">Guardian Contact</label>
            <input type="text" [(ngModel)]="form.guardianContact" name="guardianContact" class="form-input" />
          </div>
        </div>

        <div class="flex gap-3 pt-4">
          <button type="submit" [disabled]="saving()" class="btn btn-primary">
            {{ saving() ? 'Saving...' : (isEdit() ? 'Update' : 'Create') }} Student
          </button>
          <button type="button" (click)="onCancel()" class="btn btn-secondary">Cancel</button>
        </div>
      </form>
    </div>
  `
})
export class StudentFormComponent implements OnInit {
  isEdit = signal(false);
  saving = signal(false);
  error = signal('');
  studentId = '';

  form: CreateStudentRequest = {
    lrn: '', firstName: '', middleName: '', lastName: '',
    birthDate: '', gender: null, address: '',
    contactNumber: null, email: null, guardianName: null, guardianContact: null
  };

  constructor(private api: ApiService, private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.studentId = id;
      this.api.getStudent(id).subscribe(s => {
        this.form = {
          lrn: s.lrn, firstName: s.firstName, middleName: s.middleName, lastName: s.lastName,
          birthDate: s.birthDate.substring(0, 10), gender: s.gender, address: s.address,
          contactNumber: s.contactNumber, email: s.email, guardianName: s.guardianName, guardianContact: s.guardianContact
        };
      });
    }
  }

  onSubmit(): void {
    this.saving.set(true);
    this.error.set('');
    const obs = this.isEdit()
      ? this.api.updateStudent(this.studentId, this.form)
      : this.api.createStudent(this.form);
    obs.subscribe({
      next: () => this.router.navigate(['/students']),
      error: (err) => { this.saving.set(false); this.error.set(err.error?.error || 'Save failed'); }
    });
  }

  onCancel(): void { this.router.navigate(['/students']); }
}
