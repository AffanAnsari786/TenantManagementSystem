import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

/**
 * Adds the stored auth token to requests that need it (entries API, share generate, etc.).
 * Does not add token to login or to public share GET (viewing a shared link).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.getToken();
  const url = req.url;

  // Do not send token to login or to public share view (GET /api/share/{token})
  if (url.includes('/api/login')) return next(req);
  const afterShare = url.split('/api/share/')[1];
  if (req.method === 'GET' && afterShare && !afterShare.includes('/')) return next(req);

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }
  return next(req);
};
