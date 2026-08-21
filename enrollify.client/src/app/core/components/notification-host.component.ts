import { Component, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../services/notification.service';

// Renders the NotificationService state: toast stack + confirm/prompt dialogs.
// Mounted once in AppComponent next to the router outlet, so it overlays every
// page — inside MainLayout and on standalone pages alike. z-indices sit above
// the hand-rolled page modals (z-50).
@Component({
  selector: 'app-notification-host',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <!-- Toasts -->
    <div class="fixed top-4 right-4 z-[120] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
      @for (t of notify.toasts(); track t.id) {
        <div class="flex items-start gap-3 rounded-xl border bg-white p-4 shadow-lg"
             [class]="t.kind === 'success' ? 'border-emerald-200' : t.kind === 'error' ? 'border-red-200' : 'border-blue-200'">
          @if (t.kind === 'success') {
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
          } @else if (t.kind === 'error') {
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" /></svg>
          } @else {
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 shrink-0 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="m11.25 11.25.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9-3.75h.008v.008H12V8.25Z" /></svg>
          }
          <p class="flex-1 whitespace-pre-line text-sm text-gray-800">{{ t.message }}</p>
          <button (click)="notify.dismissToast(t.id)" class="shrink-0 rounded p-0.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600" aria-label="Dismiss">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" /></svg>
          </button>
        </div>
      }
    </div>

    <!-- Confirm dialog -->
    @if (notify.confirmState(); as c) {
      <div class="fixed inset-0 z-[110] flex items-center justify-center bg-gray-900/50 p-4" (click)="notify.resolveConfirm(false)">
        <div class="w-full max-w-md rounded-xl bg-white p-6 shadow-xl" (click)="$event.stopPropagation()">
          <h3 class="text-lg font-semibold text-gray-900">{{ c.title }}</h3>
          <p class="mt-2 whitespace-pre-line text-sm text-gray-600">{{ c.message }}</p>
          <div class="mt-6 flex items-center justify-end gap-3">
            <button (click)="notify.resolveConfirm(false)" class="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100">{{ c.cancelLabel }}</button>
            <button (click)="notify.resolveConfirm(true)"
                    class="rounded-lg px-5 py-2 text-sm font-semibold text-white"
                    [class]="c.danger ? 'bg-red-600 hover:bg-red-700' : 'bg-[#4361ee] hover:bg-[#3a56d4]'">
              {{ c.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Prompt dialog -->
    @if (notify.promptState(); as p) {
      <div class="fixed inset-0 z-[110] flex items-center justify-center bg-gray-900/50 p-4" (click)="notify.resolvePrompt(null)">
        <div class="w-full max-w-md rounded-xl bg-white p-6 shadow-xl" (click)="$event.stopPropagation()">
          <h3 class="text-lg font-semibold text-gray-900">{{ p.title }}</h3>
          <p class="mt-2 whitespace-pre-line text-sm text-gray-600">{{ p.message }}</p>
          <input type="text" [(ngModel)]="promptValue" (keyup.enter)="submitPrompt()"
                 [placeholder]="p.placeholder" class="form-input mt-4" autofocus />
          @if (p.required && !promptValue.trim()) {
            <p class="mt-1 text-xs text-gray-400">This field is required.</p>
          }
          <div class="mt-6 flex items-center justify-end gap-3">
            <button (click)="notify.resolvePrompt(null)" class="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100">{{ p.cancelLabel }}</button>
            <button (click)="submitPrompt()" [disabled]="p.required && !promptValue.trim()"
                    class="rounded-lg px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
                    [class]="p.danger ? 'bg-red-600 hover:bg-red-700' : 'bg-[#4361ee] hover:bg-[#3a56d4]'">
              {{ p.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class NotificationHostComponent {
  promptValue = '';

  constructor(public notify: NotificationService) {
    // Seed the input whenever a new prompt opens.
    effect(() => {
      const p = this.notify.promptState();
      if (p) this.promptValue = p.initialValue;
    });
  }

  submitPrompt(): void {
    const p = this.notify.promptState();
    if (!p) return;
    if (p.required && !this.promptValue.trim()) return;
    this.notify.resolvePrompt(this.promptValue);
  }
}
