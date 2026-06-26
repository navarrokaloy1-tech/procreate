import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
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

@Component({
  selector: 'app-visit-form',
  standalone: false,
  templateUrl: './visit-form.html',
  styleUrl: './visit-form.scss',
})
export class VisitForm implements OnInit {
  visitForm!: FormGroup;
  selectedTests: Set<number> = new Set();
  testCategories: TestCategory[] = [];
  patientOptions: PatientOption[] = [];
  patientSearch = '';
  isLoadingTests = true;
  isSaving = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private apiService: ApiService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.visitForm = this.fb.group({
      patientId: [null, Validators.required],
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

  selectPatient(patient: PatientOption): void {
    this.visitForm.patchValue({ patientId: patient.id });
    this.patientSearch = `${patient.firstName} ${patient.lastName} (${patient.patientCode})`;
    this.patientOptions = [];
  }

  toggleTest(testId: number): void {
    if (this.selectedTests.has(testId)) {
      this.selectedTests.delete(testId);
    } else {
      this.selectedTests.add(testId);
    }
  }

  isTestSelected(testId: number): boolean {
    return this.selectedTests.has(testId);
  }

  get totalAmount(): number {
    let total = 0;
    for (const category of this.testCategories) {
      for (const test of category.tests) {
        if (this.selectedTests.has(test.id)) {
          total += test.price;
        }
      }
    }
    return total;
  }

  get selectedTestItems(): TestItem[] {
    const items: TestItem[] = [];
    for (const category of this.testCategories) {
      for (const test of category.tests) {
        if (this.selectedTests.has(test.id)) {
          items.push(test);
        }
      }
    }
    return items;
  }

  onSubmit(): void {
    if (this.visitForm.invalid) {
      this.visitForm.markAllAsTouched();
      this.errorMessage = 'Please select a patient before saving.';
      return;
    }

    if (this.selectedTests.size === 0) {
      this.errorMessage = 'Please select at least one test.';
      return;
    }

    this.errorMessage = '';
    this.isSaving = true;

    const formValue = this.visitForm.value;
    const payload = {
      patientId: formValue.patientId,
      referringPhysician: formValue.referringPhysician,
      purposeOfVisit: formValue.purposeOfVisit,
      notes: formValue.notes,
      testIds: Array.from(this.selectedTests),
    };

    this.apiService.post<any>('visits', payload).subscribe({
      next: () => {
        this.isSaving = false;
        this.router.navigate(['/app/visits']);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.errorMessage =
          err?.error?.message || 'An error occurred while saving the visit.';
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/app/visits']);
  }
}
