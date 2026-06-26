import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';

interface NavItem {
  label: string;
  route: string;
  icon: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: false,
  templateUrl: './sidebar.html',
  styleUrls: ['./sidebar.scss']
})
export class SidebarComponent implements OnInit {
  navItems: NavItem[] = [
    { label: 'Dashboard',   route: '/app/dashboard',   icon: '📊' },
    { label: 'Patients',    route: '/app/patients',    icon: '👤' },
    { label: 'Visits',      route: '/app/visits',      icon: '🏥' },
    { label: 'Lab Results', route: '/app/lab-results', icon: '🧪' },
    { label: 'Billing',     route: '/app/billing',     icon: '💰' },
    { label: 'Reports',     route: '/app/reports',     icon: '📈' }
  ];

  constructor(private router: Router) {}

  ngOnInit(): void {}

  isActive(route: string): boolean {
    return this.router.isActive(route, {
      paths: 'subset',
      queryParams: 'ignored',
      fragment: 'ignored',
      matrixParams: 'ignored'
    });
  }
}
