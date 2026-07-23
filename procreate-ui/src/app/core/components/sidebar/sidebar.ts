import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth';

interface NavItem {
  label: string;
  route?: string;
  icon: string;
  children?: NavItem[];
  expanded?: boolean;
}

@Component({
  selector: 'app-sidebar',
  standalone: false,
  templateUrl: './sidebar.html',
  styleUrls: ['./sidebar.scss']
})
export class SidebarComponent implements OnInit {
  navItems: NavItem[] = [];

  constructor(private router: Router, private auth: AuthService) {}

  ngOnInit(): void {
    const role = this.auth.currentUser?.role ?? '';

    if (role === 'Cashier') {
      this.navItems = [
        { label: 'Cashiers', route: '/app/cashier', icon: 'cashier' },
        { label: 'Orders', route: '/app/orders', icon: 'orders' }
      ];
    } else {
      // Admin / staff: expanded "Cashiers" group + Orders
      this.navItems = [
        {
          label: 'Cashiers',
          icon: 'cashier',
          expanded: true,
          children: [
            { label: 'Patients', route: '/app/patients', icon: 'patients' },
            { label: 'Results', route: '/app/lab-results', icon: 'results' },
            { label: 'Patient Services', route: '/app/visits', icon: 'services' }
          ]
        },
        { label: 'Orders', route: '/app/orders', icon: 'orders' }
      ];
    }
  }

  toggle(item: NavItem): void {
    if (item.children) item.expanded = !item.expanded;
  }

  isActive(route?: string): boolean {
    if (!route) return false;
    return this.router.isActive(route, {
      paths: 'subset',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }

  isGroupActive(item: NavItem): boolean {
    return !!item.children?.some(c => this.isActive(c.route));
  }
}
