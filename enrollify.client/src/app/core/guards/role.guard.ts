import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Restricts a route to the roles listed in `data.roles`. The sidebar already hides links
 * by role, but nothing stopped a user from typing /students or /settings directly.
 * On mismatch, redirect to the user's own landing page instead of showing someone
 * else's (empty or forbidden) screen.
 */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const allowed = (route.data?.['roles'] as string[] | undefined) ?? [];
  const role = auth.userRole();

  if (allowed.length === 0 || allowed.includes(role)) {
    return true;
  }

  switch (role) {
    case 'SuperAdmin': router.navigate(['/super/tenants']); break;
    case 'Parent': router.navigate(['/parent/dashboard']); break;
    default: router.navigate(['/dashboard']); break;
  }
  return false;
};
