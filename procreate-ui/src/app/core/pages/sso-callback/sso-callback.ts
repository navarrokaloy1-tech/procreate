import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../services/auth';
import { SsoService, SsoAudience } from '../../services/sso';

/**
 * Where the identity provider drops the browser back.
 *
 * It arrives holding either a one-time ticket or a message about what went
 * wrong. A ticket is redeemed immediately and the screen is never really
 * seen; anything else stops here with something readable and a way back.
 */
@Component({
  selector: 'app-sso-callback',
  standalone: false,
  templateUrl: './sso-callback.html',
  styleUrl: './sso-callback.scss',
})
export class SsoCallbackComponent implements OnInit {
  errorMessage = '';

  /** Which sign-in screen this started from, so "back" goes to the right one. */
  audience: SsoAudience = 'staff';

  readonly year = new Date().getFullYear();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService,
    private sso: SsoService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.audience = params.get('audience') === 'patient' ? 'patient' : 'staff';

    const error = params.get('error');
    if (error) {
      this.errorMessage = error;
      return;
    }

    const ticket = params.get('ticket');
    if (!ticket) {
      this.errorMessage = 'That sign-in did not complete. Please try again.';
      return;
    }

    this.sso.exchange(ticket).subscribe({
      next: (session) => {
        this.auth.adopt(session);
        // Replaced rather than pushed: the ticket is spent, so going back to
        // this URL would only produce an error.
        this.router.navigate([this.landingRoute()], { replaceUrl: true });
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'That sign-in could not be completed.';
        this.cdr.markForCheck();
      },
    });
  }

  get signInRoute(): string {
    return this.audience === 'patient' ? '/portal/login' : '/login';
  }

  /** Cashiers land on Patient Orders; everyone else on the dashboard. */
  private landingRoute(): string {
    if (this.auth.isPatient) return '/portal';
    return this.auth.currentUser?.role === 'Cashier' ? '/app/cashier' : '/app/dashboard';
  }
}
