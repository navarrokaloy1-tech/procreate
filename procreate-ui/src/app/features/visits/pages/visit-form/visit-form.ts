import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

export interface TestItem {
  id: number;
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
}

@Component({
  selector: 'app-visit-form',
  standalone: false,
  templateUrl: './visit-form.html',
  styleUrl: './visit-form.scss',
})
export class VisitForm implements OnInit {
  visitForm!: FormGroup;
  testCategories: TestCategory[] = [];
  patientOptions: PatientOption[] = [];
  patientSearch = '';
  isLoadingTests = true;
  isSaving = false;
  errorMessage = '';

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
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.visitForm = this.fb.group({
      referringPhysician: [''],
      purposeOfVisit: [''],
      notes: [''],
    });

    this.loadTests();
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

    this.entries.push({ patient, testIds: new Set<number>() });
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

  /** Copies the active patient's selection onto everyone else in the batch. */
  applyTestsToAll(): void {
    const entry = this.activeEntry;
    if (!entry || this.entries.length < 2) return;

    for (const other of this.entries) {
      if (other === entry) continue;
      other.testIds = new Set(entry.testIds);
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
      purposeOfVisit: shared.purposeOfVisit ?? '',
      entries: this.entries.map((e) => ({
        patientId: e.patient.id,
        testIds: Array.from(e.testIds),
      })),
    };

    // One request for the whole batch, so a group is registered atomically
    // rather than leaving half the queue created if something fails midway.
    this.apiService.post<{ created: number }>('visits/batch', payload).subscribe({
      next: () => {
        this.isSaving = false;
        this.router.navigate(['/app/visits']);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Could not register the consultations.';
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/app/visits']);
  }

  initials(patient: PatientOption): string {
    return `${patient.firstName?.[0] ?? ''}${patient.lastName?.[0] ?? ''}`.toLowerCase();
  }
}
