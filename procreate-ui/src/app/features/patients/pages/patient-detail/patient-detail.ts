import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { AuthService } from '../../../../core/services/auth';

export interface ChartPatient {
  id: number;
  patientCode: string;
  fullName: string;
  firstName: string;
  middleName?: string;
  lastName: string;
  suffix?: string;
  dateOfBirth: string;
  age: number;
  gender: string;
  civilStatus?: string;
  occupation?: string;
  nationality?: string;
  bloodType?: string;
  contactNumber?: string;
  landline?: string;
  email?: string;
  address?: string;
  emergencyContactName?: string;
  emergencyContactRelationship?: string;
  emergencyContactNumber?: string;
  philHealthNumber?: string;
  seniorCitizenId?: string;
  pwdId?: string;
  hmoProvider?: string;
  hmoAccountNumber?: string;
  createdAt: string;
}

export interface Allergy {
  id?: number;
  substance: string;
  severity?: string;
  reaction?: string;
}

export interface Medication {
  id?: number;
  name: string;
  dosage?: string;
  frequency?: string;
  notes?: string;
}

export interface Condition {
  id?: number;
  condition: string;
  diagnosedOn?: string;
  notes?: string;
}

export interface VitalSigns {
  id: number;
  recordedAt: string;
  recordedBy?: string;
  systolicBp?: number | null;
  diastolicBp?: number | null;
  heartRate?: number | null;
  respiratoryRate?: number | null;
  temperatureC?: number | null;
  weightKg?: number | null;
  heightCm?: number | null;
  oxygenSaturation?: number | null;
  notes?: string;
}

export interface ResultDocument {
  id: number;
  department: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  resultDate: string;
  uploadedBy?: string;
  uploadedAt: string;
}

export interface LabHistoryEntry {
  id: number;
  orderCode: string;
  department: string;
  categoryName: string;
  testName: string;
  status: string;
  stage: string;
  isAbnormal: boolean;
  resultKind: string;
  resultDate: string;
  visitCode: string;
}

export interface PatientChart {
  patient: ChartPatient;
  allergies: Allergy[];
  medications: Medication[];
  conditions: Condition[];
  vitals: VitalSigns[];
  documents: ResultDocument[];
  labHistory: LabHistoryEntry[];
}

/** Which editor sheet is open, if any. */
type Editor = 'allergies' | 'medications' | 'conditions' | 'vitals' | 'upload' | null;

@Component({
  selector: 'app-patient-detail',
  standalone: false,
  templateUrl: './patient-detail.html',
  styleUrl: './patient-detail.scss',
})
export class PatientDetailComponent implements OnInit {
  patientId!: number;
  chart: PatientChart | null = null;
  isLoading = true;
  errorMessage = '';

  /** Demographic edit reuses the registration sheet from the patient list. */
  isFormOpen = false;

  // --- Editors ---
  editor: Editor = null;
  isSaving = false;
  editorError = '';

  allergyDraft: Allergy[] = [];
  medicationDraft: Medication[] = [];
  conditionDraft: Condition[] = [];

  vitalsDraft = {
    systolicBp: null as number | null,
    diastolicBp: null as number | null,
    heartRate: null as number | null,
    respiratoryRate: null as number | null,
    temperatureC: null as number | null,
    weightKg: null as number | null,
    heightCm: null as number | null,
    oxygenSaturation: null as number | null,
    notes: '',
  };

  uploadDraft = {
    department: 'Laboratory',
    resultDate: new Date().toISOString().slice(0, 10),
  };
  uploadFile: File | null = null;

  readonly departments = ['Laboratory', 'Imaging', 'Ultrasound', 'Heart Station'];
  readonly severities = ['', 'Mild', 'Moderate', 'Severe'];

  /** Department filter on the lab history list. */
  labFilter = 'All';

  constructor(
    private apiService: ApiService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.patientId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadChart();
  }

  loadChart(): void {
    this.isLoading = true;
    this.apiService.get<PatientChart>(`patients/${this.patientId}/chart`).subscribe({
      next: (chart) => {
        this.chart = chart;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'This patient could not be found.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Navigation
  // ----------------------------------------------------------

  back(): void {
    this.router.navigate(['/app/patients']);
  }

  /** Opens the certificate sheet with this patient already selected. */
  issueCertificate(): void {
    this.router.navigate(['/app/medical-records/certificates'], {
      queryParams: { patientId: this.patientId },
    });
  }

  editPatient(): void {
    this.isFormOpen = true;
  }

  onFormSaved(): void {
    this.isFormOpen = false;
    this.loadChart();
  }

  onFormCancelled(): void {
    this.isFormOpen = false;
  }

  openOrder(entry: LabHistoryEntry): void {
    this.router.navigate(['/app/lab-results', entry.id]);
  }

  // ----------------------------------------------------------
  // Editors
  // ----------------------------------------------------------

  /**
   * Editors work on a copy. Saving replaces the whole list server-side, so
   * cancelling has to leave the loaded chart untouched.
   */
  openAllergies(): void {
    this.allergyDraft = (this.chart?.allergies ?? []).map((a) => ({ ...a }));
    if (this.allergyDraft.length === 0) this.addAllergyRow();
    this.openEditor('allergies');
  }

  openMedications(): void {
    this.medicationDraft = (this.chart?.medications ?? []).map((m) => ({ ...m }));
    if (this.medicationDraft.length === 0) this.addMedicationRow();
    this.openEditor('medications');
  }

  openConditions(): void {
    this.conditionDraft = (this.chart?.conditions ?? []).map((c) => ({ ...c }));
    if (this.conditionDraft.length === 0) this.addConditionRow();
    this.openEditor('conditions');
  }

  openVitals(): void {
    this.vitalsDraft = {
      systolicBp: null,
      diastolicBp: null,
      heartRate: null,
      respiratoryRate: null,
      temperatureC: null,
      weightKg: null,
      heightCm: null,
      oxygenSaturation: null,
      notes: '',
    };
    this.openEditor('vitals');
  }

  openUpload(): void {
    this.uploadDraft = {
      department: 'Laboratory',
      resultDate: new Date().toISOString().slice(0, 10),
    };
    this.uploadFile = null;
    this.openEditor('upload');
  }

  private openEditor(editor: Editor): void {
    this.editorError = '';
    this.editor = editor;
  }

  closeEditor(): void {
    this.editor = null;
    this.editorError = '';
    this.isSaving = false;
  }

  addAllergyRow(): void {
    this.allergyDraft.push({ substance: '', severity: '', reaction: '' });
  }

  removeAllergyRow(index: number): void {
    this.allergyDraft.splice(index, 1);
  }

  addMedicationRow(): void {
    this.medicationDraft.push({ name: '', dosage: '', frequency: '', notes: '' });
  }

  removeMedicationRow(index: number): void {
    this.medicationDraft.splice(index, 1);
  }

  addConditionRow(): void {
    this.conditionDraft.push({ condition: '', diagnosedOn: '', notes: '' });
  }

  removeConditionRow(index: number): void {
    this.conditionDraft.splice(index, 1);
  }

  saveAllergies(): void {
    const items = this.allergyDraft
      .filter((a) => a.substance.trim())
      .map((a) => ({
        substance: a.substance.trim(),
        severity: a.severity ?? '',
        reaction: a.reaction ?? '',
      }));

    this.saveList(`patients/${this.patientId}/allergies`, { items });
  }

  saveMedications(): void {
    const items = this.medicationDraft
      .filter((m) => m.name.trim())
      .map((m) => ({
        name: m.name.trim(),
        dosage: m.dosage ?? '',
        frequency: m.frequency ?? '',
        notes: m.notes ?? '',
      }));

    this.saveList(`patients/${this.patientId}/medications`, { items });
  }

  saveConditions(): void {
    const items = this.conditionDraft
      .filter((c) => c.condition.trim())
      .map((c) => ({
        condition: c.condition.trim(),
        diagnosedOn: c.diagnosedOn ?? '',
        notes: c.notes ?? '',
      }));

    this.saveList(`patients/${this.patientId}/conditions`, { items });
  }

  private saveList(path: string, body: unknown): void {
    this.isSaving = true;
    this.apiService.put<void>(path, body).subscribe({
      next: () => {
        this.isSaving = false;
        this.closeEditor();
        this.loadChart();
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSaving = false;
        this.editorError = 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  saveVitals(): void {
    const draft = this.vitalsDraft;
    const hasReading = [
      draft.systolicBp,
      draft.diastolicBp,
      draft.heartRate,
      draft.respiratoryRate,
      draft.temperatureC,
      draft.weightKg,
      draft.heightCm,
      draft.oxygenSaturation,
    ].some((value) => value !== null && value !== undefined && `${value}` !== '');

    if (!hasReading) {
      this.editorError = 'Record at least one measurement.';
      return;
    }

    this.isSaving = true;
    this.apiService
      .post<void>(`patients/${this.patientId}/vitals`, {
        ...draft,
        recordedBy: this.auth.currentUser?.fullName ?? '',
      })
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.closeEditor();
          this.loadChart();
          this.cdr.markForCheck();
        },
        error: () => {
          this.isSaving = false;
          this.editorError = 'Saving failed. Please try again.';
          this.cdr.markForCheck();
        },
      });
  }

  deleteVitals(record: VitalSigns): void {
    const confirmed = window.confirm('Remove this set of vital signs?');
    if (!confirmed) return;

    this.apiService.delete<void>(`patients/${this.patientId}/vitals/${record.id}`).subscribe({
      next: () => {
        this.loadChart();
        this.cdr.markForCheck();
      },
      error: () => {
        window.alert('Failed to remove the record. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Result files
  // ----------------------------------------------------------

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.uploadFile = input.files?.length ? input.files[0] : null;
    this.editorError = '';
  }

  uploadResult(): void {
    if (!this.uploadFile) {
      this.editorError = 'Choose a file to upload.';
      return;
    }

    // FormData goes through untouched: HttpClient sets the multipart boundary
    // itself, which it cannot do if a Content-Type is set for it.
    const form = new FormData();
    form.append('file', this.uploadFile);
    form.append('department', this.uploadDraft.department);
    form.append('resultDate', this.uploadDraft.resultDate);
    form.append('uploadedBy', this.auth.currentUser?.fullName ?? '');

    this.isSaving = true;
    this.apiService.post<void>(`patients/${this.patientId}/documents`, form).subscribe({
      next: () => {
        this.isSaving = false;
        this.closeEditor();
        this.loadChart();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.editorError = err?.error?.message ?? 'The upload failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  documentUrl(document: ResultDocument): string {
    return this.apiService.url(`patients/${this.patientId}/documents/${document.id}`);
  }

  viewDocument(document: ResultDocument): void {
    window.open(this.documentUrl(document), '_blank');
  }

  deleteDocument(document: ResultDocument): void {
    const confirmed = window.confirm(`Delete "${document.fileName}"? This cannot be undone.`);
    if (!confirmed) return;

    this.apiService
      .delete<void>(`patients/${this.patientId}/documents/${document.id}`)
      .subscribe({
        next: () => {
          this.loadChart();
          this.cdr.markForCheck();
        },
        error: () => {
          window.alert('Failed to delete the file. Please try again.');
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  get labHistory(): LabHistoryEntry[] {
    const history = this.chart?.labHistory ?? [];
    return this.labFilter === 'All'
      ? history
      : history.filter((entry) => entry.department === this.labFilter);
  }

  onLabFilter(filter: string): void {
    this.labFilter = filter;
  }

  /** The most recent vitals, shown expanded above the rest. */
  get latestVitals(): VitalSigns | null {
    return this.chart?.vitals?.length ? this.chart.vitals[0] : null;
  }

  get olderVitals(): VitalSigns[] {
    return (this.chart?.vitals ?? []).slice(1);
  }

  bloodPressure(record: VitalSigns): string {
    if (record.systolicBp == null && record.diastolicBp == null) return '—';
    return `${record.systolicBp ?? '—'}/${record.diastolicBp ?? '—'}`;
  }

  fileExtension(fileName: string): string {
    const dot = fileName.lastIndexOf('.');
    return dot > -1 ? fileName.slice(dot + 1).toUpperCase() : 'FILE';
  }

  fileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  severityPill(severity?: string): string {
    switch (severity) {
      case 'Severe':
        return 'pill pill--danger';
      case 'Moderate':
        return 'pill pill--warning';
      case 'Mild':
        return 'pill pill--info';
      default:
        return 'pill';
    }
  }

  stagePill(stage: string): string {
    switch (stage) {
      case 'Pending':
        return 'pill pill--warning';
      case 'In Progress':
        return 'pill pill--info';
      case 'For Reading':
        return 'pill pill--primary';
      case 'Completed':
        return 'pill pill--success';
      default:
        return 'pill';
    }
  }

  departmentIcon(department: string): string {
    switch (department) {
      case 'Imaging':
        return 'scan';
      case 'Ultrasound':
        return 'waves';
      case 'Heart Station':
        return 'activity';
      default:
        return 'flask';
    }
  }

  departmentTag(department: string): string {
    switch (department) {
      case 'Imaging':
        return 'tag tag--info';
      case 'Ultrasound':
        return 'tag tag--primary';
      case 'Heart Station':
        return 'tag tag--danger';
      default:
        return 'tag tag--success';
    }
  }

  initials(name: string): string {
    return (name ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  /** Whether the patient has any statutory discount or insurance on file. */
  get hasInsurance(): boolean {
    const p = this.chart?.patient;
    if (!p) return false;
    return !!(p.philHealthNumber || p.seniorCitizenId || p.pwdId || p.hmoProvider);
  }
}
