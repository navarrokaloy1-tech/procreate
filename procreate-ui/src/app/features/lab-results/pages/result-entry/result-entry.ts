import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';

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

export interface LabOrderDetail {
  id: number;
  orderCode: string;
  patientName: string;
  testName: string;
  status: string;
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
  resultValues: { [parameterId: number]: string } = {};
  remarks = '';

  constructor(
    private apiService: ApiService,
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
        if (order.parameters) {
          order.parameters.forEach((param) => {
            this.resultValues[param.id] = param.value || '';
          });
        }
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  getFlag(param: ResultParameter): string {
    const value = this.resultValues[param.id];
    if (!value || value.trim() === '') return '';

    const numVal = parseFloat(value);
    if (isNaN(numVal)) return '';

    if (param.referenceRangeMin === null && param.referenceRangeMax === null) {
      return '';
    }

    if (param.referenceRangeMax !== null && numVal > param.referenceRangeMax) {
      return 'H';
    }
    if (param.referenceRangeMin !== null && numVal < param.referenceRangeMin) {
      return 'L';
    }
    return 'N';
  }

  getFlagClass(flag: string): string {
    switch (flag) {
      case 'H':
        return 'flag-high';
      case 'L':
        return 'flag-low';
      case 'N':
        return 'flag-normal';
      default:
        return '';
    }
  }

  onValueChange(parameterId: number, value: string): void {
    this.resultValues[parameterId] = value;
  }

  saveResults(): void {
    if (!this.order) return;
    this.isSaving = true;

    const results = this.order.parameters.map((param) => ({
      parameterId: param.id,
      value: this.resultValues[param.id] || '',
    }));

    const payload = { results, remarks: this.remarks };

    this.apiService.post<void>('lab/orders/' + this.orderId + '/results', payload).subscribe({
      next: () => {
        this.isSaving = false;
        window.alert('Results saved successfully.');
        this.loadOrder();
        this.cdr.markForCheck();
      },
      error: () => {
        this.isSaving = false;
        window.alert('Failed to save results. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  releaseResults(): void {
    if (!this.order) return;
    const confirmed = window.confirm('Release results for this order? This will make results available to the patient.');
    if (!confirmed) return;

    this.isReleasing = true;
    this.apiService.post<void>('lab/orders/' + this.orderId + '/release', {}).subscribe({
      next: () => {
        this.isReleasing = false;
        this.router.navigate(['/app/lab-results']);
        this.cdr.markForCheck();
      },
      error: () => {
        this.isReleasing = false;
        window.alert('Failed to release results. Please try again.');
        this.cdr.markForCheck();
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/app/lab-results']);
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Ordered':
        return 'badge-info';
      case 'Collected':
        return 'badge-warning';
      case 'Resulted':
        return 'badge-secondary';
      case 'Released':
        return 'badge-success';
      default:
        return '';
    }
  }

  getReferenceRangeDisplay(param: ResultParameter): string {
    if (param.referenceRangeText && param.referenceRangeText.trim()) {
      return param.referenceRangeText;
    }
    if (param.referenceRangeMin !== null && param.referenceRangeMax !== null) {
      return param.referenceRangeMin + ' – ' + param.referenceRangeMax;
    }
    if (param.referenceRangeMin !== null) {
      return '≥ ' + param.referenceRangeMin;
    }
    if (param.referenceRangeMax !== null) {
      return '≤ ' + param.referenceRangeMax;
    }
    return '—';
  }
}
