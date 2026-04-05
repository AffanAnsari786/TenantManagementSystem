import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * - Attaches the bearer access token to outgoing API requests.
 * - On a 401, transparently refreshes via the HttpOnly cookie and replays
 *   the original request once. Concurrent 401s share a single refresh call
 *   courtesy of AuthService.refresh()'s in-flight guard.
 * - Skips token injection for login/refresh/logout and for the public share
 *   GET (`GET /api/share/{token}` — anonymous viewers).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  if (isAuthEndpoint(req) || isPublicShareGet(req)) {
    return next(req);
  }

  const authorized = attachToken(req, auth.getToken());
  return next(authorized).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401) {
        return throwError(() => err);
      }
      // Try once to refresh and replay.
      return from(auth.refresh()).pipe(
        switchMap(newToken => {
          if (!newToken) {
            return throwError(() => err);
          }
          return next(attachToken(req, newToken));
        })
      );
    })
  );
};

function attachToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  if (!token) return req;
  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}

function isAuthEndpoint(req: HttpRequest<unknown>): boolean {
  const url = req.url;
  return url.includes('/api/login'); // covers /api/login, /api/login/refresh, /api/login/logout
}

function isPublicShareGet(req: HttpRequest<unknown>): boolean {
  if (req.method !== 'GET') return false;
  const afterShare = req.url.split('/api/share/')[1];
  return !!afterShare && !afterShare.includes('/');
}
