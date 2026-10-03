import {
  ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api';
import { IconComponent } from '../icon/icon';

interface TestItem {
  id: number;
  code?: string;
  name: string;
  price: number;
}

interface TestCategory {
  id: number;
  name: string;
  tests: TestItem[];
}

interface PanelOption {
  id: number;
  code: string;
  name: string;
  description: string;
  testIds: number[];
  totalPrice: number;
}

/**
 * A reusable modal for ordering lab tests for one patient — the same searchable
 * picker and predefined panels the New Consultation screen uses, in a sheet that
 * can sit on any page without navigating away (so the host's breadcrumb, e.g.
 * the Cashier Desk, stays put).
 *
 * It routes through the visit path (POST visits), so an order here creates the
 * patient's lab orders and draws down the supplies each test consumes.
 */
@Component({
  selector: 'app-test-order-sheet',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './test-order-sheet.html',
  styleUrl: './test-order-sheet.scss',
})
export class TestOrderSheetComponent implements OnChanges {
  @Input() open = false;
  @Input() patientId: number | null = null;
  @Input() patientName = '';

  @Output() closed = new EventEmitter<void>();
  @Output() ordered = new EventEmitter<{ visitCode: string; count: number; total: number }>();

  categories: TestCategory[] = [];
  panels: PanelOption[] = [];
  selected = new Set<number>();
  testSearch = '';
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  private loaded = false;

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.selected.clear();
      this.testSearch = '';
      this.errorMessage = '';
      if (!this.loaded) this.loadCatalog();
    }
  }

  private loadCatalog(): void {
    this.isLoading = true;
    this.api.get<TestCategory[]>('lab/categories').subscribe({
      next: (c) => {
        this.categories = c;
        this.isLoading = false;
        this.loaded = true;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Could not load the test catalogue.';
        this.cdr.markForCheck();
      },
    });
    this.api.get<PanelOption[]>('lab/panels').subscribe({
      next: (p) => {
        this.panels = p;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  visibleTests(category: TestCategory): TestItem[] {
    const q = this.testSearch.trim().toLowerCase();
    if (!q) return category.tests;
    return category.tests.filter(
      (t) => t.name.toLowerCase().includes(q) || (t.code ?? '').toLowerCase().includes(q)
    );
  }

  get visibleCategories(): TestCategory[] {
    return this.categories.filter((c) => this.visibleTests(c).length > 0);
  }

  toggle(id: number): void {
    if (this.selected.has(id)) this.selected.delete(id);
    else this.selected.add(id);
  }

  isSelected(id: number): boolean {
    return this.selected.has(id);
  }

  applyPanel(panel: PanelOption): void {
    for (const id of panel.testIds) this.selected.add(id);
  }

  isPanelApplied(panel: PanelOption): boolean {
    return panel.testIds.length > 0 && panel.testIds.every((id) => this.selected.has(id));
  }

  private allTests(): TestItem[] {
    return this.categories.flatMap((c) => c.tests);
  }

  get selectedTests(): TestItem[] {
    return this.allTests().filter((t) => this.selected.has(t.id));
  }

  get total(): number {
    return this.selectedTests.reduce((sum, t) => sum + t.price, 0);
  }

  close(): void {
    this.closed.emit();
  }

  submit(): void {
    if (!this.patientId) return;
    if (this.selected.size === 0) {
      this.errorMessage = 'Select at least one test.';
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.api
      .post<{ visitCode?: string }>('visits', {
        patientId: this.patientId,
        referringPhysician: '',
        purposeOfVisit: 'Lab request',
        testIds: Array.from(this.selected),
      })
      .subscribe({
        next: (v) => {
          this.isSaving = false;
          this.ordered.emit({
            visitCode: v?.visitCode ?? '',
            count: this.selected.size,
            total: this.total,
          });
          this.closed.emit();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.isSaving = false;
          this.errorMessage = err?.error?.message ?? 'Could not order the tests.';
          this.cdr.markForCheck();
        },
      });
  }
}
