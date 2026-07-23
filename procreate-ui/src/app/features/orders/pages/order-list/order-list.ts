import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

interface OrderRow {
  id: number;
  orderCode: string;
  patientName: string;
  patientCode: string;
  status: string;
  itemsCount: number;
  total: number;
  createdAt: string;
}

@Component({
  selector: 'app-order-list',
  standalone: false,
  templateUrl: './order-list.html',
  styleUrl: './order-list.scss'
})
export class OrderListComponent implements OnInit, OnDestroy {
  orders: OrderRow[] = [];
  total = 0;
  page = 0;
  pageSize = 10;
  searchTerm = '';
  status = '';
  isLoading = false;

  statuses = ['', 'Draft', 'Ordered', 'Cancelled', 'Completed'];

  private searchSubject = new Subject<string>();
  private subs = new Subscription();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.subs.add(
      this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(term => {
        this.searchTerm = term;
        this.page = 0;
        this.load();
      })
    );
    this.load();
  }

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  load(): void {
    this.isLoading = true;
    this.api.get<{ data: OrderRow[]; total: number }>('orders', {
      search: this.searchTerm,
      status: this.status,
      page: this.page + 1,
      pageSize: this.pageSize
    }).subscribe({
      next: res => { this.orders = res.data; this.total = res.total; this.isLoading = false; this.cdr.markForCheck(); },
      error: () => { this.isLoading = false; this.cdr.markForCheck(); }
    });
  }

  onSearch(term: string): void { this.searchSubject.next(term); }

  filterStatus(s: string): void { this.status = s; this.page = 0; this.load(); }

  statusClass(status: string): string {
    switch ((status || '').toLowerCase()) {
      case 'ordered': return 'badge--ordered';
      case 'cancelled': return 'badge--cancelled';
      case 'draft': return 'badge--draft';
      case 'completed': return 'badge--completed';
      default: return 'badge--draft';
    }
  }

  get totalPages(): number { return Math.max(1, Math.ceil(this.total / this.pageSize)); }
  get pageArray(): number[] { return Array.from({ length: this.totalPages }, (_, i) => i); }
  goPage(i: number): void { this.page = i; this.load(); }
}
