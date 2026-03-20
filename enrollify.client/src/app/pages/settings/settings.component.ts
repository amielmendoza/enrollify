import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WorkflowDefinition, Fee, Section } from '../../core/models';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div>
      <h2 class="text-2xl font-bold text-gray-900 mb-6">Admin Settings</h2>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Workflows -->
        <div class="bg-white rounded-xl border border-gray-200 p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Workflow Definitions</h3>
          @for (w of workflows(); track w.id) {
            <div class="border border-gray-200 rounded-lg p-4 mb-3">
              <p class="font-medium text-gray-900">{{ w.name }}</p>
              <p class="text-sm text-gray-500">{{ w.description }}</p>
              <div class="mt-2 space-y-1">
                @for (s of w.steps; track s.id) {
                  <div class="text-xs bg-gray-50 border border-gray-200 p-2 rounded-lg flex justify-between">
                    <span class="text-gray-700">{{ s.stepOrder }}. {{ s.stepName }}</span>
                    <span class="text-gray-400">{{ s.fromStatus }} &rarr; {{ s.toStatus }}</span>
                  </div>
                }
              </div>
            </div>
          }
        </div>

        <!-- Fees -->
        <div class="bg-white rounded-xl border border-gray-200 p-6">
          <h3 class="font-semibold text-gray-900 mb-4">Fee Configuration</h3>
          @for (f of fees(); track f.id) {
            <div class="flex justify-between items-center py-3 border-b border-gray-200 last:border-0">
              <div>
                <p class="font-medium text-sm text-gray-900">{{ f.name }}</p>
                <p class="text-xs text-gray-500">{{ f.gradeLevel }} - {{ f.schoolYear }}</p>
              </div>
              <p class="font-semibold text-gray-900">{{ f.amount | number:'1.2-2' }}</p>
            </div>
          }
        </div>

        <!-- Sections -->
        <div class="bg-white rounded-xl border border-gray-200 p-6 lg:col-span-2">
          <h3 class="font-semibold text-gray-900 mb-4">Sections</h3>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            @for (s of sections(); track s.id) {
              <div class="border border-gray-200 rounded-lg p-4">
                <p class="font-medium text-gray-900">{{ s.name }}</p>
                <p class="text-sm text-gray-500">{{ s.gradeLevel }} - {{ s.schoolYear }}</p>
                <div class="mt-2">
                  <div class="flex justify-between text-sm">
                    <span class="text-gray-500">Capacity</span>
                    <span class="font-medium text-gray-900">{{ s.currentCount }}/{{ s.capacity }}</span>
                  </div>
                  <div class="w-full bg-gray-200 rounded-full h-2 mt-1">
                    <div class="bg-[#4361ee] h-2 rounded-full transition-all" [style.width.%]="(s.currentCount / s.capacity) * 100"></div>
                  </div>
                </div>
                @if (s.adviser) { <p class="text-xs text-gray-400 mt-2">Adviser: {{ s.adviser }}</p> }
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class SettingsComponent implements OnInit {
  workflows = signal<WorkflowDefinition[]>([]);
  fees = signal<Fee[]>([]);
  sections = signal<Section[]>([]);

  constructor(private api: ApiService, public auth: AuthService) {}

  ngOnInit(): void {
    this.api.getWorkflows().subscribe(w => this.workflows.set(w));
    this.api.getFees().subscribe(f => this.fees.set(f));
    this.api.getSections().subscribe(s => this.sections.set(s));
  }
}
