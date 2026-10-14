import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { PsgcAddress } from '../models';
import { PsgcCity, PsgcIndex, PsgcPlace, PsgcService } from '../services/psgc.service';

/**
 * Region → (province) → city/municipality → barangay → ZIP, from the shipped PSGC files.
 * Mutates `address` in place (codes + names) and emits (changed) after every edit. Regions with
 * no provinces (NCR) skip the province step; regions that have provinces but also independent
 * cities offer an "Independent / highly urbanized city" choice in the province list.
 */
@Component({
  selector: 'app-psgc-address',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-region'">Region <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-region'" [disabled]="disabled || !index()"
                [ngModel]="address.regionCode" (ngModelChange)="selectRegion($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">{{ index() ? 'Select region' : (indexFailed() ? 'Unavailable' : 'Loading…') }}</option>
          @for (r of index()?.regions ?? []; track r.code) { <option [ngValue]="r.code">{{ r.name }}</option> }
        </select>
        @if (indexFailed()) {
          <p class="mt-1 text-xs text-red-600">Address list unavailable — reload the page to try again.</p>
        }
      </div>
      @if (showProvinceStep()) {
        <div>
          <label class="form-label" [attr.for]="idPrefix + '-province'">Province <span *ngIf="required">*</span></label>
          <select class="form-input" [id]="idPrefix + '-province'" [disabled]="disabled"
                  [ngModel]="provinceChoice()" (ngModelChange)="selectProvince($event)" [ngModelOptions]="{ standalone: true }">
            <option [ngValue]="null">Select province</option>
            @for (p of provinces(); track p.code) { <option [ngValue]="p.code">{{ p.name }}</option> }
            @if (hasIndependentCities()) { <option [ngValue]="NO_PROVINCE">Independent / highly urbanized city</option> }
          </select>
        </div>
      }
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-city'">City / Municipality <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-city'" [disabled]="disabled || cities().length === 0"
                [ngModel]="address.cityMunicipalityCode" (ngModelChange)="selectCity($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">Select city / municipality</option>
          @for (c of cities(); track c.code) { <option [ngValue]="c.code">{{ c.name }}</option> }
        </select>
      </div>
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-barangay'">Barangay <span *ngIf="required">*</span></label>
        <select class="form-input" [id]="idPrefix + '-barangay'" [disabled]="disabled || barangays().length === 0"
                [ngModel]="address.barangayCode" (ngModelChange)="selectBarangay($event)" [ngModelOptions]="{ standalone: true }">
          <option [ngValue]="null">{{ loadingBarangays() ? 'Loading…' : 'Select barangay' }}</option>
          @for (b of barangays(); track b.code) { <option [ngValue]="b.code">{{ b.name }}</option> }
        </select>
      </div>
      <div>
        <label class="form-label" [attr.for]="idPrefix + '-zip'">ZIP code</label>
        <input class="form-input folio-mono" [id]="idPrefix + '-zip'" inputmode="numeric" maxlength="10" [disabled]="disabled"
               [ngModel]="address.zipCode" (ngModelChange)="setZip($event)" [ngModelOptions]="{ standalone: true }" />
      </div>
    </div>
  `
})
export class PsgcAddressComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) address!: PsgcAddress;
  @Input() idPrefix = 'addr';
  @Input() required = false;
  @Input() disabled = false;
  @Output() changed = new EventEmitter<PsgcAddress>();

  readonly NO_PROVINCE = '__none__';

  index = signal<PsgcIndex | null>(null);
  indexFailed = signal(false);
  provinceChoice = signal<string | null>(null);
  barangays = signal<PsgcPlace[]>([]);
  loadingBarangays = signal(false);
  private regionCode = signal<string | null>(null);
  private indexSub: Subscription;
  private barangaySub?: Subscription;

  provinces = computed(() => (this.index()?.provinces ?? []).filter(p => p.regionCode === this.regionCode()));
  private regionCities = computed(() => (this.index()?.cities ?? []).filter(c => c.regionCode === this.regionCode()));
  hasIndependentCities = computed(() => this.regionCities().some(c => c.provinceCode === null));
  showProvinceStep = computed(() => this.provinces().length > 0);
  cities = computed(() => {
    const all = this.regionCities();
    if (!this.showProvinceStep()) return all;
    const choice = this.provinceChoice();
    if (choice === this.NO_PROVINCE) return all.filter(c => c.provinceCode === null);
    return choice ? all.filter(c => c.provinceCode === choice) : [];
  });

  constructor(private psgc: PsgcService) {
    this.indexSub = this.psgc.index().subscribe({
      next: ix => { this.index.set(ix); this.syncFromAddress(); },
      error: () => this.indexFailed.set(true),
    });
  }

  ngOnChanges(): void { this.syncFromAddress(); }

  ngOnDestroy(): void {
    this.indexSub.unsubscribe();
    this.barangaySub?.unsubscribe();
  }

  selectRegion(code: string | null): void {
    const r = this.index()?.regions.find(x => x.code === code) ?? null;
    Object.assign(this.address, {
      regionCode: r?.code ?? null, regionName: r?.name ?? null,
      provinceCode: null, provinceName: null,
      cityMunicipalityCode: null, cityMunicipalityName: null,
      barangayCode: null, barangayName: null,
    });
    this.regionCode.set(this.address.regionCode);
    this.provinceChoice.set(null);
    this.clearBarangays();
    this.changed.emit(this.address);
  }

  selectProvince(choice: string | null): void {
    const p = this.provinces().find(x => x.code === choice) ?? null;
    Object.assign(this.address, {
      provinceCode: p?.code ?? null, provinceName: p?.name ?? null,
      cityMunicipalityCode: null, cityMunicipalityName: null,
      barangayCode: null, barangayName: null,
    });
    this.provinceChoice.set(choice);
    this.clearBarangays();
    this.changed.emit(this.address);
  }

  selectCity(code: string | null): void {
    const c = this.cityByCode(code);
    Object.assign(this.address, {
      cityMunicipalityCode: c?.code ?? null, cityMunicipalityName: c?.name ?? null,
      barangayCode: null, barangayName: null,
    });
    if (c && c.provinceCode === null) Object.assign(this.address, { provinceCode: null, provinceName: null });
    this.clearBarangays();
    if (c) this.loadBarangays(c);
    this.changed.emit(this.address);
  }

  selectBarangay(code: string | null): void {
    const b = this.barangays().find(x => x.code === code) ?? null;
    Object.assign(this.address, { barangayCode: b?.code ?? null, barangayName: b?.name ?? null });
    this.changed.emit(this.address);
  }

  setZip(value: string | null): void {
    this.address.zipCode = value?.trim() || null;
    this.changed.emit(this.address);
  }

  private syncFromAddress(): void {
    const a = this.address;
    if (!a || !this.index()) return;
    this.regionCode.set(a.regionCode);
    const keepNoProvince = this.provinceChoice() === this.NO_PROVINCE && !a.provinceCode && !a.cityMunicipalityCode;
    if (!keepNoProvince) this.provinceChoice.set(a.provinceCode ?? (a.cityMunicipalityCode ? this.NO_PROVINCE : null));
    const city = this.cityByCode(a.cityMunicipalityCode);
    if (city) this.loadBarangays(city);
    else this.clearBarangays();
  }

  private cityByCode(code: string | null): PsgcCity | undefined {
    return code ? this.index()?.cities.find(c => c.code === code) : undefined;
  }

  private clearBarangays(): void {
    this.barangaySub?.unsubscribe();
    this.barangays.set([]);
    this.loadingBarangays.set(false);
  }

  private loadBarangays(city: PsgcCity): void {
    this.barangaySub?.unsubscribe();
    this.loadingBarangays.set(true);
    this.barangaySub = this.psgc.barangaysOf(city).subscribe({
      next: list => { this.barangays.set(list); this.loadingBarangays.set(false); },
      error: () => { this.barangays.set([]); this.loadingBarangays.set(false); },
    });
  }
}
