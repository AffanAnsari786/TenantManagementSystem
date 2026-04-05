import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Factory that restricts a route to a set of allowed roles.
 * Usage:
 *   { path: 'admin', component: AdminComponent,
 *     canActivate: [authGuard, roleGuard(['admin'])] }
 */
export const roleGuard = (allowedRoles: string[]): CanActivateFn => (_route, state) => {
  const platformId = inject(PLATFORM_ID);
  if (!isPlatformBrowser(platformId)) {
    return true;
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isLoggedIn()) {
    return router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }

  const role = (auth.getRole() ?? '').toLowerCase();
  const allowed = allowedRoles.map(r => r.toLowerCase());
  if (allowed.includes(role)) {
    return true;
  }

  return router.createUrlTree(['/404']);
};
