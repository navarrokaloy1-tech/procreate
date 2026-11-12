import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { AuthService, AuthUser } from '../../services/auth';

interface NavLink {
  label: string;
  route: string;
  icon: string;
  /** Sub-links revealed when the row is expanded (e.g. Appointments → Consultations / Procedures). */
  children?: NavLink[];
}

interface NavSection {
  label: string;
  items: NavLink[];
}

/** One toggleable feature, its sidebar destination and where it is grouped. */
interface FeatureNav extends NavLink {
  key: string;
  section: string;
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

  /** Parent routes whose sub-links are currently revealed. */
  private expanded = new Set<string>();

  private navigationSub?: Subscription;

  /** The two calendars Appointments opens into — shared by every role's nav. */
  private static readonly APPOINTMENT_CHILDREN: NavLink[] = [
    { label: 'Consultations', route: '/app/appointments/consultations', icon: 'stethoscope' },
    { label: 'Procedures', route: '/app/appointments/procedures', icon: 'scan' },
  ];

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const user = this.auth.currentUser;
    this.userName = user?.fullName ?? '';
    this.sections = SidebarComponent.navFor(user);
    this.syncExpanded();

    // isActive() is read from the template, so the highlight only moves when
    // change detection runs. The click that starts a navigation runs it too
    // early — the URL has not changed yet — and this app is zoneless, so
    // nothing schedules another pass when the navigation finishes. Without
    // this the previous item stays highlighted until the next interaction.
    this.navigationSub = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.syncExpanded();
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.navigationSub?.unsubscribe();
  }

  /**
   * Every feature a custom role can grant, with where it lands in the sidebar.
   * The order here is the order they appear; sections follow SECTION_ORDER.
   */
  private static readonly FEATURE_CATALOGUE: FeatureNav[] = [
    { key: 'dashboard', label: 'Dashboard', route: '/app/dashboard', icon: 'layout-grid', section: 'Clinic Operations' },
    { key: 'appointments', label: 'Appointments', route: '/app/appointments', icon: 'calendar', section: 'Clinic Operations', children: SidebarComponent.APPOINTMENT_CHILDREN },
    { key: 'reception-queue', label: 'Reception Queue', route: '/app/reception-queue', icon: 'monitor', section: 'Clinic Operations' },
    { key: 'patients', label: 'Patients', route: '/app/patients', icon: 'patients', section: 'Patient Care' },
    { key: 'laboratory', label: 'Diagnostic', route: '/app/lab-results', icon: 'flask', section: 'Patient Care' },
    { key: 'doctors', label: 'Doctors', route: '/app/doctors', icon: 'stethoscope', section: 'Clinic Setup' },
    { key: 'services', label: 'Services', route: '/app/services', icon: 'clipboard', section: 'Clinic Setup' },
    { key: 'test-panels', label: 'Test Panels', route: '/app/test-panels', icon: 'flask', section: 'Clinic Setup' },
    { key: 'inventory', label: 'Inventory', route: '/app/inventory', icon: 'package', section: 'Clinic Setup' },
    { key: 'user-management', label: 'User Management', route: '/app/users', icon: 'users', section: 'Account settings' },
    { key: 'login-activity', label: 'Login Activity', route: '/app/login-activity', icon: 'shield', section: 'Account settings' },
    { key: 'reports', label: 'Reports & Analytics', route: '/app/reports', icon: 'chart', section: 'Reports' },
  ];

  private static readonly SECTION_ORDER =
    ['Clinic Operations', 'Patient Care', 'Clinic Setup', 'Account settings', 'Reports'];

  /**
   * Nav is grouped into labelled sections. A custom role shows exactly the
   * features it was granted; otherwise the built-in role decides — a doctor sees
   * clinical work only, and so on.
   */
  private static navFor(user: AuthUser | null): NavSection[] {
    const features = user?.features;
    if (features && features.length > 0) return SidebarComponent.buildFromFeatures(features);

    const role = user?.role ?? '';
    if (role === 'Cashier') {
      return [
        {
          label: 'Clinic Operations',
          items: [{ label: 'Cashier Desk', route: '/app/cashier', icon: 'cashier' }]
        }
        // Hidden until the Billing & Orders scope is confirmed. Routes and
        // features are untouched — uncomment to bring the section back.
        // ,{
        //   label: 'Billing & Orders',
        //   items: [{ label: 'Orders', route: '/app/orders', icon: 'orders' }]
        // }
      ];
    }

    if (role === 'Doctor') {
      return [
        {
          label: 'Clinic Operations',
          items: [
            { label: 'Dashboard', route: '/app/dashboard', icon: 'layout-grid' },
            { label: 'Appointments', route: '/app/appointments', icon: 'calendar', children: SidebarComponent.APPOINTMENT_CHILDREN }
          ]
        },
        {
          label: 'Patient Care',
          items: [
            { label: 'Patients', route: '/app/patients', icon: 'patients' },
            { label: 'Diagnostic', route: '/app/lab-results', icon: 'flask' }
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
          { label: 'Appointments', route: '/app/appointments', icon: 'calendar', children: SidebarComponent.APPOINTMENT_CHILDREN },
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
          { label: 'Diagnostic', route: '/app/lab-results', icon: 'flask' }
        ]
      },
      // {
      //   label: 'Medical Records',
      //   items: [
      //     { label: 'Consultations', route: '/app/visits', icon: 'stethoscope' },
      //     {
      //       label: 'Medical Certificates',
      //       route: '/app/medical-records/certificates',
      //       icon: 'file-text'
      //     }
      //   ]
      // },
      // Hidden until the Billing & Orders scope is confirmed. Routes and
      // features are untouched — uncomment to bring the section back.
      // {
      //   label: 'Billing & Orders',
      //   items: [
      //     { label: 'Orders', route: '/app/orders', icon: 'orders' },
      //     { label: 'Billing', route: '/app/billing', icon: 'credit-card' }
      //   ]
      // },
      {
        label: 'Clinic Setup',
        items: [
          { label: 'Doctors', route: '/app/doctors', icon: 'stethoscope' },
          { label: 'Services', route: '/app/services', icon: 'clipboard' },
          { label: 'Test Panels', route: '/app/test-panels', icon: 'flask' },
          { label: 'Inventory', route: '/app/inventory', icon: 'package' }
        ]
      },
      {
        label: 'Account settings',
        items: [
          { label: 'User Management', route: '/app/users', icon: 'users' },
          { label: 'Custom Roles', route: '/app/custom-roles', icon: 'shield-check' },
          { label: 'Login Activity', route: '/app/login-activity', icon: 'shield' }
        ]
      },
      {
        label: 'Reports',
        items: [{ label: 'Reports & Analytics', route: '/app/reports', icon: 'chart' }]
      }
    ];
  }

  /** Builds the nav from a custom role's granted feature keys, grouped in order. */
  private static buildFromFeatures(features: string[]): NavSection[] {
    const granted = new Set(features);
    const chosen = SidebarComponent.FEATURE_CATALOGUE.filter((f) => granted.has(f.key));

    return SidebarComponent.SECTION_ORDER
      .map((section) => ({
        label: section,
        items: chosen
          .filter((f) => f.section === section)
          .map((f) => ({ label: f.label, route: f.route, icon: f.icon, children: f.children })),
      }))
      .filter((s) => s.items.length > 0);
  }

  isActive(route: string): boolean {
    return this.router.isActive(route, {
      paths: 'subset',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }

  /**
   * A sub-link is active for its own route, and also when the shared Batches
   * page was opened from it — Batches carries a `mode` query param saying which
   * calendar (Consultations / Procedures) it belongs to.
   */
  isChildActive(route: string): boolean {
    if (this.isActive(route)) return true;

    const url = this.router.url;
    if (url.startsWith('/app/appointments/batches')) {
      const mode = url.includes('mode=procedure') ? 'procedure' : 'consultation';
      if (route.endsWith('/procedures')) return mode === 'procedure';
      if (route.endsWith('/consultations')) return mode === 'consultation';
    }
    return false;
  }

  /** Whether a parent row's sub-links are currently shown. */
  isExpanded(item: NavLink): boolean {
    return this.expanded.has(item.route);
  }

  /** Clicking a parent row reveals or hides its sub-links. */
  toggle(item: NavLink): void {
    if (this.expanded.has(item.route)) this.expanded.delete(item.route);
    else this.expanded.add(item.route);
  }

  /** Keeps a parent open whenever it, or one of its children, is active. */
  private syncExpanded(): void {
    for (const section of this.sections) {
      for (const item of section.items) {
        if (!item.children) continue;
        if (this.isActive(item.route) || item.children.some((child) => this.isChildActive(child.route))) {
          this.expanded.add(item.route);
        }
      }
    }
  }
}
