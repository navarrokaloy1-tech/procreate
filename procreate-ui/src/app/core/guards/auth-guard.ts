import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(): boolean {
    // A patient session must not reach the console: its token is rejected by
    // every staff endpoint, so the screens would load empty.
    if (this.auth.isLoggedIn && !this.auth.isPatient) return true;

    this.router.navigate([this.auth.isPatient ? '/portal' : '/login']);
    return false;
  }
}
