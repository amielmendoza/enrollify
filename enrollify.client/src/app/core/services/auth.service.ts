import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _user = signal<LoginResponse | null>(null);
  private refreshInFlight$: Observable<LoginResponse> | null = null;

  user = this._user.asReadonly();
  isLoggedIn = computed(() => !!this._user());
  userRole = computed(() => this._user()?.role ?? '');
  // True while the account runs on a temporary password — authGuard quarantines
  // the session to /change-password until it is cleared.
  mustChangePassword = computed(() => this._user()?.mustChangePassword ?? false);

  constructor(private http: HttpClient, private router: Router) {
    const stored = localStorage.getItem('enrollify_user');
    if (stored) {
      this._user.set(JSON.parse(stored));
    }
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap(response => this.applySession(response))
    );
  }

  /**
   * Exchange the refresh token for a new JWT + rotated refresh token.
   * Single-flight: concurrent 401s share the one in-flight call (shareReplay),
   * so N failed requests trigger exactly one refresh.
   */
  refreshSession(): Observable<LoginResponse> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http
        .post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken: this.getRefreshToken() })
        .pipe(
          tap({
            next: (res) => { this.applySession(res); this.refreshInFlight$ = null; },
            error: () => { this.refreshInFlight$ = null; }
          }),
          shareReplay(1)
        );
    }
    return this.refreshInFlight$;
  }

  /** The server clears the temporary-password flag on change-password; mirror it locally. */
  clearMustChangePassword(): void {
    const current = this._user();
    if (!current) return;
    const updated: LoginResponse = { ...current, mustChangePassword: false };
    this._user.set(updated);
    localStorage.setItem('enrollify_user', JSON.stringify(updated));
  }

  logout(): void {
    this._user.set(null);
    localStorage.removeItem('enrollify_user');
    localStorage.removeItem('enrollify_token');
    localStorage.removeItem('enrollify_refresh_token');
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem('enrollify_token');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('enrollify_refresh_token');
  }

  getTenantId(): string {
    return this._user()?.tenantId ?? environment.defaultTenantId;
  }

  /** Persist a login/refresh response as the current session. */
  private applySession(response: LoginResponse): void {
    this._user.set(response);
    localStorage.setItem('enrollify_user', JSON.stringify(response));
    localStorage.setItem('enrollify_token', response.token);
    if (response.refreshToken) {
      localStorage.setItem('enrollify_refresh_token', response.refreshToken);
    }
  }
}
