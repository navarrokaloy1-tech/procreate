import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';

export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private base = 'http://localhost:5230/api';
  private userSubject = new BehaviorSubject<AuthUser | null>(this.loadUser());
  user$ = this.userSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {}

  private loadUser(): AuthUser | null {
    const u = localStorage.getItem('user');
    return u ? JSON.parse(u) : null;
  }

  get currentUser(): AuthUser | null { return this.userSubject.value; }
  get isLoggedIn(): boolean { return !!localStorage.getItem('token'); }
  get token(): string | null { return localStorage.getItem('token'); }

  login(username: string, password: string): Observable<any> {
    return this.http.post<any>(`${this.base}/auth/login`, { username, password }).pipe(
      tap(res => {
        localStorage.setItem('token', res.token);
        localStorage.setItem('user', JSON.stringify(res.user));
        this.userSubject.next(res.user);
      })
    );
  }

  logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    this.userSubject.next(null);
    this.router.navigate(['/login']);
  }
}
