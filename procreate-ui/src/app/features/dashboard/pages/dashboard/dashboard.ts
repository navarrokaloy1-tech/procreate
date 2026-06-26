import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

export interface DashboardStats {
  totalPatients: number;
  todayVisits: number;
  pendingResults: number;
  todayRevenue: number;
}

export interface RecentVisit {
  id: number;
  visitCode: string;
  patientName: string;
  visitDate: string;
  testsCount: number;
  totalAmount: number;
  status: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: false,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnInit {
  stats: DashboardStats | null = null;
  recentVisits: RecentVisit[] = [];
  isLoading = true;
  currentDate = new Date();

  constructor(private apiService: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    forkJoin([
      this.apiService.get<DashboardStats>('patients/stats'),
      this.apiService.get<{ data: RecentVisit[] }>('visits', { pageSize: 10, page: 1 })
    ]).subscribe({
      next: ([stats, visitsResponse]) => {
        this.stats = stats;
        this.recentVisits = visitsResponse.data;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.isLoading = false;
        console.error('Failed to load dashboard data:', error);
        this.cdr.markForCheck();
      }
    });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'badge-success';
      case 'Processing':
        return 'badge-warning';
      case 'Registered':
        return 'badge-info';
      default:
        return 'badge-secondary';
    }
  }
}
