import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';

export interface Patient {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
  middleName: string;
  dateOfBirth: string;
  gender: string;
  contactNumber: string;
  email: string;
  address: string;
  bloodType: string;
  civilStatus?: string;
  nationality?: string;
  occupation?: string;
  emergencyContactName?: string;
  emergencyContactNumber?: string;
  emergencyContactRelationship?: string;
  photoUrl?: string;
}

export interface Product {
  id: number;
  code: string;
  name: string;
  category: string;
  price: number;
}

interface HistoryRow {
  orderId: number;
  orderCode: string;
  productName: string;
  quantity: number;
  status: string;
  updatedAt: string;
  createdAt: string;
}

interface SummaryItem {
  product: Product;
  quantity: number;
}

@Component({
  selector: 'app-patient-orders',
  standalone: false,
  templateUrl: './patient-orders.html',
  styleUrl: './patient-orders.scss'
})
export class PatientOrdersComponent implements OnInit, OnDestroy {
  // ---- Search patient ----
  searchQuery = '';
  searchResults: Patient[] = [];
  showDropdown = false;
  searching = false;
  private searchSubject = new Subject<string>();
  private subs = new Subscription();

  // ---- Selected patient ----
  patient: Patient | null = null;
  history: HistoryRow[] = [];
  historyPage = 0;
  historyPageSize = 3;
  historyTotal = 0;

  // ---- Miscellaneous / checkout fields ----
  misc = { referrer: '', tags: '', branch: '', contactNumber: '', contactEmail: '', notes: '' };

  // ---- Summary line items ----
  items: SummaryItem[] = [];

  // ---- Register modal ----
  showRegister = false;
  registerSaving = false;
  registerError = '';
  reg = this.emptyRegister();

  // ---- Add Product modal ----
  showAddProduct = false;
  productQuery = '';
  products: Product[] = [];
  private productSearchSubject = new Subject<string>();

  saving = false;
  toast = '';

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.subs.add(
      this.searchSubject.pipe(debounceTime(150), distinctUntilChanged()).subscribe(term => {
        this.runPatientSearch(term);
      })
    );
    this.subs.add(
      this.productSearchSubject.pipe(debounceTime(250), distinctUntilChanged()).subscribe(term => {
        this.runProductSearch(term);
      })
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // ================= Patient search =================
  onSearch(term: string): void {
    this.searchQuery = term;
    if (!term || term.trim().length < 1) {
      this.searchResults = [];
      this.showDropdown = false;
      this.searching = false;
      this.cdr.markForCheck();
      return;
    }
    this.showDropdown = true;
    this.searching = true;
    this.cdr.markForCheck();
    this.searchSubject.next(term.trim());
  }

  private runPatientSearch(term: string): void {
    this.api.get<{ data: Patient[] }>('patients', { search: term, page: 1, pageSize: 8 }).subscribe({
      next: res => { this.searchResults = res.data; this.searching = false; this.cdr.markForCheck(); },
      error: () => { this.searchResults = []; this.searching = false; this.cdr.markForCheck(); }
    });
  }

  selectPatient(p: Patient): void {
    this.patient = p;
    this.showDropdown = false;
    this.searchQuery = '';
    this.searchResults = [];
    this.misc.contactNumber = p.contactNumber || '';
    this.misc.contactEmail = p.email || '';
    this.historyPage = 0;
    this.loadHistory();
    this.cdr.markForCheck();
  }

  clearPatient(): void {
    this.patient = null;
    this.history = [];
    this.items = [];
    this.misc = { referrer: '', tags: '', branch: '', contactNumber: '', contactEmail: '', notes: '' };
    this.cdr.markForCheck();
  }

  patientFullName(p: Patient | null): string {
    if (!p) return '';
    return [p.lastName, [p.firstName, p.middleName].filter(Boolean).join(' ')].filter(Boolean).join(', ');
  }

  age(dob?: string): number | null {
    if (!dob) return null;
    const d = new Date(dob);
    if (isNaN(d.getTime())) return null;
    const now = new Date();
    let a = now.getFullYear() - d.getFullYear();
    const m = now.getMonth() - d.getMonth();
    if (m < 0 || (m === 0 && now.getDate() < d.getDate())) a--;
    return a;
  }

  // ================= History =================
  loadHistory(): void {
    if (!this.patient) return;
    this.api.get<{ data: HistoryRow[]; total: number }>(`orders/patient/${this.patient.id}`,
      { page: this.historyPage + 1, pageSize: this.historyPageSize }).subscribe({
      next: res => { this.history = res.data; this.historyTotal = res.total; this.cdr.markForCheck(); },
      error: () => { this.history = []; this.cdr.markForCheck(); }
    });
  }

  get historyPages(): number {
    return Math.max(1, Math.ceil(this.historyTotal / this.historyPageSize));
  }

  get historyPageArray(): number[] {
    return Array.from({ length: this.historyPages }, (_, i) => i);
  }

  goHistoryPage(i: number): void {
    this.historyPage = i;
    this.loadHistory();
  }

  statusClass(status: string): string {
    switch ((status || '').toLowerCase()) {
      case 'ordered': return 'badge--ordered';
      case 'cancelled': return 'badge--cancelled';
      case 'draft': return 'badge--draft';
      case 'completed': return 'badge--completed';
      default: return 'badge--draft';
    }
  }

  relativeTime(iso: string): string {
    const d = new Date(iso);
    const diffMs = Date.now() - d.getTime();
    const mins = Math.floor(diffMs / 60000);
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins} min(s) ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}hr(s) ${mins % 60}min(s) ago`;
    const days = Math.floor(hrs / 24);
    return `${days} day(s) ago`;
  }

  // ================= Register modal =================
  private emptyRegister() {
    return {
      firstName: '', middleName: '', lastName: '', phone: '', birthday: '',
      email: '', civilStatus: '', gender: '', nationality: '', occupation: '',
      address: '', emergencyContactName: '', emergencyContactRelationship: '',
      emergencyPhone: '', photoUrl: ''
    };
  }

  openRegister(): void {
    this.reg = this.emptyRegister();
    this.registerError = '';
    this.showRegister = true;
    this.showDropdown = false;
    this.cdr.markForCheck();
  }

  closeRegister(): void {
    this.showRegister = false;
    this.cdr.markForCheck();
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => { this.reg.photoUrl = reader.result as string; this.cdr.markForCheck(); };
    reader.readAsDataURL(file);
  }

  submitRegister(): void {
    if (!this.reg.firstName || !this.reg.lastName || !this.reg.birthday ||
        !this.reg.civilStatus || !this.reg.emergencyContactName || !this.reg.emergencyContactRelationship) {
      this.registerError = 'Please fill in all required fields.';
      this.cdr.markForCheck();
      return;
    }
    this.registerSaving = true;
    const body = {
      firstName: this.reg.firstName,
      middleName: this.reg.middleName,
      lastName: this.reg.lastName,
      contactNumber: this.reg.phone ? `+63${this.reg.phone}` : '',
      dateOfBirth: this.reg.birthday,
      email: this.reg.email,
      civilStatus: this.reg.civilStatus,
      gender: this.reg.gender,
      nationality: this.reg.nationality,
      occupation: this.reg.occupation,
      address: this.reg.address,
      bloodType: '',
      emergencyContactName: this.reg.emergencyContactName,
      emergencyContactRelationship: this.reg.emergencyContactRelationship,
      emergencyContactNumber: this.reg.emergencyPhone ? `+63${this.reg.emergencyPhone}` : '',
      photoUrl: this.reg.photoUrl
    };
    this.api.post<Patient>('patients', body).subscribe({
      next: created => {
        this.registerSaving = false;
        this.showRegister = false;
        this.selectPatient(created);
        this.cdr.markForCheck();
      },
      error: () => {
        this.registerSaving = false;
        this.registerError = 'Failed to register patient. Please try again.';
        this.cdr.markForCheck();
      }
    });
  }

  // ================= Add Product modal =================
  openAddProduct(): void {
    this.showAddProduct = true;
    this.productQuery = '';
    this.runProductSearch('');
    this.cdr.markForCheck();
  }

  closeAddProduct(): void {
    this.showAddProduct = false;
    this.cdr.markForCheck();
  }

  onProductSearch(term: string): void {
    this.productQuery = term;
    this.productSearchSubject.next(term);
  }

  private runProductSearch(term: string): void {
    this.api.get<Product[]>('products', { search: term }).subscribe({
      next: res => { this.products = res; this.cdr.markForCheck(); },
      error: () => { this.products = []; this.cdr.markForCheck(); }
    });
  }

  addProduct(p: Product): void {
    const existing = this.items.find(i => i.product.id === p.id);
    if (existing) existing.quantity++;
    else this.items.push({ product: p, quantity: 1 });
    this.cdr.markForCheck();
  }

  incQty(item: SummaryItem): void { item.quantity++; this.cdr.markForCheck(); }
  decQty(item: SummaryItem): void {
    item.quantity--;
    if (item.quantity < 1) this.removeItem(item);
    this.cdr.markForCheck();
  }
  removeItem(item: SummaryItem): void {
    this.items = this.items.filter(i => i !== item);
    this.cdr.markForCheck();
  }

  get totalQty(): number { return this.items.reduce((s, i) => s + i.quantity, 0); }
  get totalAmount(): number { return this.items.reduce((s, i) => s + i.quantity * i.product.price, 0); }

  get canComplete(): boolean { return !!this.patient && this.items.length > 0; }

  // ================= Save / Complete =================
  saveDraft(): void { this.createOrder('Draft'); }
  complete(): void { this.createOrder('Ordered'); }

  private createOrder(status: 'Draft' | 'Ordered'): void {
    if (!this.patient || this.items.length === 0) return;
    this.saving = true;
    const body = {
      patientId: this.patient.id,
      status,
      referrer: this.misc.referrer,
      tags: this.misc.tags,
      branch: this.misc.branch,
      contactNumber: this.misc.contactNumber,
      contactEmail: this.misc.contactEmail,
      notes: this.misc.notes,
      items: this.items.map(i => ({ productId: i.product.id, quantity: i.quantity }))
    };
    this.api.post('orders', body).subscribe({
      next: () => {
        this.saving = false;
        this.toast = status === 'Draft' ? 'Order saved as draft.' : 'Order completed successfully.';
        this.items = [];
        this.loadHistory();
        this.cdr.markForCheck();
        setTimeout(() => { this.toast = ''; this.cdr.markForCheck(); }, 3000);
      },
      error: () => {
        this.saving = false;
        this.toast = 'Failed to save order.';
        this.cdr.markForCheck();
        setTimeout(() => { this.toast = ''; this.cdr.markForCheck(); }, 3000);
      }
    });
  }
}
