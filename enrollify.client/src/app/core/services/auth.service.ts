import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _user = signal<LoginResponse | null>(null);

  user = this._user.asReadonly();
  isLoggedIn = computed(() => !!this._user());
  userRole = computed(() => this._user()?.role ?? '');

  constructor(private http: HttpClient, private router: Router) {
    const stored = localStorage.getItem('enrollify_user');
    if (stored) {
      this._user.set(JSON.parse(stored));
    }
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap(response => {
        this._user.set(response);
        localStorage.setItem('enrollify_user', JSON.stringify(response));
        localStorage.setItem('enrollify_token', response.token);
      })
    );
  }

  logout(): void {
    this._user.set(null);
    localStorage.removeItem('enrollify_user');
    localStorage.removeItem('enrollify_token');
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem('enrollify_token');
  }

  getTenantId(): string {
    return this._user()?.tenantId ?? environment.defaultTenantId;
  }
}
