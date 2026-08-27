import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../../../core/services/api';
import { ComboOption } from '../../../../core/components/combo-select/combo-select';

interface Certificate {
  id: number;
  certificateNumber: string;
  patientId: number | null;
  issuedTo: string;
  patientCode: string;
  doctorId: number;
  doctorName: string;
  issueDate: string;
  template: string;
  diagnosis: string;
  recommendation: string;
  remarks: string;
  isWalkIn: boolean;
}

interface DoctorOption {
  id: number;
  fullName: string;
  specialty: string;
}

interface PatientOption {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
}

@Component({
  selector: 'app-certificate-list',
  standalone: false,
  templateUrl: './certificate-list.html',
  styleUrl: './certificate-list.scss',
})
export class CertificateListComponent implements OnInit {
  readonly templates = [
    { id: 'General', label: 'General', icon: 'file-text' },
    { id: 'Work', label: 'Work', icon: 'briefcase' },
    { id: 'School', label: 'School', icon: 'graduation-cap' },
  ];

  certificates: Certificate[] = [];
  doctors: DoctorOption[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 10;
  readonly pageSizeOptions = [10, 25, 50];

  searchTerm = '';
  templateFilter = '';
  isLoading = false;
  errorMessage = '';

  // --- Issue modal ---
  isModalOpen = false;
  isSaving = false;
  modalError = '';
  /** 'registered' issues against a patient record; 'walkin' takes a free name. */
  recipientMode: 'registered' | 'walkin' = 'registered';

  form = this.blankForm();

  patientQuery = '';
  patientResults: PatientOption[] = [];
  private patientSearch$ = new Subject<string>();
  private searchSubject = new Subject<string>();

  /** The certificate currently rendered into the print-only sheet. */
  printing: Certificate | null = null;

  constructor(
    private api: ApiService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.searchTerm = term;
      this.pageIndex = 0;
      this.load();
    });

    this.patientSearch$
      .pipe(debounceTime(250), distinctUntilChanged())
      .subscribe((term) => this.runPatientSearch(term));

    this.loadDoctors();
    this.load();

    // Arriving from a patient chart with ?patientId= means "issue one for
    // them": open the sheet on the Registered Patient tab with that patient
    // already chosen, then drop the parameter so a reload starts clean.
    const patientId = Number(this.route.snapshot.queryParamMap.get('patientId'));
    if (patientId) this.openModalForPatient(patientId);
  }

  /** Opens the issue sheet with a registered patient pre-selected. */
  private openModalForPatient(patientId: number): void {
    this.api.get<PatientOption>(`patients/${patientId}`).subscribe({
      next: (patient) => {
        this.openModal();
        this.choosePatient(patient);
        this.router.navigate([], { queryParams: {}, replaceUrl: true });
        this.cdr.markForCheck();
      },
      error: () => {
        // The chart link is stale or the patient is gone. The list still
        // works, so say nothing and leave the sheet closed.
        this.router.navigate([], { queryParams: {}, replaceUrl: true });
      },
    });
  }

  private blankForm() {
    return {
      patientId: 0,
      walkInName: '',
      walkInAge: '',
      walkInAddress: '',
      doctorId: 0,
      issueDate: new Date().toISOString().substring(0, 10),
      template: 'General',
      diagnosis: '',
      recommendation: '',
      remarks: '',
    };
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.api
      .get<{ total: number; data: Certificate[] }>('medicalcertificates', {
        search: this.searchTerm,
        template: this.templateFilter,
        page: this.pageIndex + 1,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (res) => {
          this.certificates = res.data;
          this.totalCount = res.total;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load certificates.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  private loadDoctors(): void {
    this.api
      .get<{ data: DoctorOption[] }>('doctors', { isActive: true, pageSize: 100 })
      .subscribe({
        next: (res) => {
          this.doctors = res.data;
          // The sheet can be opened from a patient chart before this call
          // lands, which would leave the physician combo empty.
          if (this.isModalOpen && !this.form.doctorId) {
            this.form.doctorId = this.doctors[0]?.id ?? 0;
          }
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  get doctorOptions(): ComboOption[] {
    return this.doctors.map((d) => ({
      value: String(d.id),
      label: `${d.fullName} — ${d.specialty}`,
    }));
  }

  get templateOptions(): ComboOption[] {
    return [{ value: '', label: 'All Types' }].concat(
      this.templates.map((t) => ({ value: t.id, label: t.label }))
    );
  }

  get pageSizeSelection(): ComboOption[] {
    return this.pageSizeOptions.map((n) => ({ value: String(n), label: String(n) }));
  }

  onSearch(term: string): void {
    this.searchSubject.next(term);
  }

  onTemplateFilter(value: string): void {
    this.templateFilter = value;
    this.pageIndex = 0;
    this.load();
  }

  onPageSizeChange(value: string): void {
    this.pageSize = Number(value);
    this.pageIndex = 0;
    this.load();
  }

  onPageChange(page: number): void {
    this.pageIndex = page;
    this.load();
  }

  // ----------------------------------------------------------
  // Issue modal
  // ----------------------------------------------------------

  openModal(): void {
    this.form = this.blankForm();
    this.form.doctorId = this.doctors[0]?.id ?? 0;
    this.recipientMode = 'registered';
    this.patientQuery = '';
    this.patientResults = [];
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
  }

  setRecipientMode(mode: 'registered' | 'walkin'): void {
    this.recipientMode = mode;
    // Clear the other side so only one recipient is ever submitted.
    if (mode === 'registered') {
      this.form.walkInName = '';
      this.form.walkInAge = '';
      this.form.walkInAddress = '';
    } else {
      this.form.patientId = 0;
      this.patientQuery = '';
      this.patientResults = [];
    }
  }

  selectTemplate(id: string): void {
    this.form.template = id;
  }

  onPatientQuery(term: string): void {
    this.patientQuery = term;
    this.form.patientId = 0;
    this.patientSearch$.next(term);
  }

  private runPatientSearch(term: string): void {
    if (!term.trim()) {
      this.patientResults = [];
      this.cdr.markForCheck();
      return;
    }

    this.api
      .get<{ data: PatientOption[] }>('patients', { search: term, pageSize: 8 })
      .subscribe({
        next: (res) => {
          this.patientResults = res.data;
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  choosePatient(patient: PatientOption): void {
    this.form.patientId = patient.id;
    this.patientQuery = `${patient.firstName} ${patient.lastName} · ${patient.patientCode}`;
    this.patientResults = [];
  }

  save(): void {
    if (this.recipientMode === 'registered' && !this.form.patientId) {
      this.modalError = 'Please choose a patient.';
      return;
    }
    if (this.recipientMode === 'walkin' && !this.form.walkInName.trim()) {
      this.modalError = 'Please enter the walk-in name.';
      return;
    }
    if (!this.form.doctorId) {
      this.modalError = 'Please choose an attending physician.';
      return;
    }
    if (!this.form.diagnosis.trim()) {
      this.modalError = 'Diagnosis / medical findings is required.';
      return;
    }

    this.isSaving = true;
    this.modalError = '';

    this.api
      .post<Certificate>('medicalcertificates', {
        patientId: this.recipientMode === 'registered' ? this.form.patientId : null,
        walkInName: this.recipientMode === 'walkin' ? this.form.walkInName : null,
        walkInAge: this.form.walkInAge,
        walkInAddress: this.form.walkInAddress,
        doctorId: this.form.doctorId,
        issueDate: this.form.issueDate,
        template: this.form.template,
        diagnosis: this.form.diagnosis,
        recommendation: this.form.recommendation,
        remarks: this.form.remarks,
      })
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.isModalOpen = false;
          this.pageIndex = 0;
          this.load();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.modalError = err?.error?.message ?? 'Could not issue the certificate.';
          this.isSaving = false;
          this.cdr.markForCheck();
        },
      });
  }

  remove(certificate: Certificate): void {
    if (!window.confirm(`Delete certificate ${certificate.certificateNumber}?`)) return;

    this.api.delete<void>(`medicalcertificates/${certificate.id}`).subscribe({
      next: () => {
        this.load();
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not delete that certificate.';
        this.cdr.markForCheck();
      },
    });
  }

  /** Renders the certificate into the print-only sheet and opens the dialog. */
  print(certificate: Certificate): void {
    this.printing = certificate;
    this.cdr.detectChanges();

    document.body.classList.add('printing-slip');

    const cleanup = () => {
      document.body.classList.remove('printing-slip');
      this.printing = null;
      this.cdr.markForCheck();
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);
    window.print();
    setTimeout(cleanup, 1000);
  }

  templatePill(template: string): string {
    switch (template) {
      case 'Work': return 'tag tag--info';
      case 'School': return 'tag tag--success';
      default: return 'tag tag--primary';
    }
  }

  templateIcon(template: string): string {
    return this.templates.find((t) => t.id === template)?.icon ?? 'file-text';
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0])
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
