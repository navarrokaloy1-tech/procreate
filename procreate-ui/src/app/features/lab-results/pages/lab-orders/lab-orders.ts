import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

export interface LabOrder {
  id: number;
  orderCode: string;
  visitCode: string;
  patientName: string;
  testName: string;
  specimenBarcode: string;
  status: string;
  orderedDate: string;
  collectedDate: string | null;
}

@Component({
  selector: 'app-lab-orders',
  standalone: false,
  templateUrl: './lab-orders.html',
  styleUrl: './lab-orders.scss',
})
export class LabOrders implements OnInit {
  orders: LabOrder[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 15, 20];
  isLoading = false;
  activeFilter = 'All';
  statusFilters = ['All', 'Ordered', 'Collected', 'Resulted', 'Released'];

  constructor(private apiService: ApiService, private router: Router, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading = true;
    this.apiService
      .get<LabOrder[]>('lab/orders', {
        status: this.activeFilter === 'All' ? '' : this.activeFilter,
      })
      .subscribe({
        next: (response) => {
          this.orders = Array.isArray(response) ? response : [];
          this.totalCount = this.orders.length;
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
    this.loadOrders();
  }

  collectSpecimen(orderId: number): void {
    const confirmed = window.confirm('Confirm specimen collection for this order?');
    if (!confirmed) return;

    this.apiService.post<void>('lab/orders/' + orderId + '/collect', {}).subscribe({
      next: () => {
        this.loadOrders();
        this.cdr.markForCheck();
      },
      error: () => {
        window.alert('Failed to collect specimen. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  navigateToEncode(orderId: number): void {
    this.router.navigate(['/app/lab-results', orderId, 'encode']);
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
  }

  onPageSizeChange(size: string | number): void {
    // Ignore a blank or junk value: pageSize 0 would divide by zero in
    // totalPages and render an Infinity-page pager with no rows.
    const parsed = Number(size);
    if (!Number.isFinite(parsed) || parsed < 1) return;

    this.pageSize = Math.floor(parsed);
    this.pageIndex = 0;
  }

  /**
   * lab/orders returns the whole list, so pages are sliced here. Refetching on
   * every page change would return the same rows and show no difference.
   */
  get pagedOrders(): LabOrder[] {
    const start = this.pageIndex * this.pageSize;
    return this.orders.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : this.pageIndex * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.totalCount);
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Ordered':
        return 'badge-info';
      case 'Collected':
        return 'badge-warning';
      case 'Resulted':
        return 'badge-secondary';
      case 'Released':
        return 'badge-success';
      default:
        return '';
    }
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get pages(): number[] {
    const total = this.totalPages;
    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i);
    }

    const current = this.pageIndex;
    const pages: number[] = [];

    pages.push(0);
    if (current > 3) pages.push(-1);

    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    if (current < total - 4) pages.push(-2);
    pages.push(total - 1);

    return pages;
  }
}
