import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

interface DoctorRow {
  id: number;
  doctorCode: string;
  fullName: string;
  specialty: string;
  subSpecialty: string;
  email: string;
  contactNumber: string;
  prcLicenseNumber: string;
  consultationFee: number;
  isActive: boolean;
  hasLogin: boolean;
}

interface ScheduleRow {
  dayOfWeek: number;
  isAvailable: boolean;
  startTime: string;
  endTime: string;
}

interface DoctorDetail {
  id: number;
  doctorCode: string;
  firstName: string;
  middleName: string;
  lastName: string;
  suffix: string;
  gender: string;
  specialty: string;
  subSpecialty: string;
  email: string;
  contactNumber: string;
  prcLicenseNumber: string;
  prcLicenseExpiry: string | null;
  ptrNumber: string;
  s2LicenseNumber: string;
  tinNumber: string;
  consultationFee: number;
  followUpFee: number;
  specialistFee: number;
  bio: string;
  isActive: boolean;
  userId: number | null;
  schedules: ScheduleRow[];
}

@Component({
  selector: 'app-doctor-list',
  standalone: false,
  templateUrl: './doctor-list.html',
  styleUrl: './doctor-list.scss',
})
export class DoctorListComponent implements OnInit {
  readonly dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  readonly specialtyOptions = [
    'Obstetrics and Gynecology', 'Internal Medicine', 'Pediatrics',
    'Family Medicine', 'Radiology', 'Pathology', 'Surgery', 'Cardiology',
  ];

  readonly genderOptions = ['Male', 'Female', 'Other'];

  readonly tabs = [
    { id: 'personal', label: 'Personal Info', icon: 'user' },
    { id: 'licenses', label: 'Licenses', icon: 'credit-card' },
    { id: 'practice', label: 'Practice', icon: 'briefcase' },
    { id: 'schedule', label: 'Schedule', icon: 'calendar' },
  ];

  doctors: DoctorRow[] = [];
  specialties: string[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 15;
  readonly pageSizeOptions = [15, 25, 50];

  searchTerm = '';
  specialtyFilter = '';
  statusFilter = '';
  isLoading = false;
  errorMessage = '';

  // --- Modal ---
  isModalOpen = false;
  isSaving = false;
  modalError = '';
  activeTab = 'personal';
  editingId: number | null = null;

  form = this.emptyForm();
  schedule: ScheduleRow[] = [];

  private searchSubject = new Subject<string>();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.searchTerm = term;
      this.pageIndex = 0;
      this.load();
    });

    this.loadSpecialties();
    this.load();
  }

  private emptyForm() {
    return {
      firstName: '', middleName: '', lastName: '', suffix: '', gender: '',
      specialty: '', subSpecialty: '', email: '', contactNumber: '',
      prcLicenseNumber: '', prcLicenseExpiry: '', ptrNumber: '',
      s2LicenseNumber: '', tinNumber: '',
      consultationFee: 0, followUpFee: 0, specialistFee: 0,
      bio: '', isActive: true, userId: null as number | null,
    };
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: DoctorRow[] }>('doctors', {
        search: this.searchTerm,
        specialty: this.specialtyFilter,
        isActive: this.statusFilter === '' ? null : this.statusFilter === 'active',
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.doctors = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load doctors.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  private loadSpecialties(): void {
    this.api.get<string[]>('doctors/specialties').subscribe({
      next: (res) => {
        this.specialties = res;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onSpecialtyChange(value: string): void {
    this.specialtyFilter = value;
    this.pageIndex = 0;
    this.load();
  }

  onStatusChange(value: string): void {
    this.statusFilter = value;
    this.pageIndex = 0;
    this.load();
  }

  onPageSizeChange(size: string): void {
    this.pageSize = Number(size);
    this.pageIndex = 0;
    this.load();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.load();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.specialtyFilter = '';
    this.statusFilter = '';
    this.pageIndex = 0;
    this.load();
  }

  // ----------------------------------------------------------
  // Modal
  // ----------------------------------------------------------

  openCreate(): void {
    this.editingId = null;
    this.form = this.emptyForm();
    this.schedule = this.defaultSchedule();
    this.activeTab = 'personal';
    this.modalError = '';
    this.isModalOpen = true;
  }

  openEdit(row: DoctorRow): void {
    this.api.get<DoctorDetail>(`doctors/${row.id}`).subscribe({
      next: (detail) => {
        this.editingId = detail.id;
        this.form = {
          firstName: detail.firstName,
          middleName: detail.middleName,
          lastName: detail.lastName,
          suffix: detail.suffix,
          gender: detail.gender,
          specialty: detail.specialty,
          subSpecialty: detail.subSpecialty,
          email: detail.email,
          contactNumber: detail.contactNumber,
          prcLicenseNumber: detail.prcLicenseNumber,
          prcLicenseExpiry: detail.prcLicenseExpiry
            ? detail.prcLicenseExpiry.substring(0, 10)
            : '',
          ptrNumber: detail.ptrNumber,
          s2LicenseNumber: detail.s2LicenseNumber,
          tinNumber: detail.tinNumber,
          consultationFee: detail.consultationFee,
          followUpFee: detail.followUpFee,
          specialistFee: detail.specialistFee,
          bio: detail.bio,
          isActive: detail.isActive,
          userId: detail.userId,
        };
        this.schedule = detail.schedules.length
          ? detail.schedules.map((s) => ({ ...s }))
          : this.defaultSchedule();
        this.activeTab = 'personal';
        this.modalError = '';
        this.isModalOpen = true;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not load that doctor.';
        this.cdr.markForCheck();
      },
    });
  }

  private defaultSchedule(): ScheduleRow[] {
    return Array.from({ length: 7 }, (_, day) => ({
      dayOfWeek: day,
      isAvailable: day >= 1 && day <= 5,
      startTime: '08:00',
      endTime: '17:00',
    }));
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
  }

  selectTab(id: string): void {
    this.activeTab = id;
  }

  save(): void {
    if (!this.form.firstName.trim() || !this.form.lastName.trim()) {
      this.modalError = 'First and last name are required.';
      this.activeTab = 'personal';
      return;
    }
    if (!this.form.specialty) {
      this.modalError = 'Specialty is required.';
      this.activeTab = 'practice';
      return;
    }

    this.isSaving = true;
    this.modalError = '';

    const payload = {
      ...this.form,
      prcLicenseExpiry: this.form.prcLicenseExpiry || null,
    };

    const request$ = this.editingId
      ? this.api.put<DoctorDetail>(`doctors/${this.editingId}`, payload)
      : this.api.post<DoctorDetail>('doctors', payload);

    request$.subscribe({
      next: (saved) => {
        // Schedule is a separate endpoint, so persist it once the doctor exists.
        this.api.put<DoctorDetail>(`doctors/${saved.id}/schedule`, this.schedule).subscribe({
          next: () => {
            this.isSaving = false;
            this.isModalOpen = false;
            this.loadSpecialties();
            this.load();
            this.cdr.markForCheck();
          },
          error: () => {
            this.isSaving = false;
            this.modalError = 'Doctor saved, but the schedule could not be updated.';
            this.load();
            this.cdr.markForCheck();
          },
        });
      },
      error: (err) => {
        this.modalError = err?.error?.message ?? 'Could not save the doctor.';
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  remove(row: DoctorRow): void {
    if (!window.confirm(`Remove ${row.fullName}? Doctors with appointment history are deactivated instead.`)) {
      return;
    }

    this.api.delete<void>(`doctors/${row.id}`).subscribe({
      next: () => {
        this.load();
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not remove that doctor.';
        this.cdr.markForCheck();
      },
    });
  }

  initials(fullName: string): string {
    return fullName
      .replace(/^Dr\.\s*/i, '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toLowerCase();
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
    return Array.from({ length: this.totalPages }, (_, i) => i);
  }
}
