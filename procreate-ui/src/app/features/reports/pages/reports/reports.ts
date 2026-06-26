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

  constructor(private apiService: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadReport();
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
