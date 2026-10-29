import { Component, OnInit, OnDestroy, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';
import { ConfirmService } from '../../../../core/services/confirm';
import { ToastService } from '../../../../core/services/toast';

export interface RequiredItem {
  inventoryItemId: number;
  quantity: number;
  name: string;
  unit: string;
}

/** One measured line on the service's result sheet. */
export interface ServiceParameter {
  name: string;
  unit: string;
  referenceRange: string;
  normalMin: string | null;
  normalMax: string | null;
}

export interface Service {
  id: number;
  code: string;
  name: string;
  category: string;
  price: number;
  rebateAmount: number;
  description: string;
  seniorPwdDiscount: boolean;
  philHealthCovered: boolean;
  isActive: boolean;
  requiredItems: RequiredItem[];
  parameters: ServiceParameter[];
  /** True once the service also exists as a test staff can order. */
  orderable: boolean;
}

interface InventoryOption {
  id: number;
  name: string;
  unitOfMeasure: string;
  brandName: string;
}

/** A category, and whether a service in it can be ordered or is only billed. */
interface CategoryOption {
  name: string;
  orderable: boolean;
  department: string | null;
}

@Component({
  selector: 'app-service-list',
  standalone: false,
  templateUrl: './service-list.html',
  styleUrl: './service-list.scss',
})
export class ServiceListComponent implements OnInit, OnDestroy {
  services: Service[] = [];
  categories: CategoryOption[] = [];
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
  /** Which field failed validation, so its input can be marked red. */
  invalidField: 'name' | 'category' | 'price' | 'parameters' | null = null;
  editingId: number | null = null;

  /** The scrolling modal body, so an error can bring its banner back into view. */
  @ViewChild('modalBody') modalBody?: ElementRef<HTMLElement>;

  form = this.blankForm();

  /** Reagents and consumables the service draws down. */
  requiredItems: RequiredItem[] = [];
  inventoryQuery = '';
  inventoryResults: InventoryOption[] = [];

  /** The result-sheet lines reported for the service. */
  parameters: ServiceParameter[] = [];

  private searchSubject = new Subject<string>();
  private inventorySearch = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef,
    private confirm: ConfirmService,
    private toast: ToastService
  ) {}

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
      rebateAmount: null as number | null,
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
    this.api.get<CategoryOption[]>('services/categories').subscribe({
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
    this.form.category = this.categories[0]?.name ?? '';
    this.requiredItems = [];
    this.parameters = [];
    this.resetInventoryPicker();
    this.modalError = '';
    this.invalidField = null;
    this.isModalOpen = true;
  }

  openEdit(service: Service): void {
    this.editingId = service.id;
    this.form = {
      name: service.name,
      code: service.code,
      category: service.category,
      price: service.price,
      rebateAmount: service.rebateAmount,
      description: service.description,
      seniorPwdDiscount: service.seniorPwdDiscount,
      philHealthCovered: service.philHealthCovered,
      isActive: service.isActive,
    };
    this.requiredItems = service.requiredItems.map((r) => ({ ...r }));
    this.parameters = (service.parameters ?? []).map((p) => ({ ...p }));
    this.resetInventoryPicker();
    this.modalError = '';
    this.invalidField = null;
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
    this.invalidField = null;
    this.isSaving = false;
  }

  /**
   * Shows a validation error: records which field is at fault (so its input goes
   * red), sets the banner message, and scrolls the sheet back to the top where
   * the banner sits — otherwise an error fired from the footer button stays off
   * screen below the fold.
   */
  private failValidation(field: 'name' | 'category' | 'price' | 'parameters', message: string): void {
    this.invalidField = field;
    this.modalError = message;
    this.cdr.markForCheck();
    this.scrollModalToTop();
  }

  /** Brings the modal body back to the top, where the error banner is shown. */
  private scrollModalToTop(): void {
    setTimeout(() => this.modalBody?.nativeElement.scrollTo({ top: 0, behavior: 'smooth' }));
  }

  save(): void {
    this.invalidField = null;

    if (!this.form.name.trim()) {
      this.failValidation('name', 'Enter a service name.');
      return;
    }
    if (!this.form.category) {
      this.failValidation('category', 'Choose a category.');
      return;
    }
    if (this.form.price === null || this.form.price < 0) {
      this.failValidation('price', 'Enter a fee of zero or more.');
      return;
    }
    if (this.parameters.some((p) => !p.name.trim())) {
      this.failValidation('parameters', 'Name every parameter, or remove the blank rows.');
      return;
    }

    const payload = {
      ...this.form,
      price: Number(this.form.price),
      rebateAmount: Number(this.form.rebateAmount ?? 0),
      requiredItems: this.requiredItems.map((r) => ({
        inventoryItemId: r.inventoryItemId,
        quantity: r.quantity,
      })),
      parameters: this.parameters.map((p) => ({
        name: p.name.trim(),
        unit: p.unit.trim(),
        referenceRange: p.referenceRange.trim(),
        normalMin: p.normalMin,
        normalMax: p.normalMax,
      })),
    };

    this.isSaving = true;
    const editing = !!this.editingId;
    const request = this.editingId
      ? this.api.put<Service>(`services/${this.editingId}`, payload)
      : this.api.post<Service>('services', payload);

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeModal();
        this.toast.success(`Service "${payload.name}" ${editing ? 'updated' : 'added'}.`);
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.modalError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.scrollModalToTop();
        this.cdr.markForCheck();
      },
    });
  }

  async remove(service: Service): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Delete service',
      message: `Delete "${service.name}"?`,
      detail: 'This cannot be undone.',
      confirmLabel: 'Delete',
    });
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
  // Result parameters
  // ----------------------------------------------------------

  addParameter(): void {
    this.parameters.push({ name: '', unit: '', referenceRange: '', normalMin: null, normalMax: null });
  }

  removeParameter(index: number): void {
    this.parameters.splice(index, 1);
  }

  moveParameter(index: number, offset: number): void {
    const target = index + offset;
    if (target < 0 || target >= this.parameters.length) return;

    const [row] = this.parameters.splice(index, 1);
    this.parameters.splice(target, 0, row);
  }

  /**
   * Keeps the numeric bounds in step with the typed range, so a value entered
   * at result time can still be flagged high or low. Anything that is not a
   * plain "low-high" ("Negative", say) just clears them.
   */
  onReferenceRangeChange(parameter: ServiceParameter, value: string): void {
    parameter.referenceRange = value;

    const match = value.match(/^\s*(-?\d+(?:\.\d+)?)\s*[-\u2013to]+\s*(-?\d+(?:\.\d+)?)\s*$/i);
    parameter.normalMin = match ? match[1] : null;
    parameter.normalMax = match ? match[2] : null;
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  categoryOptions(includeAll: boolean): string[] {
    return includeAll ? ['', ...this.categories.map((c) => c.name)] : this.categories.map((c) => c.name);
  }

  /** Whether a service in the chosen category also becomes an orderable test. */
  get isCategoryOrderable(): boolean {
    return this.categories.find((c) => c.name === this.form.category)?.orderable ?? false;
  }

  get categoryDepartment(): string {
    return this.categories.find((c) => c.name === this.form.category)?.department ?? '';
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
