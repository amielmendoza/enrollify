import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Registered as BOTH canActivate and canActivateChild on the authenticated shell:
// canActivate alone would not re-run when navigating between the shell's children,
// which is exactly when the temporary-password quarantine must keep biting.
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) {
    router.navigate(['/login']);
    return false;
  }

  // Temporary-password sessions are quarantined to /change-password until changed.
  if (authService.mustChangePassword() && !state.url.startsWith('/change-password')) {
    router.navigate(['/change-password']);
    return false;
  }

  return true;
};
