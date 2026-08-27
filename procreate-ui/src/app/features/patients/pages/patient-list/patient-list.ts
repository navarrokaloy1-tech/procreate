import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
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
  suffix?: string;
  civilStatus?: string;
  occupation?: string;
  nationality?: string;
  landline?: string;
  country?: string;
  region?: string;
  province?: string;
  city?: string;
  zipCode?: string;
  philHealthNumber?: string;
  seniorCitizenId?: string;
  pwdId?: string;
  hmoProvider?: string;
  hmoAccountNumber?: string;
  emergencyContactName?: string;
  emergencyContactRelationship?: string;
  emergencyContactNumber?: string;
  emergencyContactNotes?: string;
}

@Component({
  selector: 'app-patient-list',
  standalone: false,
  templateUrl: './patient-list.html',
  styleUrl: './patient-list.scss',
})
export class PatientListComponent implements OnInit, OnDestroy {
  patients: Patient[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 15;
  searchTerm = '';
  genderFilter = '';
  isLoading = false;

  /** Registration/edit sheet state; null id means a new patient. */
  isFormOpen = false;
  formPatientId: number | null = null;

  readonly pageSizeOptions = [15, 25, 50, 100];

  private searchSubject = new Subject<string>();
  private subscriptions = new Subscription();

  constructor(
    private apiService: ApiService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const searchSub = this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((term) => {
        this.searchTerm = term;
        this.pageIndex = 0;
        this.loadPatients();
        this.cdr.markForCheck();
      });

    this.subscriptions.add(searchSub);
    this.loadPatients();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadPatients(): void {
    this.isLoading = true;
    this.apiService
      .get<{ data: Patient[]; total: number }>('patients', {
        search: this.searchTerm,
        gender: this.genderFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (response) => {
          this.patients = response.data;
          this.totalCount = response.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onGenderChange(gender: string): void {
    this.genderFilter = gender;
    this.pageIndex = 0;
    this.loadPatients();
  }

  onPageSizeChange(size: string): void {
    this.pageSize = Number(size);
    this.pageIndex = 0;
    this.loadPatients();
  }

  clearFilters(): void {
    this.genderFilter = '';
    this.searchTerm = '';
    this.pageIndex = 0;
    this.loadPatients();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.loadPatients();
  }

  deletePatient(id: number): void {
    const confirmed = window.confirm(
      'Are you sure you want to delete this patient? This action cannot be undone.'
    );
    if (!confirmed) return;

    this.apiService.delete<void>('patients/' + id).subscribe({
      next: () => {
        this.loadPatients();
        this.cdr.markForCheck();
      },
      error: () => {
        window.alert('Failed to delete patient. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  viewPatient(id: number): void {
    this.router.navigate(['/app/patients', id]);
  }

  editPatient(id: number): void {
    this.formPatientId = id;
    this.isFormOpen = true;
  }

  newPatient(): void {
    this.formPatientId = null;
    this.isFormOpen = true;
  }

  onFormSaved(): void {
    this.isFormOpen = false;
    this.loadPatients();
  }

  onFormCancelled(): void {
    this.isFormOpen = false;
  }

  getPatientFullName(p: Patient): string {
    const parts = [p.firstName, p.middleName, p.lastName].filter(
      (part) => part && part.trim()
    );
    return parts.join(' ');
  }

  /** Two-letter monogram for the row avatar. */
  getInitials(p: Patient): string {
    return [p.firstName, p.lastName]
      .filter((part) => part && part.trim())
      .slice(0, 2)
      .map((part) => part.trim()[0])
      .join('')
      .toUpperCase();
  }

  /** Whole years elapsed since date of birth, or null when unknown. */
  getAge(dateOfBirth: string): number | null {
    if (!dateOfBirth) return null;

    const dob = new Date(dateOfBirth);
    if (isNaN(dob.getTime())) return null;

    const today = new Date();
    let age = today.getFullYear() - dob.getFullYear();
    const monthDelta = today.getMonth() - dob.getMonth();
    if (monthDelta < 0 || (monthDelta === 0 && today.getDate() < dob.getDate())) {
      age--;
    }
    return age >= 0 ? age : null;
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
    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i);
    }

    const current = this.pageIndex;
    const pages: number[] = [];

    pages.push(0);
    if (current > 3) pages.push(-1); // ellipsis marker

    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    if (current < total - 4) pages.push(-2); // ellipsis marker
    pages.push(total - 1);

    return pages;
  }
}
