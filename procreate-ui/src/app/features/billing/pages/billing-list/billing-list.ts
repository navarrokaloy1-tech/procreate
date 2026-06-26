import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ApiService } from '../../../../core/services/api';

export interface Bill {
  id: number;
  billNumber: string;
  visitCode: string;
  patientName: string;
  totalAmount: number;
  discountAmount: number;
  netAmount: number;
  amountPaid: number;
  changeAmount: number;
  paymentMethod: string;
  status: string;
  createdAt: string;
}

@Component({
  selector: 'app-billing-list',
  standalone: false,
  templateUrl: './billing-list.html',
  styleUrl: './billing-list.scss',
})
export class BillingList implements OnInit {
  bills: Bill[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  isLoading = false;

  constructor(private apiService: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadBills();
  }

  loadBills(): void {
    this.isLoading = true;
    this.apiService
      .get<{ data: Bill[]; total: number }>('billing', {
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (response) => {
          this.bills = response.data;
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

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.loadBills();
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Paid':
        return 'badge-success';
      case 'Partial':
        return 'badge-warning';
      case 'Unpaid':
        return 'badge-danger';
      default:
        return 'badge-secondary';
    }
  }

  getPaymentMethodIcon(method: string): string {
    switch (method) {
      case 'Cash':
        return '💵';
      case 'Card':
        return '💳';
      case 'GCash':
        return '📱';
      default:
        return '💰';
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
