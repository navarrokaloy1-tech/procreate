import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

export interface Visit {
  id: number;
  visitCode: string;
  patientName: string;
  visitDate: string;
  status: string;
  testsCount: number;
  totalAmount: number;
  paymentStatus: string;
}

export interface StatusFilter {
  label: string;
  value: string;
}

@Component({
  selector: 'app-visit-list',
  standalone: false,
  templateUrl: './visit-list.html',
  styleUrl: './visit-list.scss',
})
export class VisitList implements OnInit {
  visits: Visit[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  isLoading = false;
  activeFilter = 'All';
  statusFilters: string[] = ['All', 'Registered', 'Processing', 'Completed', 'Cancelled'];

  constructor(
    private apiService: ApiService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadVisits();
  }

  loadVisits(): void {
    this.isLoading = true;
    const params = {
      status: this.activeFilter === 'All' ? '' : this.activeFilter,
      pageIndex: this.pageIndex,
      pageSize: this.pageSize,
    };

    this.apiService
      .get<{ data: Visit[]; total: number }>('visits', { ...params, page: this.pageIndex + 1 })
      .subscribe({
        next: (response) => {
          this.visits = response.data;
          this.totalCount = response.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onFilterChange(filter: string): void {
    this.activeFilter = filter;
    this.pageIndex = 0;
    this.loadVisits();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.loadVisits();
  }

  updateStatus(visitId: number, status: string): void {
    this.apiService
      .patch<Visit>('visits/' + visitId + '/status', { status })
      .subscribe({
        next: () => {
          this.loadVisits();
          this.cdr.markForCheck();
        },
      });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Registered':
        return 'badge badge-primary';
      case 'Processing':
        return 'badge badge-warning';
      case 'Completed':
        return 'badge badge-success';
      case 'Cancelled':
        return 'badge badge-danger';
      default:
        return 'badge badge-secondary';
    }
  }

  navigateToNew(): void {
    this.router.navigate(['/app/visits/new']);
  }

  navigateToVisit(id: number): void {
    this.router.navigate(['/app/visits', id]);
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get pages(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i);
  }
}
