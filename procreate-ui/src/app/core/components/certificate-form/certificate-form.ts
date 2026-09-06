import {
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../services/api';
import { IconComponent } from '../icon/icon';
import { ComboOption, ComboSelectComponent } from '../combo-select/combo-select';

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

/** Enough of a patient to fix the recipient without another lookup. */
export interface CertificateRecipient {
  id: number;
  firstName: string;
  lastName: string;
  patientCode: string;
}

/**
 * The issue-certificate sheet, shared by the Medical Certificates list and the
 * patient chart.
 *
 * Given a `recipient`, the sheet issues to that patient only: the search box
 * becomes a read-only field and the Express Walk-in tab goes away, since a
 * free-typed name would contradict the chart it was opened from.
 */
@Component({
  selector: 'app-certificate-form',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ComboSelectComponent],
  templateUrl: './certificate-form.html',
  styleUrl: './certificate-form.scss',
})
export class CertificateFormComponent implements OnInit, OnChanges {
  @Input() open = false;

  /** Set to fix the recipient; null lets the user search or take a walk-in. */
  @Input() recipient: CertificateRecipient | null = null;

  @Output() closed = new EventEmitter<void>();
  @Output() issued = new EventEmitter<void>();

  readonly templates = [
    { id: 'General', label: 'General', icon: 'file-text' },
    { id: 'Work', label: 'Work', icon: 'briefcase' },
    { id: 'School', label: 'School', icon: 'graduation-cap' },
  ];

  doctors: DoctorOption[] = [];
  isSaving = false;
  modalError = '';

  /** 'registered' issues against a patient record; 'walkin' takes a free name. */
  recipientMode: 'registered' | 'walkin' = 'registered';

  form = this.blankForm();

  patientQuery = '';
  patientResults: PatientOption[] = [];
  private patientSearch$ = new Subject<string>();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.patientSearch$
      .pipe(debounceTime(250), distinctUntilChanged())
      .subscribe((term) => this.runPatientSearch(term));

    this.loadDoctors();
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset on the way open rather than on the way closed, so a reopened sheet
    // never shows what was typed into it last time.
    if (changes['open']?.currentValue === true) this.reset();
  }

  get isLocked(): boolean {
    return this.recipient !== null;
  }

  private reset(): void {
    this.form = this.blankForm();
    this.form.doctorId = this.doctors[0]?.id ?? 0;
    this.recipientMode = 'registered';
    this.patientResults = [];
    this.modalError = '';

    if (this.recipient) {
      this.form.patientId = this.recipient.id;
      this.patientQuery = this.recipientLabel(this.recipient);
    } else {
      this.patientQuery = '';
    }
  }

  private recipientLabel(patient: CertificateRecipient | PatientOption): string {
    return `${patient.firstName} ${patient.lastName} · ${patient.patientCode}`;
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

  private loadDoctors(): void {
    this.api
      .get<{ data: DoctorOption[] }>('doctors', { isActive: true, pageSize: 100 })
      .subscribe({
        next: (res) => {
          this.doctors = res.data;
          // The sheet can be opened before this call lands, which would leave
          // the physician combo empty.
          if (this.open && !this.form.doctorId) {
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

  close(): void {
    this.modalError = '';
    this.closed.emit();
  }

  setRecipientMode(mode: 'registered' | 'walkin'): void {
    if (this.isLocked) return;

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
    this.patientQuery = this.recipientLabel(patient);
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
      .post<unknown>('medicalcertificates', {
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
          this.issued.emit();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.modalError = err?.error?.message ?? 'Could not issue the certificate.';
          this.isSaving = false;
          this.cdr.markForCheck();
        },
      });
  }
}
