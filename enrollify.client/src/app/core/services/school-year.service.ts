import { Injectable, signal, computed } from '@angular/core';
import { Observable, of, tap, map, shareReplay } from 'rxjs';
import { ApiService } from './api.service';
import { SchoolYear } from '../models';

@Injectable({ providedIn: 'root' })
export class SchoolYearService {
  private _schoolYears = signal<SchoolYear[]>([]);
  private _loading$: Observable<SchoolYear[]> | null = null;

  schoolYears = this._schoolYears.asReadonly();
  active = computed(() => this._schoolYears().find(sy => sy.isActive) ?? null);
  activeName = computed(() => this.active()?.name ?? '');

  constructor(private api: ApiService) {}

  load(): void {
    this.api.getSchoolYears().subscribe(list => {
      this._schoolYears.set(list);
    });
  }

  /** Returns an observable that emits once school years are loaded */
  ensureLoaded(): Observable<SchoolYear[]> {
    if (this._schoolYears().length > 0) return of(this._schoolYears());
    if (!this._loading$) {
      this._loading$ = this.api.getSchoolYears().pipe(
        tap(list => this._schoolYears.set(list)),
        shareReplay(1)
      );
    }
    return this._loading$;
  }

  setList(list: SchoolYear[]): void {
    this._schoolYears.set(list);
  }

  refresh(): void {
    this._loading$ = null;
    this.load();
  }
}
