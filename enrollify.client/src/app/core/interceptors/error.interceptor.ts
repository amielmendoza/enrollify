import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Global HTTP error handling. A 401 while logged in means the JWT expired or is
// invalid: if a refresh token exists, exchange it ONCE (AuthService.refreshSession
// is single-flight, so a burst of concurrent 401s shares one refresh) and retry the
// original request with the new JWT. Refresh failure — or a 401 on the retried
// request — is a dead session: clear it and send the user to /login.
//
// 401s from /auth/* endpoints (login, refresh, change-password) NEVER trigger a
// refresh: there they are real credential failures, and refreshing on the refresh
// endpoint itself would loop. Anonymous 401s (e.g. a bad login attempt) also pass
// through untouched so callers can show their own message; the login page's
// status === 0 "can't reach the server" handling is likewise unaffected.
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401 && authService.isLoggedIn()) {
        const isAuthEndpoint = req.url.includes('/auth/');
        if (!isAuthEndpoint && authService.getRefreshToken()) {
          return authService.refreshSession().pipe(
            catchError(() => {
              // The refresh token was rejected — the session is dead.
              authService.logout();
              return throwError(() => err);
            }),
            switchMap(() => {
              // Retry with the rotated JWT (other headers, incl. X-Tenant-Id, carry over).
              const retried = req.clone({ setHeaders: { Authorization: `Bearer ${authService.getToken()}` } });
              return next(retried).pipe(
                catchError((retryErr: unknown) => {
                  // Still 401 on a fresh JWT: give up on the session. Any other
                  // error (validation 400s etc.) passes through to the caller.
                  if (retryErr instanceof HttpErrorResponse && retryErr.status === 401) {
                    authService.logout();
                  }
                  return throwError(() => retryErr);
                })
              );
            })
          );
        }
        authService.logout();
      }
      return throwError(() => err);
    })
  );
};
