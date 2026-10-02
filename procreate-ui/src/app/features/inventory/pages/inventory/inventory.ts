import { Component, OnInit, OnDestroy, ChangeDetectorRef, HostListener } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

export interface InventoryItem {
  id: number;
  itemType: string;
  categoryId: number | null;
  categoryName: string;
  name: string;
  brandName: string;
  dosage: string;
  supplierId: number | null;
  supplierName: string;
  unitOfMeasure: string;
  subUnit: string;
  conversionFactor: number | null;
  sku: string;
  costPrice: number;
  sellingPrice: number;
  currentStock: number;
  reorderLevel: number;
  minOrderQty: number;
  description: string;
  isActive: boolean;
  stockState: string;
}

interface BriefItem {
  id: number;
  name: string;
  brandName: string;
  currentStock: number;
  reorderLevel: number;
  unitOfMeasure: string;
  stockState: string;
}

interface Summary {
  totalItems: number;
  lowStock: number;
  outOfStock: number;
  byType: { type: string; count: number }[];
  outOfStockItems: BriefItem[];
  lowStockItems: BriefItem[];
}

export interface Category {
  id: number;
  name: string;
  description: string;
  itemCount: number;
}

export interface Supplier {
  id: number;
  name: string;
  contactPerson: string;
  contactNumber: string;
  email: string;
  address: string;
  itemCount: number;
}

export interface Batch {
  id: number;
  itemId: number;
  itemName: string;
  unit: string;
  batchNumber: string;
  expiryDate: string | null;
  quantityReceived: number;
  quantityRemaining: number;
  receivedAt: string;
  daysToExpiry: number | null;
  status: string; // Expired | Expiring | OK
}

export interface Movement {
  id: number;
  inventoryItemId: number;
  itemName: string;
  unit: string;
  movementType: string;
  quantityChange: number;
  balanceAfter: number;
  reason: string;
  reference: string;
  performedBy: string;
  createdAt: string;
}

interface ExpiryAlert {
  id: number;
  itemId: number;
  itemName: string;
  batchNumber: string;
  expiryDate: string | null;
  quantityRemaining: number;
  unit: string;
  daysToExpiry: number | null;
}

interface AlertsReport {
  expiryDays: number;
  counts: { expired: number; expiringSoon: number; lowStock: number; outOfStock: number; total: number };
  expired: ExpiryAlert[];
  expiringSoon: ExpiryAlert[];
  lowStock: BriefItem[];
  outOfStock: BriefItem[];
}

/** Which sheet is open, if any. */
type Sheet = 'item' | 'category' | 'supplier' | 'suppliers' | 'stock' | null;

/** The three ways stock moves by hand. */
type StockMode = 'in' | 'out' | 'adjust';

@Component({
  selector: 'app-inventory',
  standalone: false,
  templateUrl: './inventory.html',
  styleUrl: './inventory.scss',
})
export class InventoryComponent implements OnInit, OnDestroy {
  readonly tabs = ['Dashboard', 'Items', 'Stock Batches', 'Transactions'];
  activeTab = 'Dashboard';

  summary: Summary | null = null;
  alerts: AlertsReport | null = null;
  items: InventoryItem[] = [];
  categories: Category[] = [];
  suppliers: Supplier[] = [];
  batches: Batch[] = [];
  movements: Movement[] = [];
  movementTypeFilter = '';
  readonly movementTypes = ['Stock In', 'Stock Out', 'Adjustment', 'Auto-Deduction'];
  isLoadingBatches = false;
  isLoadingMovements = false;
  totalCount = 0;

  readonly itemTypes = ['Medicine', 'Consumable', 'Asset'];
  readonly stockStates = ['In Stock', 'Low Stock', 'Out of Stock'];
  readonly units = ['Piece', 'Tablet', 'Capsule', 'Bottle', 'Box', 'Pack', 'Vial', 'Ampoule', 'Set'];

  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [10, 25, 50];

  searchTerm = '';
  typeFilter = '';
  categoryFilter = '';
  stockFilter = '';

  isLoading = false;
  errorMessage = '';

  /** The Manage menu in the banner. */
  isManageOpen = false;

  /**
   * Viewport coordinates for the Manage menu, which is fixed and lives outside
   * the banner: inside it the banner clipped it and the tab strip painted over
   * it. `left` is the button's right edge and the menu is pulled back by its
   * own width in CSS, so this never has to read the window size — which is not
   * always meaningful (an offscreen or zero-size viewport reports 0).
   */
  menuPos = { top: 0, left: 0 };

  // --- Sheets ---
  sheet: Sheet = null;
  isSaving = false;
  sheetError = '';
  editingId: number | null = null;

  form = this.blankItem();
  categoryForm = { name: '', description: '' };
  supplierForm = { name: '', contactPerson: '', contactNumber: '', email: '', address: '' };
  stockForm = this.blankStock();

  private searchSubject = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.subscriptions.add(
      this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.loadItems();
      })
    );

    this.loadSummary();
    this.loadAlerts();
    this.loadCategories();
    this.loadSuppliers();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private blankStock() {
    return {
      item: null as InventoryItem | null,
      mode: 'in' as StockMode,
      quantity: null as number | null,
      delta: null as number | null,
      reason: '',
      reference: '',
      batchNumber: '',
      expiryDate: '',
    };
  }

  private blankItem() {
    return {
      itemType: 'Medicine',
      categoryId: null as number | null,
      name: '',
      brandName: '',
      dosage: '',
      supplierId: null as number | null,
      unitOfMeasure: 'Piece',
      subUnit: '',
      conversionFactor: null as number | null,
      sku: '',
      costPrice: null as number | null,
      sellingPrice: null as number | null,
      currentStock: 0,
      reorderLevel: 10,
      minOrderQty: 1,
      description: '',
      isActive: true,
    };
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  onTabChange(tab: string): void {
    this.activeTab = tab;
    this.isManageOpen = false;

    if (tab === 'Items' && this.items.length === 0) this.loadItems();
    if (tab === 'Dashboard') { this.loadSummary(); this.loadAlerts(); }
    if (tab === 'Stock Batches') this.loadBatches();
    if (tab === 'Transactions') this.loadMovements();
  }

  loadSummary(): void {
    this.api.get<Summary>('inventory/summary').subscribe({
      next: (summary) => {
        this.summary = summary;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not load the inventory summary.';
        this.cdr.markForCheck();
      },
    });
  }

  loadAlerts(): void {
    this.api.get<AlertsReport>('inventory/alerts').subscribe({
      next: (alerts) => {
        this.alerts = alerts;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  loadBatches(): void {
    this.isLoadingBatches = true;
    this.api.get<Batch[]>('inventory/batches').subscribe({
      next: (batches) => {
        this.batches = batches;
        this.isLoadingBatches = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingBatches = false;
        this.cdr.markForCheck();
      },
    });
  }

  loadMovements(): void {
    this.isLoadingMovements = true;
    this.api
      .get<{ data: Movement[] }>('inventory/movements', {
        movementType: this.movementTypeFilter,
        pageSize: 100,
      })
      .subscribe({
        next: (res) => {
          this.movements = res.data;
          this.isLoadingMovements = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.isLoadingMovements = false;
          this.cdr.markForCheck();
        },
      });
  }

  onMovementTypeChange(): void {
    this.loadMovements();
  }

  loadItems(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: InventoryItem[] }>('inventory/items', {
        search: this.searchTerm,
        itemType: this.typeFilter,
        categoryId: this.categoryFilter,
        stock: this.stockFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.items = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load inventory items.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  loadCategories(): void {
    this.api.get<Category[]>('inventory/categories').subscribe({
      next: (categories) => {
        this.categories = categories;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  loadSuppliers(): void {
    this.api.get<Supplier[]>('inventory/suppliers').subscribe({
      next: (suppliers) => {
        this.suppliers = suppliers;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onFilterChange(): void {
    this.pageIndex = 0;
    this.loadItems();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.typeFilter = '';
    this.categoryFilter = '';
    this.stockFilter = '';
    this.pageIndex = 0;
    this.loadItems();
  }

  onPageSizeChange(size: string | number): void {
    const parsed = Number(size);
    if (!Number.isFinite(parsed) || parsed < 1) return;

    this.pageSize = Math.floor(parsed);
    this.pageIndex = 0;
    this.loadItems();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.loadItems();
  }

  /** The whole catalogue, unfiltered — from the Total Items tile. */
  showAllItems(): void {
    this.clearFilters();
    this.activeTab = 'Items';
  }

  /** Who the clinic buys from, from the Suppliers tile. */
  openSuppliers(): void {
    this.isManageOpen = false;
    this.sheetError = '';
    this.sheet = 'suppliers';
  }

  /** Jumps to the Items tab filtered to one item type, from the Manage menu. */
  showTypeFilter(itemType: string): void {
    this.clearFilters();
    this.typeFilter = itemType;
    this.activeTab = 'Items';
    this.isManageOpen = false;
    this.loadItems();
  }

  /** Jumps to the Items tab already filtered, so an alert leads somewhere. */
  showStockState(state: string): void {
    this.clearFilters();
    this.stockFilter = state;
    this.activeTab = 'Items';
    this.loadItems();
  }

  // ----------------------------------------------------------
  // Manage menu
  // ----------------------------------------------------------

  toggleManage(event: MouseEvent): void {
    if (this.isManageOpen) {
      this.isManageOpen = false;
      return;
    }

    const button = event.currentTarget as HTMLElement;
    const rect = button.getBoundingClientRect();
    this.menuPos = {
      top: Math.round(rect.bottom + 8),
      left: Math.round(rect.right),
    };
    this.isManageOpen = true;
  }

  /**
   * A fixed menu does not travel with its button, so it is dismissed rather
   * than left floating somewhere it no longer belongs.
   */
  @HostListener('window:scroll')
  @HostListener('window:resize')
  closeManage(): void {
    if (this.isManageOpen) {
      this.isManageOpen = false;
      this.cdr.markForCheck();
    }
  }

  // ----------------------------------------------------------
  // Item sheet
  // ----------------------------------------------------------

  openAddItem(): void {
    this.editingId = null;
    this.form = this.blankItem();
    this.isManageOpen = false;
    this.sheetError = '';
    this.sheet = 'item';
  }

  openEditItem(item: InventoryItem): void {
    this.editingId = item.id;
    this.form = {
      itemType: item.itemType,
      categoryId: item.categoryId,
      name: item.name,
      brandName: item.brandName,
      dosage: item.dosage,
      supplierId: item.supplierId,
      unitOfMeasure: item.unitOfMeasure,
      subUnit: item.subUnit,
      conversionFactor: item.conversionFactor,
      sku: item.sku,
      costPrice: item.costPrice,
      sellingPrice: item.sellingPrice,
      currentStock: item.currentStock,
      reorderLevel: item.reorderLevel,
      minOrderQty: item.minOrderQty,
      description: item.description,
      isActive: item.isActive,
    };
    this.sheetError = '';
    this.sheet = 'item';
  }

  saveItem(): void {
    if (!this.form.name.trim()) {
      this.sheetError = this.form.itemType === 'Medicine'
        ? 'Enter a generic name.'
        : 'Enter an item name.';
      return;
    }
    if (this.form.subUnit.trim() && !this.form.conversionFactor) {
      this.sheetError = 'Set how many sub-units make up one unit.';
      return;
    }

    const payload = {
      ...this.form,
      costPrice: Number(this.form.costPrice ?? 0),
      sellingPrice: Number(this.form.sellingPrice ?? 0),
      currentStock: Number(this.form.currentStock ?? 0),
      reorderLevel: Number(this.form.reorderLevel ?? 0),
      minOrderQty: Number(this.form.minOrderQty ?? 1),
      conversionFactor: this.form.subUnit.trim() ? Number(this.form.conversionFactor) : null,
    };

    this.isSaving = true;
    const request = this.editingId
      ? this.api.put<InventoryItem>(`inventory/items/${this.editingId}`, payload)
      : this.api.post<InventoryItem>('inventory/items', payload);

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeSheet();
        this.refreshAll();
      },
      error: (err) => {
        this.isSaving = false;
        this.sheetError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  removeItem(item: InventoryItem): void {
    const confirmed = window.confirm(`Delete "${item.name}"? This cannot be undone.`);
    if (!confirmed) return;

    this.api.delete<{ message?: string }>(`inventory/items/${item.id}`).subscribe({
      next: (res) => {
        // An item a service depends on is deactivated instead, so say so
        // rather than leaving a still-listed row looking like a failure.
        if (res?.message) window.alert(res.message);
        this.refreshAll();
      },
      error: (err) => {
        window.alert(err?.error?.message ?? 'Could not delete the item.');
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Stock adjustment
  // ----------------------------------------------------------

  openStock(item: InventoryItem, mode: StockMode = 'in'): void {
    this.stockForm = { ...this.blankStock(), item, mode };
    this.sheetError = '';
    this.sheet = 'stock';
  }

  setStockMode(mode: StockMode): void {
    this.stockForm.mode = mode;
    this.sheetError = '';
  }

  get stockModeLabel(): string {
    switch (this.stockForm.mode) {
      case 'in': return 'Stock In';
      case 'out': return 'Stock Out';
      default: return 'Adjust';
    }
  }

  saveStock(): void {
    const item = this.stockForm.item;
    if (!item) return;

    if (this.stockForm.mode === 'adjust') {
      const delta = Math.trunc(Number(this.stockForm.delta));
      if (!Number.isFinite(delta) || delta === 0) {
        this.sheetError = 'Enter how many units to add or remove.';
        return;
      }
      this.submitStock(`inventory/items/${item.id}/stock`, { delta, reason: this.stockForm.reason });
      return;
    }

    const quantity = Math.trunc(Number(this.stockForm.quantity));
    if (!Number.isFinite(quantity) || quantity <= 0) {
      this.sheetError = 'Enter how many units.';
      return;
    }

    if (this.stockForm.mode === 'in') {
      this.submitStock(`inventory/items/${item.id}/stock-in`, {
        quantity,
        reason: this.stockForm.reason,
        batchNumber: this.stockForm.batchNumber,
        expiryDate: this.stockForm.expiryDate || null,
      });
    } else {
      this.submitStock(`inventory/items/${item.id}/stock-out`, {
        quantity,
        reason: this.stockForm.reason,
        reference: this.stockForm.reference,
      });
    }
  }

  private submitStock(path: string, body: any): void {
    this.isSaving = true;
    this.api.post<InventoryItem>(path, body).subscribe({
      next: () => {
        this.isSaving = false;
        this.closeSheet();
        this.refreshAll();
      },
      error: (err) => {
        this.isSaving = false;
        this.sheetError = err?.error?.message ?? 'The stock update failed.';
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Category and supplier sheets
  // ----------------------------------------------------------

  openAddCategory(): void {
    this.categoryForm = { name: '', description: '' };
    this.isManageOpen = false;
    this.sheetError = '';
    this.sheet = 'category';
  }

  saveCategory(): void {
    if (!this.categoryForm.name.trim()) {
      this.sheetError = 'Enter a category name.';
      return;
    }

    this.isSaving = true;
    this.api.post<Category>('inventory/categories', this.categoryForm).subscribe({
      next: () => {
        this.isSaving = false;
        this.closeSheet();
        this.loadCategories();
      },
      error: (err) => {
        this.isSaving = false;
        this.sheetError = err?.error?.message ?? 'Saving failed.';
        this.cdr.markForCheck();
      },
    });
  }

  openAddSupplier(): void {
    this.supplierForm = { name: '', contactPerson: '', contactNumber: '', email: '', address: '' };
    this.isManageOpen = false;
    this.sheetError = '';
    this.sheet = 'supplier';
  }

  saveSupplier(): void {
    if (!this.supplierForm.name.trim()) {
      this.sheetError = 'Enter a supplier name.';
      return;
    }

    this.isSaving = true;
    this.api.post<Supplier>('inventory/suppliers', this.supplierForm).subscribe({
      next: () => {
        this.isSaving = false;
        this.closeSheet();
        this.loadSuppliers();
      },
      error: (err) => {
        this.isSaving = false;
        this.sheetError = err?.error?.message ?? 'Saving failed.';
        this.cdr.markForCheck();
      },
    });
  }

  closeSheet(): void {
    this.sheet = null;
    this.sheetError = '';
    this.isSaving = false;
  }

  private refreshAll(): void {
    this.loadItems();
    this.loadSummary();
    this.loadAlerts();
    if (this.activeTab === 'Stock Batches') this.loadBatches();
    if (this.activeTab === 'Transactions') this.loadMovements();
    this.cdr.markForCheck();
  }

  /** Jumps to the Batches tab, e.g. from an expiry alert. */
  showBatches(): void {
    this.isManageOpen = false;
    this.activeTab = 'Stock Batches';
    this.loadBatches();
  }

  movementClass(type: string): string {
    switch (type) {
      case 'Stock In': return 'tag tag--primary';
      case 'Stock Out': return 'tag tag--warning';
      case 'Auto-Deduction': return 'tag tag--info';
      default: return 'tag';
    }
  }

  batchStatusClass(status: string): string {
    switch (status) {
      case 'Expired': return 'pill pill--danger';
      case 'Expiring': return 'pill pill--warning';
      default: return 'pill pill--success';
    }
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  /** A medicine has a generic and a brand; everything else just has a name. */
  get nameLabel(): string {
    return this.form.itemType === 'Medicine' ? 'Generic Name' : 'Item Name';
  }

  get brandLabel(): string {
    return this.form.itemType === 'Medicine' ? 'Brand Name' : 'Brand/Manufacturer';
  }

  get conversionHint(): string {
    const unit = this.form.unitOfMeasure || 'unit';
    const sub = this.form.subUnit.trim();
    return sub ? `1 ${unit} = ? ${sub}` : 'Set sub-unit first';
  }

  typeTag(itemType: string): string {
    switch (itemType) {
      case 'Medicine':
        return 'tag tag--info';
      case 'Consumable':
        return 'tag tag--primary';
      default:
        return 'tag tag--warning';
    }
  }

  stockClass(item: InventoryItem | BriefItem): string {
    switch (item.stockState) {
      case 'Out of Stock':
        return 'stock-figure stock-figure--out';
      case 'Low Stock':
        return 'stock-figure stock-figure--low';
      default:
        return 'stock-figure';
    }
  }

  countFor(type: string): number {
    return this.summary?.byType.find((t) => t.type === type)?.count ?? 0;
  }

  get hasFilters(): boolean {
    return !!this.searchTerm || !!this.typeFilter || !!this.categoryFilter || !!this.stockFilter;
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
