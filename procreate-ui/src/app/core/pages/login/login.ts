import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';
import { SsoService } from '../../services/sso';

/**
 * Staff sign-in. Opens on a choice of method rather than a form, because
 * single sign-on is the route most people take; the password form is
 * revealed on request and kept as a fallback for when the provider is out of
 * reach. With no provider configured there is no choice to make, so the form
 * is shown straight away.
 */
@Component({
  selector: 'app-login',
  standalone: false,
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginComponent implements OnInit {
  loginForm!: FormGroup;
  isLoading = false;
  errorMessage = '';

  /** 'choose' shows the two methods; 'password' shows the form. */
  mode: 'choose' | 'password' = 'choose';

  ssoEnabled = false;
  ssoLabel = 'Log in with SSO';

  showPassword = false;

  /** The footer year was hardcoded to 2024 and had gone stale. */
  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private sso: SsoService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      username: ['', [Validators.required]],
      password: ['', [Validators.required, Validators.minLength(4)]],
      rememberMe: [true]
    });

    if (this.authService.isLoggedIn) {
      this.router.navigate([this.landingRoute()]);
    }

    this.sso.config().subscribe((config) => {
      this.ssoEnabled = config.enabled;
      this.ssoLabel = config.displayName
        ? `Log in with ${config.displayName}`
        : 'Log in with SSO';

      // Nothing to choose between, so skip the menu.
      if (!config.enabled) this.mode = 'password';
      this.cdr.markForCheck();
    });
  }

  /** Cashiers land on the Patient Orders screen; everyone else on the dashboard. */
  private landingRoute(): string {
    return this.authService.currentUser?.role === 'Cashier' ? '/app/cashier' : '/app/dashboard';
  }

  // ----------------------------------------------------------
  // Method choice
  // ----------------------------------------------------------

  choosePassword(): void {
    this.mode = 'password';
    this.errorMessage = '';
  }

  back(): void {
    this.mode = 'choose';
    this.errorMessage = '';
    this.loginForm.reset({ username: '', password: '', rememberMe: true });
  }

  /** Leaves the app; the provider returns the browser to /auth/callback. */
  signInWithSso(): void {
    this.sso.start('staff');
  }

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  // ----------------------------------------------------------
  // Password sign-in
  // ----------------------------------------------------------

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';
    const { username, password } = this.loginForm.value;
    this.authService.login(username, password).subscribe({
      next: () => {
        this.router.navigate([this.landingRoute()]);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message || 'Invalid username or password. Please try again.';
        this.cdr.markForCheck();
      }
    });
  }

  get usernameControl() { return this.loginForm.get('username'); }
  get passwordControl() { return this.loginForm.get('password'); }
}
