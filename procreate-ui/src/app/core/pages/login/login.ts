import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

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
      password: ['', [Validators.required, Validators.minLength(4)]]
    });
    // redirect if already logged in
    if (this.authService.isLoggedIn) {
      this.router.navigate([this.landingRoute()]);
    }
  }

  /** Cashiers land on the Patient Orders screen; everyone else on the dashboard. */
  private landingRoute(): string {
    return this.authService.currentUser?.role === 'Cashier' ? '/app/cashier' : '/app/dashboard';
  }

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
