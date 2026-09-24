import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth';
import { SsoService } from '../../../../core/services/sso';

/**
 * Patient sign-in. Single sign-on first where it is configured, then the
 * password form, then self-registration.
 *
 * The card is no longer a way in. It stays on the patient's record as the
 * thing staff scan to find their file, which is a different job from proving
 * who is at the keyboard.
 */
@Component({
  selector: 'app-portal-login',
  standalone: false,
  templateUrl: './portal-login.html',
  styleUrl: './portal-login.scss',
})
export class PortalLoginComponent implements OnInit {
  loginForm!: FormGroup;
  isLoading = false;
  errorMessage = '';

  /** 'choose' shows the methods; 'password' shows the form. */
  mode: 'choose' | 'password' = 'choose';

  ssoEnabled = false;
  ssoLabel = 'Log in with SSO';

  showPassword = false;

  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private sso: SsoService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loginForm = this.fb.group({
      identifier: ['', [Validators.required]],
      password: ['', [Validators.required]],
    });

    // A staff session must not land in the portal, so only a patient is sent on.
    if (this.auth.isLoggedIn && this.auth.isPatient) {
      this.router.navigate(['/portal']);
    }

    this.sso.config().subscribe((config) => {
      this.ssoEnabled = config.enabled;
      this.ssoLabel = config.displayName
        ? `Log in with ${config.displayName}`
        : 'Log in with SSO';
      this.cdr.markForCheck();
    });
  }

  choosePassword(): void {
    this.mode = 'password';
    this.errorMessage = '';
  }

  back(): void {
    this.mode = 'choose';
    this.errorMessage = '';
    this.loginForm.reset({ identifier: '', password: '' });
  }

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  /** Leaves the app; the provider returns the browser to /auth/callback. */
  signInWithSso(): void {
    this.sso.start('patient');
  }

  // ----------------------------------------------------------
  // Password
  // ----------------------------------------------------------

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    const { identifier, password } = this.loginForm.value;

    this.auth.patientLogin(identifier, password).subscribe({
      next: () => {
        this.router.navigate(['/portal']);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.message ?? 'Those sign-in details did not work.';
        this.cdr.markForCheck();
      },
    });
  }

  get identifierControl() { return this.loginForm.get('identifier'); }
  get passwordControl() { return this.loginForm.get('password'); }
}
