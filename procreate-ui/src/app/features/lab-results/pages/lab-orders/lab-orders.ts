import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

export interface LabOrder {
  id: number;
  orderCode: string;
  visitCode: string;
  patientName: string;
  patientCode: string;
  testName: string;
  testCode: string;
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

  // --- Scan-to-find ---
  isScannerOpen = false;
  scanError = '';
  /** The code currently narrowing the list, shown as a removable chip. */
  scanFilter = '';
  /** What the scanned code matched, so the chip can say so. */
  scanFilterLabel = '';
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
   * Orders left after a scan filter. Everything downstream — paging, counts,
   * the range readout — works off this rather than the raw list.
   */
  get visibleOrders(): LabOrder[] {
    if (!this.scanFilter) return this.orders;

    const term = this.scanFilter.toLowerCase();
    return this.orders.filter(
      (o) =>
        o.specimenBarcode?.toLowerCase() === term ||
        o.patientCode?.toLowerCase() === term ||
        o.orderCode?.toLowerCase() === term
    );
  }

  /**
   * lab/orders returns the whole list, so pages are sliced here. Refetching on
   * every page change would return the same rows and show no difference.
   */
  get pagedOrders(): LabOrder[] {
    const start = this.pageIndex * this.pageSize;
    return this.visibleOrders.slice(start, start + this.pageSize);
  }

  // ----------------------------------------------------------
  // Scan to find
  // ----------------------------------------------------------

  openScanner(): void {
    this.scanError = '';
    this.isScannerOpen = true;
  }

  closeScanner(): void {
    this.isScannerOpen = false;
    this.scanError = '';
  }

  /**
   * Accepts a specimen barcode, an order code, or a patient QR. The list is
   * already loaded, so matching happens here rather than round-tripping.
   */
  onScanned(raw: string): void {
    const code = this.normaliseScan(raw);
    if (!code) return;

    const lower = code.toLowerCase();

    const specimen = this.orders.find((o) => o.specimenBarcode?.toLowerCase() === lower);
    if (specimen) {
      this.applyScan(code, `Specimen ${specimen.specimenBarcode} — ${specimen.testName}`);
      return;
    }

    const order = this.orders.find((o) => o.orderCode?.toLowerCase() === lower);
    if (order) {
      this.applyScan(code, `Order ${order.orderCode} — ${order.testName}`);
      return;
    }

    const patientOrders = this.orders.filter((o) => o.patientCode?.toLowerCase() === lower);
    if (patientOrders.length > 0) {
      this.applyScan(
        code,
        `${patientOrders[0].patientName} — ${patientOrders.length} order${
          patientOrders.length === 1 ? '' : 's'
        }`
      );
      return;
    }

    // A patient with no orders is a different problem from an unknown code,
    // so say which one it is.
    this.apiService.get<{ firstName: string; lastName: string }>(
      `patients/by-code/${encodeURIComponent(code)}`
    ).subscribe({
      next: (patient) => {
        this.scanError =
          `${patient.firstName} ${patient.lastName} has no lab orders` +
          (this.activeFilter === 'All' ? '.' : ` under the "${this.activeFilter}" filter.`);
        this.cdr.markForCheck();
      },
      error: () => {
        this.scanError = `Nothing matches "${code}".`;
        this.cdr.markForCheck();
      },
    });
  }

  private applyScan(code: string, label: string): void {
    this.scanFilter = code;
    this.scanFilterLabel = label;
    this.pageIndex = 0;
    this.scanError = '';
    this.isScannerOpen = false;
  }

  /** Strips a "patient:" scheme so a card carrying one still matches. */
  private normaliseScan(raw: string): string {
    const trimmed = (raw ?? '').trim();
    const scheme = 'patient:';
    return trimmed.toLowerCase().startsWith(scheme)
      ? trimmed.slice(scheme.length).trim()
      : trimmed;
  }

  clearScanFilter(): void {
    this.scanFilter = '';
    this.scanFilterLabel = '';
    this.pageIndex = 0;
  }

  get filteredCount(): number {
    return this.visibleOrders.length;
  }

  get rangeStart(): number {
    return this.filteredCount === 0 ? 0 : this.pageIndex * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.filteredCount);
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
    return Math.ceil(this.filteredCount / this.pageSize);
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
