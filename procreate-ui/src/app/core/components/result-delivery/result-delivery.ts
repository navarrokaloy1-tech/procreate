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
import { ApiService } from '../../services/api';
import { IconComponent } from '../icon/icon';

/** Enough of a patient to address the delivery without another lookup. */
export interface DeliveryPatient {
  id: number;
  patientCode: string;
  name: string;
  email?: string;
}

interface DoctorOption {
  id: number;
  fullName: string;
  specialty: string;
  prcLicenseNumber?: string;
}

interface AvailableResult {
  id: number;
  label: string;
  department: string;
  releasedAt: string;
  isAbnormal: boolean;
  resultKind: string;
}

interface AvailableDocument {
  id: number;
  label: string;
  department: string;
  sizeBytes: number;
  resultDate: string;
}

interface AvailableResponse {
  patient: { id: number; patientCode: string; name: string; email: string };
  labResults: AvailableResult[];
  documents: AvailableDocument[];
}

interface PackageParameter {
  parameterName: string;
  unit: string;
  reference: string;
  value: string;
  flag: string;
}

interface PackageResult {
  id: number;
  testName: string;
  department: string;
  categoryName: string;
  specimen: string;
  method: string;
  releasedAt: string;
  isAbnormal: boolean;
  narrativeFindings: string;
  resultedBy: string;
  resultKind: string;
  parameters: PackageParameter[];
}

export interface DeliveryPackage {
  patient: {
    id: number;
    patientCode: string;
    name: string;
    gender: string;
    dateOfBirth: string;
    age: number;
    email: string;
  };
  doctor: {
    id: number;
    name: string;
    specialty: string;
    prcLicenseNumber: string;
    hasSignature: boolean;
  };
  includeSignature: boolean;
  results: PackageResult[];
  documents: { id: number; fileName: string; department: string; sizeBytes: number; resultDate: string }[];
  generatedAt: string;
}

/** One row of the results picker. Blank until something is chosen. */
interface PickerRow {
  /** "lab:12" or "doc:4" — the kind has to travel with the id, they overlap. */
  key: string;
}

/**
 * Sending or printing a patient's results.
 *
 * Only released results are offered. An unreleased one has not been read by a
 * doctor, and the release step exists precisely to stop it reaching a patient.
 */
@Component({
  selector: 'app-result-delivery',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './result-delivery.html',
  styleUrl: './result-delivery.scss',
})
export class ResultDeliveryComponent implements OnInit, OnChanges {
  @Input() open = false;
  @Input() patient: DeliveryPatient | null = null;
  /** Which tab the sheet opens on — the two buttons share this component. */
  @Input() mode: 'email' | 'print' = 'email';

  @Output() closed = new EventEmitter<void>();
  @Output() delivered = new EventEmitter<string>();

  doctors: DoctorOption[] = [];
  labResults: AvailableResult[] = [];
  documents: AvailableDocument[] = [];

  rows: PickerRow[] = [{ key: '' }];

  doctorId: number | '' = '';
  includeSignature = true;
  toAddress = '';
  message = '';

  isLoading = false;
  isWorking = false;
  errorMessage = '';

  /** The assembled sheet, shown in the preview panel and used for printing. */
  preview: DeliveryPackage | null = null;
  printPackage: DeliveryPackage | null = null;

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadDoctors();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue === true) this.reset();
  }

  private reset(): void {
    this.rows = [{ key: '' }];
    this.doctorId = this.doctors[0]?.id ?? '';
    this.includeSignature = true;
    this.toAddress = this.patient?.email ?? '';
    this.message = '';
    this.errorMessage = '';
    this.preview = null;
    this.loadAvailable();
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  private loadDoctors(): void {
    this.api
      .get<{ data: DoctorOption[] }>('doctors', { isActive: true, pageSize: 100 })
      .subscribe({
        next: (res) => {
          this.doctors = res.data ?? [];
          if (this.open && !this.doctorId) this.doctorId = this.doctors[0]?.id ?? '';
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  private loadAvailable(): void {
    if (!this.patient) return;

    this.isLoading = true;

    this.api
      .get<AvailableResponse>(`patients/${this.patient.id}/result-delivery/available`)
      .subscribe({
        next: (res) => {
          this.labResults = res.labResults ?? [];
          this.documents = res.documents ?? [];
          if (!this.toAddress) this.toAddress = res.patient?.email ?? '';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load this patient’s results.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // The picker
  // ----------------------------------------------------------

  addRow(): void {
    this.rows = [...this.rows, { key: '' }];
  }

  removeRow(index: number): void {
    this.rows = this.rows.filter((_, i) => i !== index);
    if (this.rows.length === 0) this.rows = [{ key: '' }];
    this.preview = null;
  }

  onRowChange(index: number, key: string): void {
    this.rows[index].key = key;
    this.preview = null;
  }

  /** Already picked elsewhere, so it should not be offered twice. */
  isTaken(key: string, index: number): boolean {
    return this.rows.some((row, i) => i !== index && row.key === key);
  }

  private keys(): string[] {
    return this.rows.map((r) => r.key).filter(Boolean);
  }

  get selectedLabIds(): number[] {
    return this.keys().filter((k) => k.startsWith('lab:')).map((k) => Number(k.slice(4)));
  }

  get selectedDocumentIds(): number[] {
    return this.keys().filter((k) => k.startsWith('doc:')).map((k) => Number(k.slice(4)));
  }

  get hasSelection(): boolean {
    return this.keys().length > 0;
  }

  get nothingAvailable(): boolean {
    return !this.isLoading && this.labResults.length === 0 && this.documents.length === 0;
  }

  /**
   * Whether the patient's record carries an address.
   *
   * When it does, the field is fixed to it: results are personal medical
   * information, and letting anyone type a destination over the top turns one
   * mistyped character into a disclosure. Correcting it means correcting the
   * chart, which is where it should be corrected anyway. The server applies
   * the same rule, so the disabled field is not the only thing enforcing it.
   */
  get hasEmailOnFile(): boolean {
    return !!this.patient?.email?.trim();
  }

  // ----------------------------------------------------------
  // Actions
  // ----------------------------------------------------------

  close(): void {
    this.errorMessage = '';
    this.preview = null;
    this.closed.emit();
  }

  setMode(mode: 'email' | 'print'): void {
    this.mode = mode;
    this.errorMessage = '';
  }

  private validate(): boolean {
    if (!this.hasSelection) {
      this.errorMessage = 'Choose at least one result to include.';
      return false;
    }
    if (!this.doctorId) {
      this.errorMessage = 'Choose an attending doctor.';
      return false;
    }
    return true;
  }

  private packageBody() {
    return {
      labOrderIds: this.selectedLabIds,
      documentIds: this.selectedDocumentIds,
      doctorId: Number(this.doctorId),
      includeSignature: this.includeSignature,
    };
  }

  showPreview(): void {
    if (!this.patient || !this.validate()) return;

    this.isWorking = true;
    this.errorMessage = '';

    this.api
      .post<DeliveryPackage>(`patients/${this.patient.id}/result-delivery/package`, this.packageBody())
      .subscribe({
        next: (pkg) => {
          this.preview = pkg;
          this.isWorking = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Could not build the preview.';
          this.isWorking = false;
          this.cdr.markForCheck();
        },
      });
  }

  hidePreview(): void {
    this.preview = null;
  }

  send(): void {
    if (!this.patient || !this.validate()) return;

    if (!this.toAddress.trim()) {
      this.errorMessage = 'Enter an email address to send to.';
      return;
    }

    this.isWorking = true;
    this.errorMessage = '';

    this.api
      .post<{ message: string }>(`patients/${this.patient.id}/result-delivery/send`, {
        ...this.packageBody(),
        toAddress: this.toAddress.trim(),
        message: this.message.trim(),
      })
      .subscribe({
        next: (res) => {
          this.isWorking = false;
          this.delivered.emit(res.message ?? 'Results sent.');
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'The results could not be sent.';
          this.isWorking = false;
          this.cdr.markForCheck();
        },
      });
  }

  /**
   * Builds the sheet, hands it to the browser's print dialog, and records that
   * a printout was produced. Recorded on the way out rather than on success:
   * the browser never reports whether the paper actually came out.
   */
  print(): void {
    if (!this.patient || !this.validate()) return;

    this.isWorking = true;
    this.errorMessage = '';

    this.api
      .post<DeliveryPackage>(`patients/${this.patient.id}/result-delivery/package`, this.packageBody())
      .subscribe({
        next: (pkg) => {
          this.isWorking = false;
          this.printPackage = pkg;
          this.cdr.detectChanges();

          document.body.classList.add('printing-slip');

          const cleanup = () => {
            document.body.classList.remove('printing-slip');
            this.printPackage = null;
            this.cdr.markForCheck();
            window.removeEventListener('afterprint', cleanup);
          };

          // The signature is fetched over the network as the sheet renders, and
          // the print dialog does not wait for it. Printing straight away gave
          // a sheet with the signature missing — the one thing it is there for.
          this.whenImagesReady(document.querySelector('.delivery-print')).then(() => {
            window.addEventListener('afterprint', cleanup);
            window.print();
            setTimeout(cleanup, 1000);
          });

          this.api
            .post<void>(`patients/${this.patient!.id}/result-delivery/print`, this.packageBody())
            .subscribe({
              next: () => this.delivered.emit('Results printed.'),
              // The paper is what matters here; a missing audit row should not
              // be reported to the user as a failed print.
              error: () => this.delivered.emit('Results printed.'),
            });
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Could not prepare the printout.';
          this.isWorking = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // Display
  // ----------------------------------------------------------

  /** The doctor's stored signature, for the preview and printed sheets. */
  signatureUrl(doctorId: number): string {
    return this.api.url(`doctors/${doctorId}/signature`);
  }

  /**
   * Resolves once every image inside `root` has finished loading, or after a
   * short grace period. Capped rather than open-ended: a signature that will
   * not load must not leave someone staring at a page that never prints.
   */
  private whenImagesReady(root: Element | null, timeoutMs = 3000): Promise<void> {
    const images = [...(root?.querySelectorAll('img') ?? [])];
    const pending = images.filter((img) => !img.complete || img.naturalWidth === 0);

    if (pending.length === 0) return Promise.resolve();

    return Promise.race([
      Promise.all(
        pending.map(
          (img) =>
            new Promise<void>((resolve) => {
              img.addEventListener('load', () => resolve(), { once: true });
              // A broken image still lets the rest of the sheet print.
              img.addEventListener('error', () => resolve(), { once: true });
            }),
        ),
      ).then(() => undefined),
      new Promise<void>((resolve) => setTimeout(resolve, timeoutMs)),
    ]);
  }

  fileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  flagClass(flag: string): string {
    if (!flag || flag === 'N') return '';
    return 'delivery-flag';
  }

  trackRow = (index: number) => index;
}
