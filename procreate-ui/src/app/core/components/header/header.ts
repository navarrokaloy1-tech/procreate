import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, filter, map, startWith } from 'rxjs';
import { AuthService, AuthUser } from '../../services/auth';

interface Crumb {
  section: string;
  page: string;
}

@Component({
  selector: 'app-header',
  standalone: false,
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent implements OnInit {
  currentUser$!: Observable<AuthUser | null>;
  crumb$!: Observable<Crumb>;
  menuOpen = false;

  /** Section > Page label for each top-level feature route. */
  private static readonly CRUMBS: Record<string, Crumb> = {
    dashboard: { section: 'Clinic Operations', page: 'Dashboard' },
    appointments: { section: 'Clinic Operations', page: 'Appointments' },
    'reception-queue': { section: 'Clinic Operations', page: 'Reception Queue' },
    cashier: { section: 'Clinic Operations', page: 'Cashier Desk' },
    doctors: { section: 'Clinic Setup', page: 'Doctors' },
    patients: { section: 'Patient Care', page: 'Patient Management' },
    visits: { section: 'Patient Care', page: 'Patient Services' },
    'lab-results': { section: 'Patient Care', page: 'Laboratory & Diagnostics' },
    orders: { section: 'Billing & Orders', page: 'Orders' },
    billing: { section: 'Billing & Orders', page: 'Billing' },
    reports: { section: 'Reports', page: 'Reports & Analytics' }
  };

  constructor(private authService: AuthService, private router: Router) {}

  ngOnInit(): void {
    this.currentUser$ = this.authService.user$;

    this.crumb$ = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      startWith(null),
      map(() => this.resolveCrumb())
    );
  }

  private resolveCrumb(): Crumb {
    const segment = this.router.url.split('?')[0].split('/').filter(Boolean)[1] ?? '';
    return HeaderComponent.CRUMBS[segment] ?? { section: 'General', page: 'Pro-Create' };
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0])
      .join('')
      .toUpperCase();
  }

  toggleMenu(): void {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  logout(): void {
    this.menuOpen = false;
    this.authService.logout();
  }
}
