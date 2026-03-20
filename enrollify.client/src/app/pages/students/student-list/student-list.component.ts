import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Student } from '../../../core/models';

@Component({
  selector: 'app-student-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold text-gray-900">Students</h2>
        <a routerLink="/students/new" class="btn btn-primary">
          + Add Student
        </a>
      </div>

      <div class="bg-white rounded-xl border border-gray-200">
        <div class="p-4 border-b border-gray-200">
          <input type="text" [(ngModel)]="search" (ngModelChange)="onSearch()" placeholder="Search by name or LRN..."
                 class="form-input max-w-sm" />
        </div>

        <div class="overflow-x-auto">
          <table class="w-full">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">LRN</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Gender</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Contact</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Guardian</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-200">
              @for (student of students(); track student.id) {
                <tr class="hover:bg-gray-50 transition-colors">
                  <td class="px-6 py-4 text-sm font-mono text-gray-700">{{ student.lrn }}</td>
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ student.fullName }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.gender }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.contactNumber }}</td>
                  <td class="px-6 py-4 text-sm text-gray-600">{{ student.guardianName }}</td>
                  <td class="px-6 py-4 text-sm text-right">
                    <a [routerLink]="['/students', student.id, 'edit']" class="text-[#4361ee] hover:text-[#3a56d4] font-medium">Edit</a>
                  </td>
                </tr>
              }
              @empty {
                <tr><td colspan="6" class="px-6 py-8 text-center text-gray-400">No students found</td></tr>
              }
            </tbody>
          </table>
        </div>

        @if (totalPages() > 1) {
          <div class="flex items-center justify-between p-4 border-t border-gray-200">
            <p class="text-sm text-gray-500">Showing page {{ page() }} of {{ totalPages() }} ({{ totalCount() }} total)</p>
            <div class="flex gap-2">
              <button (click)="loadPage(page() - 1)" [disabled]="page() <= 1"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Previous</button>
              <button (click)="loadPage(page() + 1)" [disabled]="page() >= totalPages()"
                      class="px-3 py-1.5 border border-gray-200 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">Next</button>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class StudentListComponent implements OnInit {
  students = signal<Student[]>([]);
  search = '';
  page = signal(1);
  totalCount = signal(0);
  totalPages = signal(0);

  constructor(private api: ApiService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getStudents(this.search, this.page(), 20).subscribe(result => {
      this.students.set(result.items);
      this.totalCount.set(result.totalCount);
      this.totalPages.set(result.totalPages);
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.load();
  }

  loadPage(p: number): void {
    this.page.set(p);
    this.load();
  }
}
