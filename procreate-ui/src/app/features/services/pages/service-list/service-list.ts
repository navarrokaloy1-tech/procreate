import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

export interface RequiredItem {
  inventoryItemId: number;
  quantity: number;
  name: string;
  unit: string;
}

export interface Service {
  id: number;
  code: string;
  name: string;
  category: string;
  price: number;
  description: string;
  seniorPwdDiscount: boolean;
  philHealthCovered: boolean;
  isActive: boolean;
  requiredItems: RequiredItem[];
}

interface InventoryOption {
  id: number;
  name: string;
  unitOfMeasure: string;
  brandName: string;
}

@Component({
  selector: 'app-service-list',
  standalone: false,
  templateUrl: './service-list.html',
  styleUrl: './service-list.scss',
})
export class ServiceListComponent implements OnInit, OnDestroy {
  services: Service[] = [];
  categories: string[] = [];
  totalCount = 0;

  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [10, 25, 50];

  searchTerm = '';
  categoryFilter = '';
  statusFilter = '';

  isLoading = false;
  errorMessage = '';

  // --- Add / edit sheet ---
  isModalOpen = false;
  isSaving = false;
  modalError = '';
  editingId: number | null = null;

  form = this.blankForm();

  /** Reagents and consumables the service draws down. */
  requiredItems: RequiredItem[] = [];
  inventoryQuery = '';
  inventoryResults: InventoryOption[] = [];

  private searchSubject = new Subject<string>();
  private inventorySearch = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.load();
      })
    );

    this.subscriptions.add(
      this.inventorySearch
        .pipe(debounceTime(250), distinctUntilChanged())
        .subscribe((term) => this.runInventorySearch(term))
    );

    this.loadCategories();
    this.load();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private blankForm() {
    return {
      name: '',
      code: '',
      category: '',
      price: null as number | null,
      description: '',
      seniorPwdDiscount: true,
      philHealthCovered: false,
      isActive: true,
    };
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  loadCategories(): void {
    this.api.get<string[]>('services/categories').subscribe({
      next: (categories) => {
        this.categories = categories;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: Service[] }>('services', {
        search: this.searchTerm,
        category: this.categoryFilter,
        status: this.statusFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.services = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load the service catalogue.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onCategoryFilter(category: string): void {
    this.categoryFilter = category;
    this.pageIndex = 0;
    this.load();
  }

  onStatusFilter(status: string): void {
    this.statusFilter = status;
    this.pageIndex = 0;
    this.load();
  }

  onPageSizeChange(size: string | number): void {
    const parsed = Number(size);
    if (!Number.isFinite(parsed) || parsed < 1) return;

    this.pageSize = Math.floor(parsed);
    this.pageIndex = 0;
    this.load();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.load();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.categoryFilter = '';
    this.statusFilter = '';
    this.pageIndex = 0;
    this.load();
  }

  // ----------------------------------------------------------
  // Add / edit
  // ----------------------------------------------------------

  openAdd(): void {
    this.editingId = null;
    this.form = this.blankForm();
    this.form.category = this.categories[0] ?? '';
    this.requiredItems = [];
    this.resetInventoryPicker();
    this.modalError = '';
    this.isModalOpen = true;
  }

  openEdit(service: Service): void {
    this.editingId = service.id;
    this.form = {
      name: service.name,
      code: service.code,
      category: service.category,
      price: service.price,
      description: service.description,
      seniorPwdDiscount: service.seniorPwdDiscount,
      philHealthCovered: service.philHealthCovered,
      isActive: service.isActive,
    };
    this.requiredItems = service.requiredItems.map((r) => ({ ...r }));
    this.resetInventoryPicker();
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
    this.isSaving = false;
  }

  save(): void {
    if (!this.form.name.trim()) {
      this.modalError = 'Enter a service name.';
      return;
    }
    if (!this.form.category) {
      this.modalError = 'Choose a category.';
      return;
    }
    if (this.form.price === null || this.form.price < 0) {
      this.modalError = 'Enter a fee of zero or more.';
      return;
    }

    const payload = {
      ...this.form,
      price: Number(this.form.price),
      requiredItems: this.requiredItems.map((r) => ({
        inventoryItemId: r.inventoryItemId,
        quantity: r.quantity,
      })),
    };

    this.isSaving = true;
    const request = this.editingId
      ? this.api.put<Service>(`services/${this.editingId}`, payload)
      : this.api.post<Service>('services', payload);

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeModal();
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.modalError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  remove(service: Service): void {
    const confirmed = window.confirm(`Delete "${service.name}"? This cannot be undone.`);
    if (!confirmed) return;

    this.api.delete<{ message?: string }>(`services/${service.id}`).subscribe({
      next: (res) => {
        // A service with past orders is retired rather than removed, so say so
        // instead of leaving a still-listed row looking like a failed delete.
        if (res?.message) window.alert(res.message);
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        window.alert(err?.error?.message ?? 'Could not delete the service.');
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Required inventory items
  // ----------------------------------------------------------

  onInventoryQuery(term: string): void {
    this.inventoryQuery = term;
    this.inventorySearch.next(term);
  }

  private runInventorySearch(term: string): void {
    if (!term.trim()) {
      this.inventoryResults = [];
      this.cdr.markForCheck();
      return;
    }

    this.api
      .get<{ data: InventoryOption[] }>('inventory/items', { search: term, pageSize: 8 })
      .subscribe({
        next: (res) => {
          // Anything already on the recipe is filtered out; adding it twice
          // only ever means "use more", which the quantity field covers.
          const chosen = new Set(this.requiredItems.map((r) => r.inventoryItemId));
          this.inventoryResults = res.data.filter((i) => !chosen.has(i.id));
          this.cdr.markForCheck();
        },
        error: () => {
          this.inventoryResults = [];
          this.cdr.markForCheck();
        },
      });
  }

  addRequiredItem(item: InventoryOption): void {
    this.requiredItems.push({
      inventoryItemId: item.id,
      quantity: 1,
      name: item.name,
      unit: item.unitOfMeasure,
    });
    this.resetInventoryPicker();
  }

  removeRequiredItem(index: number): void {
    this.requiredItems.splice(index, 1);
  }

  onQuantityChange(item: RequiredItem, value: string): void {
    const parsed = Number(value);
    item.quantity = Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : 1;
  }

  private resetInventoryPicker(): void {
    this.inventoryQuery = '';
    this.inventoryResults = [];
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  categoryOptions(includeAll: boolean): string[] {
    return includeAll ? ['', ...this.categories] : this.categories;
  }

  get hasFilters(): boolean {
    return !!this.searchTerm || !!this.categoryFilter || !!this.statusFilter;
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : this.pageIndex * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min((this.pageIndex + 1) * this.pageSize, this.totalCount);
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get pages(): number[] {
    const total = this.totalPages;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i);

    const current = this.pageIndex;
    const pages: number[] = [0];
    if (current > 3) pages.push(-1);

    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    for (let i = start; i <= end; i++) pages.push(i);

    if (current < total - 4) pages.push(-2);
    pages.push(total - 1);
    return pages;
  }
}
