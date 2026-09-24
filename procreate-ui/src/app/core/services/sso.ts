import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, shareReplay, catchError } from 'rxjs';

export interface SsoConfig {
  /** False when the API has no provider configured — hide the button. */
  enabled: boolean;
  /** Names the method on the button: "Log in with {displayName}". */
  displayName: string;
}

/** Which sign-in screen started the round trip. */
export type SsoAudience = 'staff' | 'patient';

/**
 * Single sign-on.
 *
 * The exchange with the provider happens server side, so there is very
 * little to do here: ask whether SSO is on, hand the browser over, and swap
 * the ticket we come back with for a session.
 */
@Injectable({ providedIn: 'root' })
export class SsoService {
  private base = 'http://localhost:5230/api';

  /** Asked for on both sign-in screens, so the answer is fetched once. */
  private config$?: Observable<SsoConfig>;

  constructor(private http: HttpClient) {}

  config(): Observable<SsoConfig> {
    this.config$ ??= this.http.get<SsoConfig>(`${this.base}/auth/sso/config`).pipe(
      // An API that is down should leave the password form usable rather
      // than break the screen it sits on.
      catchError(() => of({ enabled: false, displayName: '' })),
      shareReplay(1)
    );
    return this.config$;
  }

  /**
   * Leaves the app. A full navigation rather than a fetch, because the
   * provider needs to show its own screens — and with Authentik brokering
   * Google, there are two of them.
   */
  start(audience: SsoAudience): void {
    window.location.href = `${this.base}/auth/sso/start?audience=${audience}`;
  }

  /** Redeems the one-time ticket the provider sent us back with. */
  exchange(ticket: string): Observable<any> {
    return this.http.post<any>(`${this.base}/auth/sso/exchange`, { ticket });
  }
}
