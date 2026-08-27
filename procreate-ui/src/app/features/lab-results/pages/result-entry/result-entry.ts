import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { AuthService } from '../../../../core/services/auth';

export interface ResultParameter {
  id: number;
  parameterName: string;
  unit: string;
  referenceRangeMin: number | null;
  referenceRangeMax: number | null;
  referenceRangeText: string;
  value: string;
  flag: string;
}

export interface OrderPatient {
  id: number;
  patientCode: string;
  name: string;
  gender: string;
  dateOfBirth: string;
  age: number;
}

export interface LabOrderDetail {
  id: number;
  orderCode: string;
  visitCode: string;
  patientName: string;
  patient: OrderPatient;
  testName: string;
  testCode: string;
  department: string;
  categoryName: string;
  specimen: string;
  method: string;
  status: string;
  stage: string;
  specimenBarcode: string;
  narrativeFindings: string;
  isAbnormal: boolean;
  resultedBy: string;
  requestedBy: string;
  orderedDate: string;
  collectedAt: string | null;
  processedAt: string | null;
  releasedAt: string | null;
  /** 'parameters' for measured tests, 'narrative' for studies that are read. */
  resultKind: string;
  parameters: ResultParameter[];
}

@Component({
  selector: 'app-result-entry',
  standalone: false,
  templateUrl: './result-entry.html',
  styleUrl: './result-entry.scss',
})
export class ResultEntry implements OnInit {
  orderId!: number;
  order: LabOrderDetail | null = null;
  isLoading = true;
  isSaving = false;
  isReleasing = false;
  errorMessage = '';

  resultValues: { [parameterId: number]: string } = {};
  remarks = '';

  /** Free-text write-up, for studies with no measurable parameters. */
  narrativeFindings = '';
  isAbnormal = false;

  /** Set while the print sheet is rendered. */
  isPrinting = false;

  constructor(
    private apiService: ApiService,
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.orderId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadOrder();
  }

  loadOrder(): void {
    this.isLoading = true;
    this.apiService.get<LabOrderDetail>('lab/orders/' + this.orderId).subscribe({
      next: (order) => {
        this.order = order;
        this.resultValues = {};
        (order.parameters ?? []).forEach((param) => {
          this.resultValues[param.id] = param.value || '';
        });
        this.narrativeFindings = order.narrativeFindings || '';
        this.isAbnormal = !!order.isAbnormal;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'This order could not be found.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  // ----------------------------------------------------------
  // Reading and releasing
  // ----------------------------------------------------------

  get isNarrative(): boolean {
    return this.order?.resultKind === 'narrative';
  }

  /** A released result is a signed record, so it stops being editable. */
  get isReadOnly(): boolean {
    return this.order?.status === 'Released';
  }

  get canRelease(): boolean {
    return this.order?.status === 'Resulted';
  }

  onValueChange(parameterId: number, value: string): void {
    this.resultValues[parameterId] = value;
  }

  saveResults(): void {
    if (!this.order || this.isReadOnly) return;

    if (this.isNarrative && !this.narrativeFindings.trim()) {
      window.alert('Enter the findings before saving.');
      return;
    }

    this.isSaving = true;

    const results = (this.order.parameters ?? []).map((param) => ({
      parameterId: param.id,
      value: this.resultValues[param.id] || '',
    }));

    const payload = {
      results,
      remarks: this.remarks,
      narrativeFindings: this.narrativeFindings,
      isAbnormal: this.isAbnormal,
      resultedBy: this.auth.currentUser?.fullName ?? '',
    };

    this.apiService.post<void>('lab/orders/' + this.orderId + '/results', payload).subscribe({
      next: () => {
        this.isSaving = false;
        this.loadOrder();
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSaving = false;
        window.alert('Failed to save the results. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  releaseResults(): void {
    if (!this.order || !this.canRelease) return;

    const confirmed = window.confirm(
      'Release this result? It becomes part of the patient record and can no longer be edited.'
    );
    if (!confirmed) return;

    this.isReleasing = true;
    this.apiService.post<void>('lab/orders/' + this.orderId + '/release', {}).subscribe({
      next: () => {
        this.isReleasing = false;
        this.loadOrder();
        this.cdr.markForCheck();
      },
      error: () => {
        this.isReleasing = false;
        window.alert('Failed to release the result. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  /** Renders the print-only report and opens the dialog. */
  print(): void {
    this.isPrinting = true;
    this.cdr.detectChanges();

    document.body.classList.add('printing-slip');

    const cleanup = () => {
      document.body.classList.remove('printing-slip');
      this.isPrinting = false;
      this.cdr.markForCheck();
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);
    window.print();
    setTimeout(cleanup, 1000);
  }

  back(): void {
    this.router.navigate(['/app/lab-results']);
  }

  openPatient(): void {
    if (!this.order?.patient?.id) return;
    this.router.navigate(['/app/patients', this.order.patient.id]);
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  /**
   * Flag for a value as typed, so the row reacts before saving. Blank and
   * non-numeric entries carry no flag — a range check cannot say anything
   * about "trace" or "not detected".
   */
  getFlag(param: ResultParameter): string {
    const value = this.resultValues[param.id];
    if (!value || value.trim() === '') return '';

    const numVal = parseFloat(value);
    if (isNaN(numVal)) return '';

    if (param.referenceRangeMin === null && param.referenceRangeMax === null) return '';

    if (param.referenceRangeMax !== null && numVal > param.referenceRangeMax) return 'H';
    if (param.referenceRangeMin !== null && numVal < param.referenceRangeMin) return 'L';
    return 'N';
  }

  flagClass(flag: string): string {
    switch (flag) {
      case 'H':
        return 'pill pill--danger';
      case 'L':
        return 'pill pill--info';
      case 'N':
        return 'pill pill--success';
      default:
        return 'pill';
    }
  }

  flagLabel(flag: string): string {
    switch (flag) {
      case 'H':
        return 'High';
      case 'L':
        return 'Low';
      case 'N':
        return 'Normal';
      default:
        return '—';
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

  initials(name: string): string {
    return (name ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0])
      .join('')
      .toUpperCase();
  }

  referenceRange(param: ResultParameter): string {
    if (param.referenceRangeText && param.referenceRangeText.trim()) {
      return param.referenceRangeText;
    }
    if (param.referenceRangeMin !== null && param.referenceRangeMax !== null) {
      return param.referenceRangeMin + ' – ' + param.referenceRangeMax;
    }
    if (param.referenceRangeMin !== null) return '≥ ' + param.referenceRangeMin;
    if (param.referenceRangeMax !== null) return '≤ ' + param.referenceRangeMax;
    return '—';
  }
}
