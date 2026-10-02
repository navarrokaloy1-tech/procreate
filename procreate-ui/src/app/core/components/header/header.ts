import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Observable, filter, map, startWith } from 'rxjs';
import { AuthService, AuthUser } from '../../services/auth';
import { ApiService } from '../../services/api';

interface Crumb {
  section: string;
  page: string;
}

interface ExpiryAlertBrief {
  itemName: string;
  batchNumber: string;
  daysToExpiry: number | null;
}

interface StockAlertBrief {
  name: string;
  currentStock: number;
  unitOfMeasure: string;
}

interface AlertsSummary {
  counts: { expired: number; expiringSoon: number; lowStock: number; outOfStock: number; total: number };
  expired: ExpiryAlertBrief[];
  expiringSoon: ExpiryAlertBrief[];
  lowStock: StockAlertBrief[];
  outOfStock: StockAlertBrief[];
}

@Component({
  selector: 'app-header',
  standalone: false,
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class HeaderComponent implements OnInit, OnDestroy {
  currentUser$!: Observable<AuthUser | null>;
  crumb$!: Observable<Crumb>;
  menuOpen = false;

  // Inventory alert bell — only for roles that can see Inventory.
  canSeeAlerts = false;
  alerts: AlertsSummary | null = null;
  alertCount = 0;
  bellOpen = false;
  private pollHandle: ReturnType<typeof setInterval> | null = null;

  /** Section > Page label for each top-level feature route. */
  private static readonly CRUMBS: Record<string, Crumb> = {
    dashboard: { section: 'Clinic Operations', page: 'Dashboard' },
    appointments: { section: 'Clinic Operations', page: 'Appointments' },
    'reception-queue': { section: 'Clinic Operations', page: 'Reception Queue' },
    cashier: { section: 'Clinic Operations', page: 'Cashier Desk' },
    doctors: { section: 'Clinic Setup', page: 'Doctors' },
    patients: { section: 'Patient Care', page: 'Patient Management' },
    visits: { section: 'Medical Records', page: 'Consultations' },
    'medical-records': { section: 'Medical Records', page: 'Medical Certificates' },
    'lab-results': { section: 'Patient Care', page: 'Laboratory & Diagnostics' },
    orders: { section: 'Billing & Orders', page: 'Orders' },
    billing: { section: 'Billing & Orders', page: 'Billing' },
    reports: { section: 'Reports', page: 'Reports & Analytics' }
  };

  constructor(
    private authService: AuthService,
    private router: Router,
    private api: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.currentUser$ = this.authService.user$;

    this.crumb$ = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      startWith(null),
      map(() => this.resolveCrumb())
    );

    // The bell mirrors the Inventory section: cashiers and doctors don't see it.
    const role = this.authService.currentUser?.role ?? '';
    this.canSeeAlerts = role !== 'Cashier' && role !== 'Doctor' && role !== 'Patient';
    if (this.canSeeAlerts) {
      this.loadAlerts();
      // A light refresh so a day-turnover or a depleted item shows up without a reload.
      this.pollHandle = setInterval(() => this.loadAlerts(), 5 * 60 * 1000);
    }
  }

  ngOnDestroy(): void {
    if (this.pollHandle) clearInterval(this.pollHandle);
  }

  private loadAlerts(): void {
    this.api.get<AlertsSummary>('inventory/alerts').subscribe({
      next: (alerts) => {
        this.alerts = alerts;
        this.alertCount = alerts?.counts?.total ?? 0;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  toggleBell(): void {
    this.bellOpen = !this.bellOpen;
  }

  closeBell(): void {
    this.bellOpen = false;
  }

  goToInventory(): void {
    this.bellOpen = false;
    this.router.navigate(['/app/inventory']);
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
