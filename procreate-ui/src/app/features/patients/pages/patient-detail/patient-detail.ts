import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { ConfirmService } from '../../../../core/services/confirm';
import { AuthService } from '../../../../core/services/auth';
import { ToastService } from '../../../../core/services/toast';
import { CertificateRecipient } from '../../../../core/components/certificate-form/certificate-form';
import { DeliveryPatient } from '../../../../core/components/result-delivery/result-delivery';

/** A file staged in the upload modal, with an object-URL preview. */
export interface UploadItem {
  file: File;
  url: string;
  kind: 'image' | 'video' | 'file';
}

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

/** One coded diagnosis row on a SOAP note. */
export interface SoapDiagnosis {
  description: string;
  icd10: string;
}

export interface SoapNote {
  id: number;
  /**
   * Which editor wrote it. Kept because the API still stores it and older
   * notes read 'Simple'; every note written now is a full one.
   */
  mode: 'Simple' | 'Advanced';
  clinic: string;
  noteDate: string;
  chiefComplaint: string;
  historyOfPresentIllness: string;
  objectiveRemarks: string;
  assessment: string;
  plan: string;
  diagnoses: SoapDiagnosis[];
  createdBy: string;
  createdAt: string;
}

export interface PatientChart {
  patient: ChartPatient;
  allergies: Allergy[];
  medications: Medication[];
  conditions: Condition[];
  vitals: VitalSigns[];
  documents: ResultDocument[];
  soapNotes: SoapNote[];
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

  /** The issue-certificate sheet, opened over the chart for this patient. */
  isCertificateOpen = false;

  /** The send/print-results sheet. Both hero buttons open it, on different tabs. */
  isDeliveryOpen = false;
  deliveryMode: 'email' | 'print' = 'email';

  /** The order-lab-tests sheet, opened over the chart for this patient. */
  isOrderOpen = false;

  // --- Editors ---
  editor: Editor = null;
  isSaving = false;
  editorError = '';

  allergyDraft: Allergy[] = [];
  medicationDraft: Medication[] = [];
  conditionDraft: Condition[] = [];

  /**
   * Allergies and Current Medications are hidden for now at the client's
   * request. Flip this back to true to bring both cards back — the editors and
   * their API remain in place.
   */
  readonly showAllergiesAndMeds = false;


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
  /** Files chosen in the upload modal, each with a preview. */
  uploadItems: UploadItem[] = [];
  /** How far through a multi-file upload we are, for the button label. */
  uploadProgress = '';

  readonly departments = ['Laboratory', 'Imaging', 'Ultrasound', 'Heart Station'];
  readonly severities = ['', 'Mild', 'Moderate', 'Severe'];

  /** Department filter on the lab history list. */
  labFilter = 'All';

  // --- Card pagination (5 records per page, then paged) ---------------------
  readonly pageSize = 5;
  labPage = 0;
  soapPage = 0;
  historyPage = 0;
  vitalsPage = 0;
  docsPage = 0;

  /**
   * The visible slice of each history card. Held as fields and rebuilt on data
   * or page changes — a getter that sliced on every change-detection pass would
   * hand *ngFor a fresh array each time and, under zoneless CD, never settle.
   */
  pagedLabHistory: LabHistoryEntry[] = [];
  pagedSoapNotes: SoapNote[] = [];
  pagedConditions: Condition[] = [];
  pagedOlderVitals: VitalSigns[] = [];
  pagedDocuments: ResultDocument[] = [];

  constructor(
    private apiService: ApiService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private confirm: ConfirmService
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
        this.rebuildPages();
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

  /**
   * Opens the certificate sheet over the chart, fixed to this patient.
   *
   * It used to navigate to the Medical Certificates list to do this, which
   * meant losing the chart you were reading to issue a certificate for the
   * patient whose chart it was.
   */
  issueCertificate(): void {
    this.isCertificateOpen = true;
  }

  closeCertificate(): void {
    this.isCertificateOpen = false;
    // Zoneless: the emit that got us here came out of the child's HTTP
    // callback, so nothing has marked this view dirty.
    this.cdr.markForCheck();
  }

  /** Fixes the sheet's recipient to the chart being read. */
  get certificateRecipient(): CertificateRecipient | null {
    const patient = this.chart?.patient;
    if (!patient) return null;

    return {
      id: patient.id,
      firstName: patient.firstName,
      lastName: patient.lastName,
      patientCode: patient.patientCode,
    };
  }

  onCertificateIssued(): void {
    this.isCertificateOpen = false;
    this.toast.success('Medical certificate issued.');
    this.cdr.markForCheck();
  }

  // ----------------------------------------------------------
  // Send / print results
  // ----------------------------------------------------------

  openDelivery(mode: 'email' | 'print'): void {
    this.deliveryMode = mode;
    this.isDeliveryOpen = true;
  }

  closeDelivery(): void {
    this.isDeliveryOpen = false;
    // Zoneless: the emit came out of the child, which has not marked this view.
    this.cdr.markForCheck();
  }

  onDelivered(message: string): void {
    this.isDeliveryOpen = false;
    this.toast.success(message);
    this.cdr.markForCheck();
  }

  /** Enough of the patient to address a delivery, without a second lookup. */
  get deliveryPatient(): DeliveryPatient | null {
    const patient = this.chart?.patient;
    if (!patient) return null;

    return {
      id: patient.id,
      patientCode: patient.patientCode,
      name: patient.fullName,
      email: patient.email,
    };
  }

  // ----------------------------------------------------------
  // Ordering lab tests (shared picker, over the chart)
  // ----------------------------------------------------------

  openOrderTests(): void {
    if (this.chart?.patient) this.isOrderOpen = true;
  }

  closeOrderTests(): void {
    this.isOrderOpen = false;
    this.cdr.markForCheck();
  }

  onTestsOrdered(result: { visitCode: string; count: number; total: number }): void {
    this.isOrderOpen = false;
    this.toast.success(`Ordered ${result.count} test${result.count === 1 ? '' : 's'}.`);
    this.cdr.markForCheck();
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
    this.clearUploadItems();
    this.uploadProgress = '';
    this.openEditor('upload');
  }

  private openEditor(editor: Editor): void {
    this.editorError = '';
    this.editor = editor;
  }

  closeEditor(): void {
    this.clearUploadItems();
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

  // ----------------------------------------------------------
  // SOAP notes
  // ----------------------------------------------------------

  /**
   * Writing a note is a page, not a sheet over the chart. A consultation note
   * is the longest thing anybody types in here, and a modal gave it a few
   * hundred pixels of scrolling window to do it in.
   */
  openSoapNote(): void {
    this.router.navigate(['/app/patients', this.patientId, 'soap', 'new']);
  }

  editSoapNote(note: SoapNote): void {
    this.router.navigate(['/app/patients', this.patientId, 'soap', note.id]);
  }

  async deleteSoapNote(note: SoapNote): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Delete SOAP note',
      message: `Delete the note from ${new Date(note.noteDate).toLocaleDateString()}?`,
      detail: 'This cannot be undone.',
      confirmLabel: 'Delete',
    });
    if (!confirmed) return;

    this.apiService.delete<void>(`patients/${this.patientId}/soap-notes/${note.id}`).subscribe({
      next: () => {
        this.loadChart();
        this.cdr.markForCheck();
      },
      error: () => {
        this.toast.error('Failed to delete the note. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  /** A short one-line summary for the collapsed note row on the chart. */
  soapSummary(note: SoapNote): string {
    const parts = [note.chiefComplaint, note.assessment, note.plan]
      .map((p) => (p ?? '').trim())
      .filter(Boolean);
    return parts.length ? parts.join(' · ') : 'No details recorded';
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

  async deleteVitals(record: VitalSigns): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Remove vital signs',
      message: 'Remove this set of vital signs?',
      confirmLabel: 'Remove',
    });
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
    const files = Array.from(input.files ?? []);
    this.editorError = '';
    // Append rather than replace, so a second pick adds to the batch; each file
    // gets an object URL for an inline preview, revoked when the modal closes.
    for (const file of files) {
      const url = URL.createObjectURL(file);
      const kind: UploadItem['kind'] = file.type.startsWith('image/')
        ? 'image'
        : file.type.startsWith('video/') || /\.(mp4|webm|mov)$/i.test(file.name)
          ? 'video'
          : 'file';
      this.uploadItems.push({ file, url, kind });
    }
    // Let the same file be re-picked later if removed.
    input.value = '';
  }

  removeUploadItem(index: number): void {
    const [removed] = this.uploadItems.splice(index, 1);
    if (removed) URL.revokeObjectURL(removed.url);
  }

  private clearUploadItems(): void {
    for (const item of this.uploadItems) URL.revokeObjectURL(item.url);
    this.uploadItems = [];
  }

  uploadResult(): void {
    if (this.uploadItems.length === 0) {
      this.editorError = 'Choose at least one file to upload.';
      return;
    }

    const items = [...this.uploadItems];
    this.isSaving = true;
    this.editorError = '';

    // Uploaded one at a time so a single oversized/rejected file reports which
    // one failed rather than failing the whole batch opaquely.
    const uploadAt = (i: number): void => {
      if (i >= items.length) {
        this.isSaving = false;
        this.uploadProgress = '';
        this.clearUploadItems();
        this.closeEditor();
        this.loadChart();
        this.cdr.markForCheck();
        return;
      }

      this.uploadProgress = items.length > 1 ? `${i + 1} of ${items.length}` : '';
      this.cdr.markForCheck();

      // FormData goes through untouched: HttpClient sets the multipart boundary
      // itself, which it cannot do if a Content-Type is set for it.
      const form = new FormData();
      form.append('file', items[i].file);
      form.append('department', this.uploadDraft.department);
      form.append('resultDate', this.uploadDraft.resultDate);
      form.append('uploadedBy', this.auth.currentUser?.fullName ?? '');

      this.apiService.post<void>(`patients/${this.patientId}/documents`, form).subscribe({
        next: () => uploadAt(i + 1),
        error: (err) => {
          this.isSaving = false;
          this.uploadProgress = '';
          const which = items.length > 1 ? ` (${items[i].file.name})` : '';
          this.editorError = (err?.error?.message ?? 'The upload failed. Please try again.') + which;
          // Keep already-uploaded files out of the list so a retry won't double them.
          this.loadChart();
          this.cdr.markForCheck();
        },
      });
    };

    uploadAt(0);
  }

  documentUrl(document: ResultDocument): string {
    return this.apiService.url(`patients/${this.patientId}/documents/${document.id}`);
  }

  viewDocument(document: ResultDocument): void {
    window.open(this.documentUrl(document), '_blank');
  }

  async deleteDocument(document: ResultDocument): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Delete file',
      message: `Delete "${document.fileName}"?`,
      detail: 'This cannot be undone.',
      confirmLabel: 'Delete',
    });
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
    // A narrower filter has fewer pages; jump back to the first and reslice.
    this.labPage = 0;
    this.rebuildPages();
  }

  // ----------------------------------------------------------
  // Card pagination
  // ----------------------------------------------------------

  /** Pages needed to show `total` records, 5 at a time (at least 1). */
  pageCount(total: number): number {
    return Math.max(1, Math.ceil(total / this.pageSize));
  }

  /** Step a card's page by ±1, clamped, and reslice. */
  pageNav(card: 'lab' | 'soap' | 'history' | 'vitals' | 'docs', delta: number): void {
    switch (card) {
      case 'lab':
        this.labPage = this.clampPage(this.labPage + delta, this.labHistory.length);
        break;
      case 'soap':
        this.soapPage = this.clampPage(this.soapPage + delta, this.chart?.soapNotes.length ?? 0);
        break;
      case 'history':
        this.historyPage = this.clampPage(this.historyPage + delta, this.chart?.conditions.length ?? 0);
        break;
      case 'vitals':
        this.vitalsPage = this.clampPage(this.vitalsPage + delta, this.olderVitals.length);
        break;
      case 'docs':
        this.docsPage = this.clampPage(this.docsPage + delta, this.chart?.documents.length ?? 0);
        break;
    }
    this.rebuildPages();
    this.cdr.markForCheck();
  }

  private clampPage(page: number, total: number): number {
    return Math.min(this.pageCount(total) - 1, Math.max(0, page));
  }

  private sliceFor<T>(list: T[], page: number): T[] {
    const start = page * this.pageSize;
    return list.slice(start, start + this.pageSize);
  }

  /** Recompute every card's visible slice, clamping pages to the current data. */
  private rebuildPages(): void {
    const lab = this.labHistory;
    this.labPage = this.clampPage(this.labPage, lab.length);
    this.pagedLabHistory = this.sliceFor(lab, this.labPage);

    const soap = this.chart?.soapNotes ?? [];
    this.soapPage = this.clampPage(this.soapPage, soap.length);
    this.pagedSoapNotes = this.sliceFor(soap, this.soapPage);

    const conditions = this.chart?.conditions ?? [];
    this.historyPage = this.clampPage(this.historyPage, conditions.length);
    this.pagedConditions = this.sliceFor(conditions, this.historyPage);

    const older = this.olderVitals;
    this.vitalsPage = this.clampPage(this.vitalsPage, older.length);
    this.pagedOlderVitals = this.sliceFor(older, this.vitalsPage);

    const docs = this.chart?.documents ?? [];
    this.docsPage = this.clampPage(this.docsPage, docs.length);
    this.pagedDocuments = this.sliceFor(docs, this.docsPage);
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
