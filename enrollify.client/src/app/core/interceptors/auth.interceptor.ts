import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();
  const tenantId = authService.getTenantId();

  let headers = req.headers;
  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }
  if (tenantId) {
    headers = headers.set('X-Tenant-Id', tenantId);
  }

  return next(req.clone({ headers }));
};
