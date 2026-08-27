import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

export interface LabOrder {
  id: number;
  orderCode: string;
  visitCode: string;
  patientId: number;
  patientName: string;
  patientCode: string;
  testName: string;
  testCode: string;
  department: string;
  categoryName: string;
  specimenBarcode: string;
  status: string;
  /** The label staff use: Pending | In Progress | For Reading | Completed. */
  stage: string;
  isAbnormal: boolean;
  /** 'parameters' for measured tests, 'narrative' for studies that are read. */
  resultKind: string;
  orderedDate: string;
  collectedDate: string | null;
  releasedDate: string | null;
}

interface DepartmentTab {
  name: string;
  count: number;
}

@Component({
  selector: 'app-lab-orders',
  standalone: false,
  templateUrl: './lab-orders.html',
  styleUrl: './lab-orders.scss',
})
export class LabOrders implements OnInit, OnDestroy {
  orders: LabOrder[] = [];
  isLoading = false;

  /** Department tabs come from the API so the counts stay honest. */
  departments: DepartmentTab[] = [];
  activeDepartment = 'Laboratory';

  readonly stageFilters = ['All Orders', 'Pending', 'In Progress', 'For Reading', 'Completed'];
  activeStage = 'All Orders';

  searchTerm = '';

  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 15, 20];

  // --- Scan-to-find ---
  isScannerOpen = false;
  scanError = '';
  /** The code currently narrowing the list, shown as a removable chip. */
  scanFilter = '';
  /** What the scanned code matched, so the chip can say so. */
  scanFilterLabel = '';

  private searchSubject = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(
    private apiService: ApiService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const searchSub = this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.loadOrders();
        this.cdr.markForCheck();
      });

    this.subscriptions.add(searchSub);
    this.loadDepartments();
    this.loadOrders();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadDepartments(): void {
    this.apiService.get<DepartmentTab[]>('lab/departments').subscribe({
      next: (departments) => {
        this.departments = Array.isArray(departments) ? departments : [];
        this.cdr.markForCheck();
      },
      error: () => {
        // Without counts the tabs are still usable, so fall back to the
        // four known service lines rather than leaving the strip empty.
        this.departments = ['Laboratory', 'Imaging', 'Ultrasound', 'Heart Station'].map(
          (name) => ({ name, count: 0 })
        );
        this.cdr.markForCheck();
      },
    });
  }

  loadOrders(): void {
    this.isLoading = true;
    this.apiService
      .get<LabOrder[]>('lab/orders', {
        department: this.activeDepartment,
        status: this.activeStage === 'All Orders' ? '' : this.activeStage,
        search: this.searchTerm,
      })
      .subscribe({
        next: (response) => {
          this.orders = Array.isArray(response) ? response : [];
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.orders = [];
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onDepartmentChange(department: string): void {
    if (this.activeDepartment === department) return;

    // A scan matched an order in the department it was found in; carrying the
    // chip across tabs would show an empty list under a filter that looks live.
    this.clearScanFilter();
    this.activeDepartment = department;
    this.activeStage = 'All Orders';
    this.pageIndex = 0;
    this.loadOrders();
  }

  onStageChange(stage: string): void {
    this.activeStage = stage;
    this.pageIndex = 0;
    this.loadOrders();
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.activeStage = 'All Orders';
    this.clearScanFilter();
    this.loadOrders();
  }

  newOrder(): void {
    this.router.navigate(['/app/visits/new']);
  }

  // ----------------------------------------------------------
  // Row actions
  // ----------------------------------------------------------

  /** Label for the first step: the lab draws a specimen, the others do not. */
  collectLabel(order: LabOrder): string {
    return order.department === 'Laboratory' ? 'Collect Specimen' : 'Start Study';
  }

  /** Label for the reading step, which is measurement in the lab and prose elsewhere. */
  encodeLabel(order: LabOrder): string {
    return order.resultKind === 'parameters' ? 'Encode Results' : 'Enter Findings';
  }

  collectSpecimen(order: LabOrder): void {
    const confirmed = window.confirm(
      order.department === 'Laboratory'
        ? 'Confirm specimen collection for this order?'
        : 'Mark this study as started?'
    );
    if (!confirmed) return;

    this.apiService.post<void>('lab/orders/' + order.id + '/collect', {}).subscribe({
      next: () => {
        this.loadOrders();
        this.loadDepartments();
        this.cdr.markForCheck();
      },
      error: () => {
        window.alert('Failed to update the order. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  openOrder(orderId: number): void {
    this.router.navigate(['/app/lab-results', orderId]);
  }

  openPatient(order: LabOrder, event: MouseEvent): void {
    event.stopPropagation();
    this.router.navigate(['/app/patients', order.patientId]);
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
   * Accepts a specimen barcode, an order code, or a patient QR. The list for
   * this department is already loaded, so matching happens here rather than
   * round-tripping.
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

    // A patient with no orders here is a different problem from an unknown
    // code, so say which one it is — and name the tab being searched.
    this.apiService.get<{ firstName: string; lastName: string }>(
      `patients/by-code/${encodeURIComponent(code)}`
    ).subscribe({
      next: (patient) => {
        this.scanError =
          `${patient.firstName} ${patient.lastName} has no ${this.activeDepartment} orders` +
          (this.activeStage === 'All Orders' ? '.' : ` under the "${this.activeStage}" filter.`);
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

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  departmentIcon(department: string): string {
    switch (department) {
      case 'Imaging':
        return 'scan';
      case 'Ultrasound':
        return 'waves';
      case 'Heart Station':
        return 'activity';
      default:
        return 'flask';
    }
  }

  stagePill(stage: string): string {
    switch (stage) {
      case 'Pending':
        return 'pill pill--warning';
      case 'In Progress':
        return 'pill pill--info';
      case 'For Reading':
        return 'pill pill--primary';
      case 'Completed':
        return 'pill pill--success';
      default:
        return 'pill';
    }
  }

  /** Two-letter monogram for the row avatar. */
  initials(name: string): string {
    return (name ?? '')
      .split(' ')
      .filter((part) => part.trim())
      .slice(0, 2)
      .map((part) => part.trim()[0])
      .join('')
      .toUpperCase();
  }

  countFor(department: string): number {
    return this.departments.find((d) => d.name === department)?.count ?? 0;
  }

  get hasFilters(): boolean {
    return !!this.searchTerm || this.activeStage !== 'All Orders' || !!this.scanFilter;
  }

  // ----------------------------------------------------------
  // Paging
  // ----------------------------------------------------------

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
   * lab/orders returns the whole filtered list, so pages are sliced here.
   * Refetching on every page change would return the same rows.
   */
  get pagedOrders(): LabOrder[] {
    const start = this.pageIndex * this.pageSize;
    return this.visibleOrders.slice(start, start + this.pageSize);
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

  get filteredCount(): number {
    return this.visibleOrders.length;
  }

  get rangeStart(): number {
    return this.filteredCount === 0 ? 0 : this.pageIndex * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.filteredCount);
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
    if (current > 3) pages.push(-1); // ellipsis marker

    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    if (current < total - 4) pages.push(-2); // ellipsis marker
    pages.push(total - 1);

    return pages;
  }
}
