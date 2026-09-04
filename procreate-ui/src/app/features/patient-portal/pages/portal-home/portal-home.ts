import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../../../../core/services/api';
import { AuthService } from '../../../../core/services/auth';

interface PortalResult {
  orderCode: string;
  name: string;
  department: string;
  releasedAt: string;
  isAbnormal: boolean;
  narrativeFindings: string;
  parameters: {
    parameter: string;
    value: string;
    unit: string;
    reference: string;
    flag: string;
  }[];
}

interface PortalChart {
  patient: {
    patientCode: string;
    fullName: string;
    dateOfBirth: string;
    age: number;
    gender: string;
    bloodType: string;
    contactNumber: string;
    email: string;
  };
  allergies: { substance: string; severity: string; reaction: string }[];
  medications: { name: string; dosage: string; frequency: string }[];
  vitals: {
    recordedAt: string;
    systolicBp: number | null;
    diastolicBp: number | null;
    heartRate: number | null;
    respiratoryRate: number | null;
    temperatureC: number | null;
  }[];
  appointments: {
    appointmentCode: string;
    service: string;
    scheduledAt: string;
    status: string;
    doctor: string;
  }[];
  documents: {
    id: number;
    department: string;
    fileName: string;
    sizeBytes: number;
    resultDate: string;
  }[];
  results: PortalResult[];
}

/**
 * What a patient sees of their own record. Read-only throughout: everything
 * here is written by clinic staff.
 */
@Component({
  selector: 'app-portal-home',
  standalone: false,
  templateUrl: './portal-home.html',
  styleUrl: './portal-home.scss',
})
export class PortalHomeComponent implements OnInit {
  chart: PortalChart | null = null;
  isLoading = true;
  errorMessage = '';

  /** Which released result is expanded, by order code. */
  openResult: string | null = null;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.api.get<PortalChart>('portal/me').subscribe({
      next: (chart) => {
        this.chart = chart;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        // A rejected token means the session lapsed, so send them to sign in
        // again rather than showing an error they cannot act on.
        if (err?.status === 401 || err?.status === 403) {
          this.auth.logout();
          return;
        }
        this.errorMessage = 'Your records could not be loaded. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  toggleResult(orderCode: string): void {
    this.openResult = this.openResult === orderCode ? null : orderCode;
  }

  documentUrl(documentId: number): string {
    return this.api.url(`portal/me/documents/${documentId}`);
  }

  viewDocument(documentId: number): void {
    window.open(this.documentUrl(documentId), '_blank');
  }

  signOut(): void {
    this.auth.logout();
  }

  bookAppointment(): void {
    this.router.navigate(['/register']);
  }

  // ----------------------------------------------------------
  // Presentation
  // ----------------------------------------------------------

  get nextAppointment() {
    const now = Date.now();
    return (this.chart?.appointments ?? []).find(
      (a) => new Date(a.scheduledAt).getTime() >= now && a.status !== 'Cancelled'
    ) ?? null;
  }

  bloodPressure(v: PortalChart['vitals'][number]): string {
    if (v.systolicBp == null && v.diastolicBp == null) return '—';
    return `${v.systolicBp ?? '—'}/${v.diastolicBp ?? '—'}`;
  }

  flagClass(flag: string): string {
    switch (flag) {
      case 'High':
        return 'pill pill--danger';
      case 'Low':
        return 'pill pill--info';
      case 'Normal':
        return 'pill pill--success';
      default:
        return 'pill';
    }
  }

  severityPill(severity: string): string {
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

  fileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
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
}
