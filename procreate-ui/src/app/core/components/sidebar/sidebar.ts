import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { AuthService } from '../../services/auth';

interface NavLink {
  label: string;
  route: string;
  icon: string;
}

interface NavSection {
  label: string;
  items: NavLink[];
}

@Component({
  selector: 'app-sidebar',
  standalone: false,
  templateUrl: './sidebar.html',
  styleUrls: ['./sidebar.scss']
})
export class SidebarComponent implements OnInit, OnDestroy {
  sections: NavSection[] = [];
  userName = '';

  private navigationSub?: Subscription;

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const user = this.auth.currentUser;
    this.userName = user?.fullName ?? '';
    this.sections = SidebarComponent.navFor(user?.role ?? '');

    // isActive() is read from the template, so the highlight only moves when
    // change detection runs. The click that starts a navigation runs it too
    // early — the URL has not changed yet — and this app is zoneless, so
    // nothing schedules another pass when the navigation finishes. Without
    // this the previous item stays highlighted until the next interaction.
    this.navigationSub = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => this.cdr.markForCheck());
  }

  ngOnDestroy(): void {
    this.navigationSub?.unsubscribe();
  }

  /**
   * Nav is grouped into labelled sections and scoped by role. A doctor sees
   * clinical work only — no cashier desk, billing, or reporting.
   */
  private static navFor(role: string): NavSection[] {
    if (role === 'Cashier') {
      return [
        {
          label: 'Clinic Operations',
          items: [{ label: 'Cashier Desk', route: '/app/cashier', icon: 'cashier' }]
        },
        {
          label: 'Billing & Orders',
          items: [{ label: 'Orders', route: '/app/orders', icon: 'orders' }]
        }
      ];
    }

    if (role === 'Doctor') {
      return [
        {
          label: 'Clinic Operations',
          items: [
            { label: 'Dashboard', route: '/app/dashboard', icon: 'layout-grid' },
            { label: 'Appointments', route: '/app/appointments', icon: 'calendar' }
          ]
        },
        {
          label: 'Patient Care',
          items: [
            { label: 'Patients', route: '/app/patients', icon: 'patients' },
            { label: 'Laboratory', route: '/app/lab-results', icon: 'flask' }
          ]
        },
        {
          label: 'Medical Records',
          items: [
            { label: 'Consultations', route: '/app/visits', icon: 'stethoscope' },
            {
              label: 'Medical Certificates',
              route: '/app/medical-records/certificates',
              icon: 'file-text'
            }
          ]
        }
      ];
    }

    // Admin / staff — full console
    return [
      {
        label: 'Clinic Operations',
        items: [
          { label: 'Dashboard', route: '/app/dashboard', icon: 'layout-grid' },
          { label: 'Appointments', route: '/app/appointments', icon: 'calendar' },
          { label: 'Reception Queue', route: '/app/reception-queue', icon: 'monitor' }
          // Hidden for admin/staff: overlaps with Consultations. The route and
          // the feature are untouched, and the Cashier role still gets it.
          // { label: 'Cashier Desk', route: '/app/cashier', icon: 'cashier' }
        ]
      },
      {
        label: 'Patient Care',
        items: [
          { label: 'Patients', route: '/app/patients', icon: 'patients' },
          { label: 'Laboratory', route: '/app/lab-results', icon: 'flask' }
        ]
      },
      {
        label: 'Medical Records',
        items: [
          { label: 'Consultations', route: '/app/visits', icon: 'stethoscope' },
          {
            label: 'Medical Certificates',
            route: '/app/medical-records/certificates',
            icon: 'file-text'
          }
        ]
      },
      {
        label: 'Billing & Orders',
        items: [
          { label: 'Orders', route: '/app/orders', icon: 'orders' },
          { label: 'Billing', route: '/app/billing', icon: 'credit-card' }
        ]
      },
      {
        label: 'Clinic Setup',
        items: [{ label: 'Doctors', route: '/app/doctors', icon: 'stethoscope' }]
      },
      {
        label: 'Reports',
        items: [{ label: 'Reports & Analytics', route: '/app/reports', icon: 'chart' }]
      }
    ];
  }

  isActive(route: string): boolean {
    return this.router.isActive(route, {
      paths: 'subset',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }
}
