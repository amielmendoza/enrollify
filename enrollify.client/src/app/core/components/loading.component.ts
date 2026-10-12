import { Component, input } from '@angular/core';

// The one loading indicator for the app: a quiet manila-tinted spinner.
// Drop it wherever data is being fetched; empty states must only render when
// the fetch has finished AND came back empty — never as a flash before it.
@Component({
  selector: 'app-loading',
  standalone: true,
  template: `
    <div class="flex items-center justify-center gap-2.5 py-8 text-sm text-gray-400">
      <svg class="h-5 w-5 animate-spin text-[#C9B77E]" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
      </svg>
      <span>{{ label() }}</span>
    </div>
  `
})
export class LoadingComponent {
  label = input('Loading...');
}
