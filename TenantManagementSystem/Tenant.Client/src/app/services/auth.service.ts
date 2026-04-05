import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-tokens';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  role: string;
  username: string;
  userId: number;
}

/**
 * Authentication client.
 *
 * Security model:
 *  - Access token: short-lived (~15 min), returned in the login/refresh
 *    response body, held in memory and mirrored to localStorage so SSR
 *    rehydration works. Sent via Authorization: Bearer on every API call.
 *  - Refresh token: long-lived, stored server-side and delivered to the
 *    browser as an HttpOnly, Secure, SameSite cookie scoped to /api/login.
 *    JavaScript never sees it; refresh calls carry it automatically because
 *    HttpClient uses `withCredentials: true` on the refresh endpoint.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl: string;

  /** Re-entrancy guard for the interceptor so multiple 401s share one refresh. */
  private refreshInFlight: Promise<string | null> | null = null;
  private readonly loggedIn$ = new BehaviorSubject<boolean>(false);

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) apiBaseUrl: string
  ) {
    this.baseUrl = `${apiBaseUrl}/login`;
    // Initialise loggedIn state from localStorage if running in the browser.
    if (typeof localStorage !== 'undefined') {
      this.loggedIn$.next(!!localStorage.getItem('token'));
    }
  }

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(
        this.baseUrl,
        { username: username.trim(), password } as LoginRequest,
        { withCredentials: true }
      )
      .pipe(tap(res => this.storeSession(res)));
  }

  /**
   * Attempts to exchange the HttpOnly refresh cookie for a new access token.
   * Concurrent callers share a single in-flight promise. Returns null on
   * failure so the caller can redirect to /login.
   */
  refresh(): Promise<string | null> {
    if (this.refreshInFlight) return this.refreshInFlight;

    this.refreshInFlight = this.http
      .post<LoginResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true })
      .toPromise()
      .then(res => {
        if (!res) return null;
        this.storeSession(res);
        return res.token;
      })
      .catch(() => {
        this.clearSession();
        return null;
      })
      .finally(() => {
        this.refreshInFlight = null;
      }) as Promise<string | null>;

    return this.refreshInFlight;
  }

  logout(): Observable<unknown> {
    return this.http
      .post(`${this.baseUrl}/logout`, {}, { withCredentials: true })
      .pipe(tap(() => this.clearSession()));
  }

  getToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem('token');
  }

  getRole(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem('role');
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  loggedInChanges(): Observable<boolean> {
    return this.loggedIn$.asObservable();
  }

  private storeSession(res: LoginResponse): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.setItem('token', res.token);
    localStorage.setItem('role', res.role);
    localStorage.setItem('username', res.username);
    localStorage.setItem('userId', String(res.userId));
    this.loggedIn$.next(true);
  }

  private clearSession(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem('token');
      localStorage.removeItem('role');
      localStorage.removeItem('username');
      localStorage.removeItem('userId');
    }
    this.loggedIn$.next(false);
  }
}
