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

interface BookingDoctor {
  id: number;
  name: string;
  specialty: string;
}

interface BookingSlot {
  id: number;
  startTime: string;
  endTime: string;
  label: string;
  remaining: number;
  isClosed: boolean;
  /** Already has an appointment of theirs, so it cannot take another. */
  alreadyBooked: boolean;
}

interface MonthResponse {
  year: number;
  month: number;
  days: { date: string; openSeats: number }[];
  doctors: BookingDoctor[];
  services: string[];
}

/** One cell of the booking calendar. */
interface BookingDay {
  date: Date;
  /** YYYY-MM-DD, the key the month response is indexed by. */
  key: string;
  inMonth: boolean;
  openSeats: number;
}

/**
 * What a patient sees of their own record.
 *
 * The chart itself is read-only — everything on it is written by clinic staff.
 * Booking is the one thing they can do here, and it only ever requests a slot:
 * the appointment arrives Pending for the desk to confirm.
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

  // --- Booking panel ---
  isBookingOpen = false;
  isLoadingMonth = false;
  isLoadingSlots = false;
  isBooking = false;
  bookingError = '';
  bookingNotice = '';

  viewMonth = new Date();
  weeks: BookingDay[][] = [];
  /** Bookable dates this month, keyed YYYY-MM-DD, valued by seats left. */
  private openDays = new Map<string, number>();

  selectedDate: string | null = null;
  slots: BookingSlot[] = [];

  doctors: BookingDoctor[] = [];
  services: string[] = [];

  batchId: number | '' = '';
  doctorId: number | '' = '';
  service = '';
  notes = '';

  readonly weekdayLabels = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

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

  // ----------------------------------------------------------
  // Booking
  // ----------------------------------------------------------

  /**
   * Opens the booking panel under the appointment card. It expands in place
   * rather than navigating, so the chart stays on screen beside it.
   */
  bookAppointment(): void {
    this.isBookingOpen = true;
    this.bookingError = '';
    this.bookingNotice = '';
    this.selectedDate = null;
    this.slots = [];
    this.loadMonth();
  }

  closeBooking(): void {
    this.isBookingOpen = false;
  }

  shiftMonth(step: number): void {
    const next = new Date(this.viewMonth.getFullYear(), this.viewMonth.getMonth() + step, 1);
    this.viewMonth = next;
    this.selectedDate = null;
    this.slots = [];
    this.loadMonth();
  }

  /** Which days this month can be booked, plus the doctors and services. */
  private loadMonth(): void {
    this.isLoadingMonth = true;

    this.api
      .get<MonthResponse>('portal/booking/month', {
        year: this.viewMonth.getFullYear(),
        month: this.viewMonth.getMonth() + 1,
      })
      .subscribe({
        next: (res) => {
          this.openDays = new Map((res.days ?? []).map((d) => [d.date.slice(0, 10), d.openSeats]));
          this.doctors = res.doctors ?? [];
          this.services = res.services ?? [];
          if (!this.doctorId) this.doctorId = this.doctors[0]?.id ?? '';
          if (!this.service) this.service = this.services[0] ?? '';
          this.buildWeeks();
          this.isLoadingMonth = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.bookingError = 'Could not load the calendar. Please try again.';
          this.isLoadingMonth = false;
          this.cdr.markForCheck();
        },
      });
  }

  /** The month grid, padded to whole weeks. */
  private buildWeeks(): void {
    const year = this.viewMonth.getFullYear();
    const month = this.viewMonth.getMonth();
    const first = new Date(year, month, 1);

    const cursor = new Date(first);
    cursor.setDate(cursor.getDate() - cursor.getDay());

    const weeks: BookingDay[][] = [];

    for (let w = 0; w < 6; w++) {
      const week: BookingDay[] = [];

      for (let d = 0; d < 7; d++) {
        const key = this.dateKey(cursor);
        week.push({
          date: new Date(cursor),
          key,
          inMonth: cursor.getMonth() === month,
          openSeats: this.openDays.get(key) ?? 0,
        });
        cursor.setDate(cursor.getDate() + 1);
      }

      weeks.push(week);
      // Stop once the month is behind us rather than always drawing six rows.
      if (cursor.getMonth() !== month && weeks.length >= 4) break;
    }

    this.weeks = weeks;
  }

  selectDay(day: BookingDay): void {
    if (day.openSeats === 0) return;

    this.selectedDate = day.key;
    this.bookingError = '';
    this.bookingNotice = '';
    this.isLoadingSlots = true;
    this.slots = [];
    this.batchId = '';

    this.api
      .get<{ data: BookingSlot[] }>('portal/booking/day', { date: day.key })
      .subscribe({
        next: (res) => {
          this.slots = res.data ?? [];
          this.isLoadingSlots = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.bookingError = err?.error?.message ?? 'Could not load times for that day.';
          this.isLoadingSlots = false;
          this.cdr.markForCheck();
        },
      });
  }

  selectSlot(slot: BookingSlot): void {
    if (slot.isClosed || slot.alreadyBooked || slot.remaining === 0) return;
    this.batchId = slot.id;
  }

  slotDisabled(slot: BookingSlot): boolean {
    return slot.isClosed || slot.alreadyBooked || slot.remaining === 0;
  }

  confirmBooking(): void {
    if (!this.batchId) {
      this.bookingError = 'Choose a time first.';
      return;
    }

    this.isBooking = true;
    this.bookingError = '';

    this.api
      .post<{ message: string }>('portal/me/appointments', {
        batchId: this.batchId,
        doctorId: Number(this.doctorId),
        service: this.service,
        notes: this.notes.trim(),
      })
      .subscribe({
        next: (res) => {
          this.isBooking = false;
          this.isBookingOpen = false;
          this.bookingNotice = res.message ?? 'Appointment requested.';
          this.notes = '';
          // The new booking belongs on the chart beside this panel.
          this.reloadChart();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.bookingError = err?.error?.message ?? 'That booking could not be made.';
          this.isBooking = false;
          // Capacity may have moved under us, so show the day as it is now.
          if (this.selectedDate) {
            this.api
              .get<{ data: BookingSlot[] }>('portal/booking/day', { date: this.selectedDate })
              .subscribe({
                next: (res) => {
                  this.slots = res.data ?? [];
                  this.batchId = '';
                  this.cdr.markForCheck();
                },
                error: () => this.cdr.markForCheck(),
              });
          }
          this.cdr.markForCheck();
        },
      });
  }

  private reloadChart(): void {
    this.api.get<PortalChart>('portal/me').subscribe({
      next: (chart) => {
        this.chart = chart;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  /** Local YYYY-MM-DD; toISOString would shift the day in this timezone. */
  private dateKey(date: Date): string {
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }

  get selectedDateLabel(): string {
    if (!this.selectedDate) return '';
    const [y, m, d] = this.selectedDate.split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString(undefined, {
      weekday: 'long', day: 'numeric', month: 'long',
    });
  }

  trackDay = (_: number, day: BookingDay) => day.key;
  trackSlot = (_: number, slot: BookingSlot) => slot.id;

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
