import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

/**
 * Staff sign-in. Opens on a choice of method rather than a form, because the
 * card scanner is the faster route for anyone carrying one; the password form
 * is revealed on request.
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

  isScannerOpen = false;
  scanError = '';
  showPassword = false;

  /** The footer year was hardcoded to 2024 and had gone stale. */
  readonly year = new Date().getFullYear();

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
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

  openScanner(): void {
    this.scanError = '';
    this.isScannerOpen = true;
  }

  closeScanner(): void {
    this.isScannerOpen = false;
    this.scanError = '';
  }

  /**
   * Staff cards are not issued yet, so a scan here can only be a patient
   * card. Rather than fail flatly, it signs the holder into the portal —
   * which is what someone scanning a patient card actually wants.
   */
  onScanned(raw: string): void {
    this.scanError = '';
    this.authService.patientLoginWithCard(raw).subscribe({
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
