import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ShsStrand } from '../models';

/** SHS track/strand picker grouped by track. Emits { track, strand } (both null to clear). */
@Component({
  selector: 'app-strand-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <label class="form-label" [attr.for]="selectId">{{ label }} <span *ngIf="required">*</span></label>
    <select class="form-input" [id]="selectId" [disabled]="disabled"
            [ngModel]="selectedId()" (ngModelChange)="select($event)" [ngModelOptions]="{ standalone: true }">
      <option [ngValue]="null">Select track / strand</option>
      @if (selectedId() === LEGACY) {
        <option [ngValue]="LEGACY" disabled>{{ track }} – {{ strand }} (no longer offered)</option>
      }
      @for (g of groups(); track g.track) {
        <optgroup [label]="g.track">
          @for (s of g.items; track s.id) { <option [ngValue]="s.id">{{ s.strand }}</option> }
        </optgroup>
      }
    </select>
  `
})
export class StrandSelectComponent {
  @Input() strands: ShsStrand[] = [];
  @Input() track: string | null = null;
  @Input() strand: string | null = null;
  @Input() required = false;
  @Input() disabled = false;
  @Input() label = 'Track / Strand';
  // Not "id": a static id attribute would also land on the host element, duplicating the DOM id.
  @Input() selectId = 'strand';
  @Output() changed = new EventEmitter<{ track: string | null; strand: string | null }>();

  readonly LEGACY = '__legacy__';

  groups(): { track: string; items: ShsStrand[] }[] {
    const out: { track: string; items: ShsStrand[] }[] = [];
    for (const s of this.strands) {
      let g = out.find(x => x.track === s.track);
      if (!g) { g = { track: s.track, items: [] }; out.push(g); }
      g.items.push(s);
    }
    return out;
  }

  selectedId(): string | null {
    if (!this.track || !this.strand) return null;
    const eq = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();
    const m = this.strands.find(s => eq(s.track, this.track!) && eq(s.strand, this.strand!));
    return m ? m.id : this.LEGACY;
  }

  select(id: string | null): void {
    const s = this.strands.find(x => x.id === id);
    this.changed.emit({ track: s?.track ?? null, strand: s?.strand ?? null });
  }
}
