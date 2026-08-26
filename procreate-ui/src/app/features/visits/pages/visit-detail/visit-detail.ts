import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

interface VisitTest {
  testName: string;
  code: string;
  price: number;
}

interface TestItem {
  id: number;
  name: string;
  price: number;
}

interface TestCategory {
  id: number;
  name: string;
  tests: TestItem[];
}

interface LabOrder {
  id: number;
  status: string;
  specimenBarcode: string;
  collectedAt: string | null;
  processedAt: string | null;
  releasedAt: string | null;
}

interface VisitRecord {
  id: number;
  visitCode: string;
  patientId: number;
  patientName: string;
  patientCode: string;
  visitDate: string;
  referringPhysician: string;
  purpose: string;
  status: string;
  totalAmount: number;
  amountPaid: number;
  paymentStatus: string;
  paymentMethod: string;
  tests: VisitTest[];
  labOrders: LabOrder[];
}

/**
 * Read-only consultation record.
 *
 * The `:id` route previously rendered VisitForm, which ignores the parameter,
 * so "View" always showed an empty new-visit form.
 */
@Component({
  selector: 'app-visit-detail',
  standalone: false,
  templateUrl: './visit-detail.html',
  styleUrl: './visit-detail.scss',
})
export class VisitDetail implements OnInit {
  visit: VisitRecord | null = null;
  isLoading = true;
  errorMessage = '';

  // --- Add tests sheet ---
  isAddOpen = false;
  isSavingTests = false;
  addError = '';
  testCategories: TestCategory[] = [];
  isLoadingTests = false;
  selectedTestIds = new Set<number>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private apiService: ApiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!id) {
      this.errorMessage = 'No consultation was specified.';
      this.isLoading = false;
      return;
    }

    this.load(id);
  }

  private load(id: number): void {
    this.isLoading = true;

    this.apiService.get<VisitRecord>(`visits/${id}`).subscribe({
      next: (visit) => {
        this.visit = visit;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage =
          err?.status === 404
            ? 'That consultation no longer exists.'
            : 'Could not load the consultation.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  back(): void {
    this.router.navigate(['/app/visits']);
  }

  // ----------------------------------------------------------
  // Adding tests to an existing consultation
  // ----------------------------------------------------------

  get canEdit(): boolean {
    return !!this.visit && this.visit.status !== 'Cancelled';
  }

  /** Test codes already on the visit, so the picker can grey them out. */
  private get existingCodes(): Set<string> {
    return new Set((this.visit?.tests ?? []).map((t) => t.code));
  }

  isAlreadyOrdered(test: TestItem): boolean {
    // The visit payload exposes codes rather than ids, so match on name too —
    // a test is identified well enough by either for this purpose.
    return (this.visit?.tests ?? []).some((t) => t.testName === test.name);
  }

  openAddTests(): void {
    this.isAddOpen = true;
    this.addError = '';
    this.selectedTestIds.clear();

    if (this.testCategories.length === 0) this.loadTests();
  }

  closeAddTests(): void {
    this.isAddOpen = false;
    this.addError = '';
  }

  private loadTests(): void {
    this.isLoadingTests = true;

    this.apiService.get<TestCategory[]>('lab/categories').subscribe({
      next: (categories) => {
        this.testCategories = categories;
        this.isLoadingTests = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingTests = false;
        this.addError = 'Could not load the test catalogue.';
        this.cdr.markForCheck();
      },
    });
  }

  toggleTest(test: TestItem): void {
    if (this.isAlreadyOrdered(test)) return;

    if (this.selectedTestIds.has(test.id)) this.selectedTestIds.delete(test.id);
    else this.selectedTestIds.add(test.id);
  }

  isSelected(test: TestItem): boolean {
    return this.selectedTestIds.has(test.id);
  }

  get selectedTests(): TestItem[] {
    return this.testCategories
      .flatMap((c) => c.tests)
      .filter((t) => this.selectedTestIds.has(t.id));
  }

  get addedTotal(): number {
    return this.selectedTests.reduce((sum, t) => sum + t.price, 0);
  }

  saveTests(): void {
    if (!this.visit || this.selectedTestIds.size === 0) return;

    this.isSavingTests = true;
    this.addError = '';

    this.apiService
      .post(`visits/${this.visit.id}/tests`, { testIds: Array.from(this.selectedTestIds) })
      .subscribe({
        next: () => {
          this.isSavingTests = false;
          this.isAddOpen = false;
          // Reload so totals, payment status and lab orders all reflect the change.
          this.load(this.visit!.id);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.addError = err?.error?.message ?? 'Could not add those tests.';
          this.isSavingTests = false;
          this.cdr.markForCheck();
        },
      });
  }

  removeOrder(order: LabOrder): void {
    if (!this.visit) return;
    if (!window.confirm('Remove this test from the consultation?')) return;

    this.apiService.delete(`visits/${this.visit.id}/tests/${order.id}`).subscribe({
      next: () => {
        this.load(this.visit!.id);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Could not remove that test.';
        this.cdr.markForCheck();
      },
    });
  }

  /** Only an untouched order can be pulled back off the visit. */
  canRemoveOrder(order: LabOrder): boolean {
    return this.canEdit && order.status === 'Ordered' && !order.collectedAt;
  }

  get balance(): number {
    if (!this.visit) return 0;
    return this.visit.totalAmount - this.visit.amountPaid;
  }

  statusPill(status: string): string {
    switch (status) {
      case 'Completed': return 'pill pill--success';
      case 'In Progress': return 'pill pill--warning';
      case 'Cancelled': return 'pill pill--danger';
      case 'Registered': return 'pill pill--info';
      default: return 'pill pill--neutral';
    }
  }

  paymentPill(status: string): string {
    switch (status) {
      case 'Paid': return 'pill pill--success';
      case 'Partial': return 'pill pill--warning';
      default: return 'pill pill--danger';
    }
  }

  orderPill(status: string): string {
    switch (status) {
      case 'Released': return 'pill pill--success';
      case 'Processed': return 'pill pill--info';
      case 'Collected': return 'pill pill--warning';
      default: return 'pill pill--neutral';
    }
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0])
      .join('')
      .toLowerCase();
  }
}
