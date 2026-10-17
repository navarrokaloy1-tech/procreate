import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ApiService } from '../../../../core/services/api';
import { ToastService } from '../../../../core/services/toast';
import { IconComponent } from '../../../../core/components/icon/icon';
import { QrScannerComponent } from '../../../../core/components/qr-scanner/qr-scanner';

export interface TestItem {
  id: number;
  code?: string;
  name: string;
  price: number;
  categoryId: number;
  categoryName: string;
}

export interface TestCategory {
  id: number;
  name: string;
  tests: TestItem[];
}

/** A predefined, reusable set of tests that can be applied then adjusted. */
export interface TestPanelOption {
  id: number;
  code: string;
  name: string;
  description: string;
  testIds: number[];
  totalPrice: number;
}

export interface PatientOption {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
}

/** One patient in the batch, with the tests chosen for them. */
interface BatchEntry {
  patient: PatientOption;
  testIds: Set<number>;
  /** Panels the user explicitly applied, so overlapping panels don't light up. */
  appliedPanels: Set<number>;
}

@Component({
  selector: 'app-visit-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, IconComponent, QrScannerComponent],
  templateUrl: './visit-form.html',
  styleUrl: './visit-form.scss',
})
export class VisitForm implements OnInit, OnDestroy {
  visitForm!: FormGroup;

  /** Registered-doctor suggestions for the Referring Physician field. */
  doctorResults: { id: number; fullName: string; specialty: string }[] = [];
  /** Set when a suggestion is picked, so the referral is attributed to a doctor. */
  referringDoctorId: number | null = null;
  private subscriptions = new Subscription();
  testCategories: TestCategory[] = [];
  panels: TestPanelOption[] = [];
  testSearch = '';
  patientOptions: PatientOption[] = [];
  patientSearch = '';
  isLoadingTests = true;
  isSaving = false;
  errorMessage = '';
  successMessage = '';
  private successTimer?: ReturnType<typeof setTimeout>;

  /**
   * Patients queued for registration. Each becomes its own consultation —
   * a visit belongs to one patient, so a group is several visits, not one
   * shared record.
   */
  entries: BatchEntry[] = [];
  /** Index of the entry whose tests are being edited. */
  activeIndex = 0;

  isScannerOpen = false;
  scanError = '';

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.visitForm = this.fb.group({
      referringPhysician: [''],
      purposeOfVisit: [''],
      notes: [''],
    });

    this.loadTests();
    this.loadPanels();

    // Suggest registered doctors as the referring physician is typed. Editing
    // the text drops any previously picked doctor so a typed-over name is not
    // silently attributed to the wrong one.
    const ref = this.visitForm.get('referringPhysician')!;
    this.subscriptions.add(
      ref.valueChanges.pipe(debounceTime(250), distinctUntilChanged()).subscribe((term: string) => {
        this.referringDoctorId = null;
        this.searchDoctors(term ?? '');
      }),
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  private searchDoctors(term: string): void {
    if (!term.trim()) {
      this.doctorResults = [];
      this.cdr.markForCheck();
      return;
    }

    this.apiService
      .get<{ data: { id: number; fullName: string; specialty: string }[] }>('doctors', {
        search: term,
        isActive: true,
        pageSize: 6,
      })
      .subscribe({
        next: (res) => {
          this.doctorResults = res.data ?? [];
          this.cdr.markForCheck();
        },
        error: () => {
          this.doctorResults = [];
          this.cdr.markForCheck();
        },
      });
  }

  /** Picks a registered doctor: fills the name and attributes the referral. */
  chooseDoctor(doctor: { id: number; fullName: string }): void {
    this.referringDoctorId = doctor.id;
    // Suppress valueChanges so picking does not clear the id we just set.
    this.visitForm.get('referringPhysician')!.setValue(doctor.fullName, { emitEvent: false });
    this.doctorResults = [];
  }

  loadTests(): void {
    this.isLoadingTests = true;
    this.apiService.get<TestCategory[]>('lab/categories').subscribe({
      next: (categories) => {
        this.testCategories = categories;
        this.isLoadingTests = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoadingTests = false;
        this.cdr.markForCheck();
      },
    });
  }

  loadPanels(): void {
    this.apiService.get<TestPanelOption[]>('lab/panels').subscribe({
      next: (panels) => {
        this.panels = panels;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  // ----------------------------------------------------------
  // Patient search / batch
  // ----------------------------------------------------------

  searchPatients(): void {
    if (!this.patientSearch.trim()) {
      this.patientOptions = [];
      return;
    }

    this.apiService
      .get<{ data: PatientOption[] }>('patients', {
        search: this.patientSearch,
        pageSize: 10,
      })
      .subscribe({
        next: (response) => {
          this.patientOptions = response.data;
          this.cdr.markForCheck();
        },
        error: () => {
          this.patientOptions = [];
          this.cdr.markForCheck();
        },
      });
  }

  addPatient(patient: PatientOption): boolean {
    if (this.entries.some((e) => e.patient.id === patient.id)) {
      this.errorMessage = `${patient.firstName} ${patient.lastName} is already in this batch.`;
      return false;
    }

    this.entries.push({ patient, testIds: new Set<number>(), appliedPanels: new Set<number>() });
    this.activeIndex = this.entries.length - 1;
    this.patientSearch = '';
    this.patientOptions = [];
    this.errorMessage = '';
    return true;
  }

  selectPatient(patient: PatientOption): void {
    this.addPatient(patient);
  }

  removeEntry(index: number): void {
    this.entries.splice(index, 1);
    if (this.activeIndex >= this.entries.length) {
      this.activeIndex = Math.max(0, this.entries.length - 1);
    }
  }

  setActive(index: number): void {
    this.activeIndex = index;
  }

  get activeEntry(): BatchEntry | null {
    return this.entries[this.activeIndex] ?? null;
  }

  // ----------------------------------------------------------
  // QR scanning
  // ----------------------------------------------------------

  openScanner(): void {
    this.scanError = '';
    this.isScannerOpen = true;
  }

  closeScanner(): void {
    this.isScannerOpen = false;
    this.scanError = '';
  }

  /** Resolves a scanned payload to a patient and adds them to the batch. */
  onScanned(code: string): void {
    this.scanError = '';

    this.apiService.get<PatientOption>(`patients/by-code/${encodeURIComponent(code)}`).subscribe({
      next: (patient) => {
        const added = this.addPatient(patient);
        if (added) {
          // Keep the scanner open so a queue can be scanned back to back.
          this.scanError = '';
        } else {
          this.scanError = `${patient.firstName} ${patient.lastName} is already in this batch.`;
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.scanError =
          err?.error?.message ?? `No patient matches "${code}".`;
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Test selection
  // ----------------------------------------------------------

  toggleTest(testId: number): void {
    const entry = this.activeEntry;
    if (!entry) return;

    if (entry.testIds.has(testId)) entry.testIds.delete(testId);
    else entry.testIds.add(testId);
  }

  isTestSelected(testId: number): boolean {
    return this.activeEntry?.testIds.has(testId) ?? false;
  }

  // ----------------------------------------------------------
  // Search + panels
  // ----------------------------------------------------------

  /** Tests in a category that match the current search box. */
  visibleTests(category: TestCategory): TestItem[] {
    const q = this.testSearch.trim().toLowerCase();
    if (!q) return category.tests;
    return category.tests.filter(
      (t) => t.name.toLowerCase().includes(q) || (t.code ?? '').toLowerCase().includes(q)
    );
  }

  /** Categories that still have at least one test matching the search. */
  get visibleCategories(): TestCategory[] {
    return this.testCategories.filter((c) => this.visibleTests(c).length > 0);
  }

  /**
   * Toggles a panel for the active patient. Applying adds its tests; clicking
   * again removes them — but keeps any test still needed by another panel the
   * user applied, since panels overlap. Tests stay individually editable.
   */
  applyPanel(panel: TestPanelOption): void {
    const entry = this.activeEntry;
    if (!entry) return;

    if (entry.appliedPanels.has(panel.id)) {
      const keep = new Set<number>();
      for (const other of this.panels)
        if (other.id !== panel.id && entry.appliedPanels.has(other.id))
          other.testIds.forEach((id) => keep.add(id));
      panel.testIds.forEach((id) => { if (!keep.has(id)) entry.testIds.delete(id); });
      entry.appliedPanels.delete(panel.id);
    } else {
      panel.testIds.forEach((id) => entry.testIds.add(id));
      entry.appliedPanels.add(panel.id);
    }
  }

  /** True only when the user explicitly applied this panel to the active patient. */
  isPanelApplied(panel: TestPanelOption): boolean {
    return this.activeEntry?.appliedPanels.has(panel.id) ?? false;
  }

  /** Copies the active patient's selection onto everyone else in the batch. */
  applyTestsToAll(): void {
    const entry = this.activeEntry;
    if (!entry || this.entries.length < 2) return;

    for (const other of this.entries) {
      if (other === entry) continue;
      other.testIds = new Set(entry.testIds);
      other.appliedPanels = new Set(entry.appliedPanels);
    }
  }

  private allTests(): TestItem[] {
    return this.testCategories.flatMap((c) => c.tests);
  }

  testsFor(entry: BatchEntry): TestItem[] {
    return this.allTests().filter((t) => entry.testIds.has(t.id));
  }

  get selectedTestItems(): TestItem[] {
    const entry = this.activeEntry;
    return entry ? this.testsFor(entry) : [];
  }

  entryTotal(entry: BatchEntry): number {
    return this.testsFor(entry).reduce((sum, t) => sum + t.price, 0);
  }

  get totalAmount(): number {
    const entry = this.activeEntry;
    return entry ? this.entryTotal(entry) : 0;
  }

  get batchTotal(): number {
    return this.entries.reduce((sum, e) => sum + this.entryTotal(e), 0);
  }

  get entriesWithoutTests(): number {
    return this.entries.filter((e) => e.testIds.size === 0).length;
  }

  // ----------------------------------------------------------
  // Save
  // ----------------------------------------------------------

  onSubmit(): void {
    if (this.entries.length === 0) {
      this.errorMessage = 'Add at least one patient.';
      return;
    }

    if (this.entriesWithoutTests > 0) {
      this.errorMessage =
        this.entriesWithoutTests === 1
          ? 'One patient has no tests selected.'
          : `${this.entriesWithoutTests} patients have no tests selected.`;
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';

    const shared = this.visitForm.value;

    const payload = {
      referringPhysician: shared.referringPhysician ?? '',
      referringDoctorId: this.referringDoctorId,
      purposeOfVisit: shared.purposeOfVisit ?? '',
      entries: this.entries.map((e) => ({
        patientId: e.patient.id,
        testIds: Array.from(e.testIds),
      })),
    };

    // One request for the whole batch, so a group is registered atomically
    // rather than leaving half the queue created if something fails midway.
    const registered = this.entries.length;
    this.apiService.post<{ created: number }>('visits/batch', payload).subscribe({
      next: () => {
        this.isSaving = false;
        // On the Cashier Desk the clerk usually serves the next patient straight
        // away, so the batch resets in place rather than leaving the desk. From
        // New Consultation it hands off to the lab worklist.
        if (this.onCashier) {
          this.resetBatch();
          this.flashSuccess(
            `Registered ${registered} ${registered === 1 ? 'patient' : 'patients'}. Ready for the next.`
          );
        } else {
          this.toast.success(
            `Lab order created for ${registered} ${registered === 1 ? 'patient' : 'patients'}.`
          );
          this.router.navigate(['/app/lab-results']);
        }
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Could not register the order.';
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    // Nowhere to go back to on the Cashier Desk — clear and start fresh instead.
    if (this.onCashier) {
      this.resetBatch();
      this.cdr.markForCheck();
      return;
    }
    this.router.navigate(['/app/lab-results']);
  }

  /** True when this screen is serving the Cashier Desk route. */
  private get onCashier(): boolean {
    return this.router.url.split('?')[0].startsWith('/app/cashier');
  }

  /** Clears the batch back to an empty state for the next patient. */
  private resetBatch(): void {
    this.entries = [];
    this.activeIndex = 0;
    this.patientSearch = '';
    this.patientOptions = [];
    this.testSearch = '';
    this.errorMessage = '';
    this.referringDoctorId = null;
    this.doctorResults = [];
    this.visitForm.reset({ referringPhysician: '', purposeOfVisit: '', notes: '' });
  }

  private flashSuccess(message: string): void {
    this.successMessage = message;
    clearTimeout(this.successTimer);
    this.successTimer = setTimeout(() => {
      this.successMessage = '';
      this.cdr.markForCheck();
    }, 4000);
  }

  initials(patient: PatientOption): string {
    return `${patient.firstName?.[0] ?? ''}${patient.lastName?.[0] ?? ''}`.toLowerCase();
  }
}
