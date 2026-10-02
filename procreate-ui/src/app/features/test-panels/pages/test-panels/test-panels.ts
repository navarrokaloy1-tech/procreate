import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

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

interface Panel {
  id: number;
  code: string;
  name: string;
  description: string;
  isActive: boolean;
  testIds: number[];
  tests: { id: number; code: string; name: string; price: number }[];
  totalPrice: number;
}

@Component({
  selector: 'app-test-panels',
  standalone: false,
  templateUrl: './test-panels.html',
  styleUrl: './test-panels.scss',
})
export class TestPanelsComponent implements OnInit {
  panels: Panel[] = [];
  testCategories: TestCategory[] = [];
  isLoading = false;
  errorMessage = '';

  isModalOpen = false;
  isSaving = false;
  modalError = '';
  editingId: number | null = null;
  form = { code: '', name: '', description: '', isActive: true };
  selectedTestIds = new Set<number>();
  testSearch = '';

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadPanels();
    this.loadTests();
  }

  loadPanels(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.api.get<Panel[]>('lab/panels', { includeInactive: true }).subscribe({
      next: (panels) => {
        this.panels = panels;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not load panels.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  loadTests(): void {
    this.api.get<TestCategory[]>('lab/categories').subscribe({
      next: (categories) => {
        this.testCategories = categories;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  // ----------------------------------------------------------
  // Create / edit
  // ----------------------------------------------------------

  openAdd(): void {
    this.editingId = null;
    this.form = { code: '', name: '', description: '', isActive: true };
    this.selectedTestIds.clear();
    this.testSearch = '';
    this.modalError = '';
    this.isModalOpen = true;
  }

  openEdit(panel: Panel): void {
    this.editingId = panel.id;
    this.form = {
      code: panel.code,
      name: panel.name,
      description: panel.description,
      isActive: panel.isActive,
    };
    this.selectedTestIds = new Set(panel.testIds);
    this.testSearch = '';
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
    this.isSaving = false;
  }

  toggleTest(test: TestItem): void {
    if (this.selectedTestIds.has(test.id)) this.selectedTestIds.delete(test.id);
    else this.selectedTestIds.add(test.id);
  }

  isSelected(test: TestItem): boolean {
    return this.selectedTestIds.has(test.id);
  }

  visibleTests(category: TestCategory): TestItem[] {
    const q = this.testSearch.trim().toLowerCase();
    if (!q) return category.tests;
    return category.tests.filter(
      (t) => t.name.toLowerCase().includes(q) || (t.code ?? '').toLowerCase().includes(q)
    );
  }

  get visibleCategories(): TestCategory[] {
    return this.testCategories.filter((c) => this.visibleTests(c).length > 0);
  }

  private allTests(): TestItem[] {
    return this.testCategories.flatMap((c) => c.tests);
  }

  get selectedTotal(): number {
    return this.allTests()
      .filter((t) => this.selectedTestIds.has(t.id))
      .reduce((sum, t) => sum + t.price, 0);
  }

  save(): void {
    if (!this.form.name.trim()) {
      this.modalError = 'Enter a panel name.';
      return;
    }
    if (this.selectedTestIds.size === 0) {
      this.modalError = 'Pick at least one test.';
      return;
    }

    const payload = {
      code: this.form.code.trim(),
      name: this.form.name.trim(),
      description: this.form.description.trim(),
      isActive: this.form.isActive,
      testIds: Array.from(this.selectedTestIds),
    };

    this.isSaving = true;
    const request: Observable<any> = this.editingId
      ? this.api.put(`lab/panels/${this.editingId}`, payload)
      : this.api.post('lab/panels', payload);

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeModal();
        this.loadPanels();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.modalError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  remove(panel: Panel): void {
    if (!window.confirm(`Delete the "${panel.name}" panel? This cannot be undone.`)) return;

    this.api.delete(`lab/panels/${panel.id}`).subscribe({
      next: () => {
        this.loadPanels();
        this.cdr.markForCheck();
      },
      error: (err) => {
        window.alert(err?.error?.message ?? 'Could not delete the panel.');
        this.cdr.markForCheck();
      },
    });
  }
}
