import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { ApiService } from '../../../../core/services/api';

export interface ReportVisit {
  id: number;
  visitCode: string;
  patientName: string;
  testsCount: number;
  totalAmount: number;
  status: string;
  paymentStatus: string;
}

export interface DailyReport {
  date: string;
  totalVisits: number;
  totalRevenue: number;
  totalPatients: number;
  pendingResults: number;
  completedVisits: number;
  visits: ReportVisit[];
}

export interface ReferralGroup {
  source: string;
  count: number;
  percent: number;
  details: { name: string; count: number }[];
}

export interface ReferralReport {
  total: number;
  breakdown: ReferralGroup[];
}

@Component({
  selector: 'app-reports',
  standalone: false,
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class Reports implements OnInit {
  selectedDate: string = new Date().toISOString().substring(0, 10);
  report: DailyReport | null = null;
  isLoading = false;
  isDownloading: { [visitId: number]: boolean } = {};

  // Referral-source breakdown, with its own optional date range.
  referralReport: ReferralReport | null = null;
  referralFrom = '';
  referralTo = '';
  isReferralLoading = false;
  expandedSource: string | null = null;

  constructor(private apiService: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadReport();
    this.loadReferral();
  }

  loadReferral(): void {
    this.isReferralLoading = true;
    const params: Record<string, string> = {};
    if (this.referralFrom) params['from'] = this.referralFrom;
    if (this.referralTo) params['to'] = this.referralTo;
    this.apiService.get<ReferralReport>('reports/referral-sources', params).subscribe({
      next: (data) => {
        this.referralReport = data;
        this.isReferralLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.referralReport = null;
        this.isReferralLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  toggleSource(source: string): void {
    this.expandedSource = this.expandedSource === source ? null : source;
  }

  loadReport(): void {
    this.isLoading = true;
    this.apiService
      .get<DailyReport>('reports/daily', { date: this.selectedDate })
      .subscribe({
        next: (data) => {
          this.report = data;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.report = null;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });
  }

  onDateChange(date: string): void {
    this.selectedDate = date;
    this.loadReport();
  }

  downloadPdf(visitId: number): void {
    this.isDownloading[visitId] = true;
    this.apiService.getBlob('reports/visit/' + visitId + '/pdf').subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'visit-' + visitId + '-report.pdf';
        a.click();
        URL.revokeObjectURL(url);
        this.isDownloading[visitId] = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.isDownloading[visitId] = false;
        this.cdr.markForCheck();
      },
    });
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Paid':
        return 'badge-success';
      case 'Partial':
        return 'badge-warning';
      case 'Unpaid':
        return 'badge-danger';
      default:
        return 'badge-secondary';
    }
  }
}
