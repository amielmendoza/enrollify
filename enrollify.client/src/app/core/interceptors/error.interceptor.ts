import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Global HTTP error handling. A 401 while logged in means the token is expired or
// invalid — clear the session and send the user back to /login instead of leaving
// them on a silently-empty page. Anonymous 401s (e.g. a bad login attempt) pass
// through untouched so callers can show their own error message.
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401 && authService.isLoggedIn()) {
        authService.logout();
      }
      return throwError(() => err);
    })
  );
};
