import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth';

/**
 * Patient sign-in. Card scan first, because most patients arrive holding one;
 * the password form and self-registration sit behind it.
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

  isScannerOpen = false;
  scanError = '';
  showPassword = false;

  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
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

  // ----------------------------------------------------------
  // Card
  // ----------------------------------------------------------

  openScanner(): void {
    this.scanError = '';
    this.isScannerOpen = true;
  }

  closeScanner(): void {
    this.isScannerOpen = false;
    this.scanError = '';
  }

  onScanned(raw: string): void {
    this.scanError = '';
    this.auth.patientLoginWithCard(raw).subscribe({
      next: () => {
        this.isScannerOpen = false;
        this.router.navigate(['/portal']);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.scanError = err?.error?.message ?? 'That card could not be read.';
        this.cdr.markForCheck();
      },
    });
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
