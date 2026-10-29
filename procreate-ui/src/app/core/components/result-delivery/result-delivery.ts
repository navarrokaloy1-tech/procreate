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
import * as QRCode from 'qrcode';
import { ApiService } from '../../services/api';
import { ToastService } from '../../services/toast';
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
  categoryName: string;
  /** Every panel this test belongs to — a result groups under each of them. */
  panelNames: string[];
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
  panelName: string;
  specimen: string;
  method: string;
  collectedAt: string;
  releasedAt: string;
  specimenBarcode: string;
  referringPhysician: string;
  isAbnormal: boolean;
  narrativeFindings: string;
  resultedBy: string;
  resultKind: string;
  parameters: PackageParameter[];
}

/** The clinic letterhead, from the "Clinic" section of the API config. */
interface ClinicInfo {
  name: string;
  tagline: string;
  subTagline: string;
  dohLicenseNumber: string;
  mobileNumber: string;
  email: string;
  website: string;
}

/** One section of the sheet — a category with its tests. */
interface ResultSection {
  title: string;
  results: PackageResult[];
}

export interface DeliveryPackage {
  clinic: ClinicInfo;
  patient: {
    id: number;
    patientCode: string;
    name: string;
    gender: string;
    dateOfBirth: string;
    age: number;
    email: string;
    contactNumber: string;
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

  /** Chosen results/documents, and whether the picker dropdown is open. */
  selectedLab = new Set<number>();
  selectedDoc = new Set<number>();
  pickerOpen = false;

  doctorId: number | '' = '';
  includeSignature = true;
  toAddress = '';
  message = '';

  isLoading = false;
  isWorking = false;
  /** Narrower than isWorking: only the email send, which blocks the sheet. */
  isSending = false;
  errorMessage = '';

  /** The assembled sheet, shown in the preview panel and used for printing. */
  preview: DeliveryPackage | null = null;
  printPackage: DeliveryPackage | null = null;

  /** QR of the patient code, drawn top-right of the sheet for staff to scan. */
  qrDataUrl = '';

  constructor(private api: ApiService, private cdr: ChangeDetectorRef, private toast: ToastService) {}

  ngOnInit(): void {
    this.loadDoctors();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue === true) this.reset();
  }

  private reset(): void {
    this.selectedLab = new Set<number>();
    this.selectedDoc = new Set<number>();
    this.pickerOpen = false;
    this.doctorId = this.doctors[0]?.id ?? '';
    this.includeSignature = true;
    this.toAddress = this.patient?.email ?? '';
    this.message = '';
    this.errorMessage = '';
    this.isWorking = false;
    this.isSending = false;
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


  togglePicker(): void {
    this.pickerOpen = !this.pickerOpen;
  }

  /**
   * Released results grouped by the panels they belong to — one group (one main
   * checkbox) per panel, like the Test Panels screen. A test in several panels
   * shows under each; a result in no panel falls to its lab category. Panel
   * groups come first (alphabetical), then any category groups.
   */
  get labByCategory(): { category: string; results: AvailableResult[] }[] {
    const panelMap = new Map<string, AvailableResult[]>();
    const unpanelled: AvailableResult[] = [];

    const push = (map: Map<string, AvailableResult[]>, key: string, r: AvailableResult) => {
      const k = key.toUpperCase();
      if (!map.has(k)) map.set(k, []);
      map.get(k)!.push(r);
    };

    for (const r of this.labResults) {
      if (r.panelNames?.length) {
        for (const panel of r.panelNames) push(panelMap, panel, r);
      } else {
        unpanelled.push(r);
      }
    }

    const groups = [...panelMap.keys()]
      .sort()
      .map((category) => ({ category, results: panelMap.get(category)! }));

    // Leftovers that belong to no panel, grouped by their lab category.
    const catMap = new Map<string, AvailableResult[]>();
    const catOrder: string[] = [];
    for (const r of unpanelled) {
      const key = (r.categoryName || r.department || 'Other').toUpperCase();
      if (!catMap.has(key)) { catMap.set(key, []); catOrder.push(key); }
      catMap.get(key)!.push(r);
    }
    for (const category of catOrder) groups.push({ category, results: catMap.get(category)! });

    return groups;
  }

  toggleLab(id: number): void {
    if (this.selectedLab.has(id)) this.selectedLab.delete(id);
    else this.selectedLab.add(id);
  }

  isLabSelected(id: number): boolean {
    return this.selectedLab.has(id);
  }

  toggleDoc(id: number): void {
    if (this.selectedDoc.has(id)) this.selectedDoc.delete(id);
    else this.selectedDoc.add(id);
  }

  isDocSelected(id: number): boolean {
    return this.selectedDoc.has(id);
  }

  /** Select or clear every result in a category at once. */
  toggleCategory(group: { results: AvailableResult[] }): void {
    const all = this.categoryAllSelected(group);
    for (const r of group.results) {
      if (all) this.selectedLab.delete(r.id);
      else this.selectedLab.add(r.id);
    }
  }

  categoryAllSelected(group: { results: AvailableResult[] }): boolean {
    return group.results.length > 0 && group.results.every((r) => this.selectedLab.has(r.id));
  }

  categorySomeSelected(group: { results: AvailableResult[] }): boolean {
    return group.results.some((r) => this.selectedLab.has(r.id)) && !this.categoryAllSelected(group);
  }

  get selectedLabIds(): number[] {
    return Array.from(this.selectedLab);
  }

  get selectedDocumentIds(): number[] {
    return Array.from(this.selectedDoc);
  }

  get hasSelection(): boolean {
    return this.selectedLab.size + this.selectedDoc.size > 0;
  }

  /** Dropdown button text: a count once anything is chosen, else the prompt. */
  get selectionSummary(): string {
    const n = this.selectedLab.size + this.selectedDoc.size;
    return n === 0 ? 'Please select lab results to be sent' : `${n} selected`;
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
    // Backdrop and the X bypass the disabled footer buttons, and a half-sent
    // email should not be abandoned.
    if (this.isSending) return;

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
          this.renderQr(pkg.patient.patientCode);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'Could not build the preview.';
          this.isWorking = false;
          this.cdr.markForCheck();
        },
      });
  }

  /**
   * Draws the patient-code QR for the sheet. The same code the patient card
   * carries, so staff can scan the printed result to pull the file up. A
   * failure just leaves it off — the sheet is still valid without it.
   */
  private renderQr(patientCode: string): void {
    QRCode.toDataURL(patientCode, { errorCorrectionLevel: 'M', margin: 0, width: 220 })
      .then((url) => {
        this.qrDataUrl = url;
        this.cdr.markForCheck();
      })
      .catch(() => {
        this.qrDataUrl = '';
        this.cdr.markForCheck();
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
    this.isSending = true;
    this.errorMessage = '';
    this.cdr.markForCheck();

    this.api
      .post<{ message: string }>(`patients/${this.patient.id}/result-delivery/send`, {
        ...this.packageBody(),
        toAddress: this.toAddress.trim(),
        message: this.message.trim(),
      })
      .subscribe({
        next: (res) => {
          this.isWorking = false;
          this.isSending = false;
          this.delivered.emit(res.message ?? 'Results sent.');
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.errorMessage = err?.error?.message ?? 'The results could not be sent.';
          this.toast.error(this.errorMessage);
          this.isWorking = false;
          this.isSending = false;
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
          this.renderQr(pkg.patient.patientCode);
          this.cdr.detectChanges();

          // Print a static clone of the sheet, lifted to <body>.
          //
          // The sheet can run to more than one page, and the shared "slip"
          // trick — hide the app with visibility:hidden, overlay the artefact
          // absolutely — cannot carry a multi-page document: the hidden page
          // keeps its full height, so a long host page (the patient record is
          // several screens tall) padded the printout with blank sheets while
          // the absolute overlay only landed on page one.
          //
          // Moving the live node instead fails too: rendering the QR triggers
          // change detection, and Angular re-manages the sheet from its
          // original anchor, emptying the moved copy. So we clone a static
          // snapshot, print that alone, and drop it afterwards — Angular never
          // sees it.
          const buildAndPrint = () => {
            const source = document.querySelector('.delivery-print');
            if (!source) return;

            const clone = source.cloneNode(true) as HTMLElement;
            clone.classList.add('print-clone');
            document.body.appendChild(clone);
            document.body.classList.add('printing-sheet');

            let torndown = false;
            const cleanup = () => {
              if (torndown) return;
              torndown = true;
              window.removeEventListener('afterprint', cleanup);
              document.body.classList.remove('printing-sheet');
              clone.remove();
              this.printPackage = null;
              this.qrDataUrl = '';
              this.cdr.markForCheck();
            };

            // Wait for the clone's own images (signature, logo, QR) before
            // printing — the dialog does not wait, and a sheet missing its
            // signature is the one thing it must not be.
            this.whenImagesReady(clone).then(() => {
              window.addEventListener('afterprint', cleanup);
              window.print();
            });
          };

          // Let the QR data URL settle into the DOM first, so the clone carries
          // it. renderQr is async; a short wait keeps the snapshot complete.
          setTimeout(buildAndPrint, 60);

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

  // ----------------------------------------------------------
  // Sheet layout — shared by the preview and the printed copy
  // ----------------------------------------------------------

  /** Results grouped by the panel they were ordered as, falling back to category. */
  sections(pkg: DeliveryPackage): ResultSection[] {
    const order: string[] = [];
    const byTitle = new Map<string, PackageResult[]>();

    for (const result of pkg.results) {
      const title = (result.panelName || result.categoryName || result.department || 'Results').toUpperCase();
      if (!byTitle.has(title)) {
        byTitle.set(title, []);
        order.push(title);
      }
      byTitle.get(title)!.push(result);
    }

    return order.map((title) => ({ title, results: byTitle.get(title)! }));
  }

  /** Earliest collection across the chosen results, for the header. */
  collectedOn(pkg: DeliveryPackage): string | null {
    const dates = pkg.results.map((r) => r.collectedAt).filter(Boolean).sort();
    return dates[0] ?? null;
  }

  /** Latest release across the chosen results, for the header. */
  releasedOn(pkg: DeliveryPackage): string | null {
    const dates = pkg.results.map((r) => r.releasedAt).filter(Boolean).sort();
    return dates[dates.length - 1] ?? null;
  }

  headerBarcode(pkg: DeliveryPackage): string {
    return pkg.results.find((r) => r.specimenBarcode)?.specimenBarcode ?? '';
  }

  referringMd(pkg: DeliveryPackage): string {
    return pkg.results.find((r) => r.referringPhysician)?.referringPhysician ?? '';
  }

  /** The med-tech who resulted these, named on the left signature line. */
  medTech(pkg: DeliveryPackage): string {
    return pkg.results.find((r) => r.resultedBy)?.resultedBy ?? '';
  }

  /** The coloured marker beside an out-of-range value: ▲ high, ▼ low. */
  flagMark(flag: string): string {
    const f = (flag ?? '').trim();
    if (!f || f === 'N' || f === 'Normal') return '';
    const first = f[0].toUpperCase();
    if (first === 'L') return '▼';
    return '▲';
  }

  isAbnormalFlag(flag: string): boolean {
    const f = (flag ?? '').trim();
    return !!f && f !== 'N' && f !== 'Normal';
  }

  /** Reference range split into its lines, so multi-band ranges stack. */
  refLines(reference: string): string[] {
    return (reference || '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
  }

  trackSection = (_: number, s: ResultSection) => s.title;
  trackResult = (_: number, r: PackageResult) => r.id;
}
