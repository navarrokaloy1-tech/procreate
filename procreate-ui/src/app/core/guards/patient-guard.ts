import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth';

/**
 * Guards the patient portal. A staff session is turned away rather than shown
 * an empty portal: the portal reads /portal/me, which their token cannot
 * satisfy, so it would only fail once the page had loaded.
 */
@Injectable({ providedIn: 'root' })
export class PatientGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (this.auth.isLoggedIn && this.auth.isPatient) return true;

    this.router.navigate([this.auth.isLoggedIn ? '/app/dashboard' : '/portal/login']);
    return false;
  }
}
