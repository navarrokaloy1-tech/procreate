import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

export interface Appointment {
  id: number;
  appointmentCode: string;
  patientId: number;
  patientName: string;
  patientCode: string;
  doctorId: number;
  doctorName: string;
  service: string;
  scheduledAt: string;
  type: string;
  status: string;
  chiefComplaint: string;
  notes: string;
  isArchived: boolean;
}

interface DoctorOption {
  id: number;
  fullName: string;
  specialty: string;
  consultationFee: number;
  isActive: boolean;
}

interface PatientOption {
  id: number;
  patientCode: string;
  firstName: string;
  lastName: string;
}

/** A session block on the chosen date that still has room. */
interface BatchOption {
  id: number;
  startTime: string;
  endTime: string;
  label: string;
  capacity: number;
  booked: number;
  remaining: number;
}

/** One cell in the month grid. */
interface CalendarDay {
  date: Date;
  inMonth: boolean;
  isToday: boolean;
  appointments: Appointment[];
}

@Component({
  selector: 'app-appointment-calendar',
  standalone: false,
  templateUrl: './appointment-calendar.html',
  styleUrl: './appointment-calendar.scss',
})
export class AppointmentCalendarComponent implements OnInit {
  readonly weekdayLabels = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  /** Legend order matches the workflow, and each maps to an existing theme colour. */
  readonly statuses = [
    'Scheduled', 'Confirmed', 'CheckedIn', 'InProgress',
    'Completed', 'Pending', 'Cancelled', 'NoShow',
  ];

  readonly serviceOptions = [
    'General Consultation', 'Follow-up Consultation', 'Prenatal Check-up',
    'Fertility Consultation', 'Ultrasound', 'Laboratory Request',
  ];

  readonly typeOptions = ['Scheduled', 'Walk-in', 'Follow-up'];

  viewMonth = new Date();
  selectedDate = new Date();
  weeks: CalendarDay[][] = [];

  monthAppointments: Appointment[] = [];
  dayAppointments: Appointment[] = [];
  archivedCount = 0;

  doctors: DoctorOption[] = [];
  doctorFilter: number | '' = '';

  isLoading = false;
  errorMessage = '';

  // --- New appointment modal ---
  isModalOpen = false;
  isSaving = false;
  modalError = '';

  form = {
    patientId: 0,
    doctorId: 0,
    service: 'General Consultation',
    date: '',
    time: '',
    type: 'Scheduled',
    chiefComplaint: '',
    notes: '',
    /** Blank books at an exact time instead of joining a session block. */
    batchId: '' as number | '',
  };

  /** Blocks with room left on the chosen date. */
  availableBatches: BatchOption[] = [];
  isLoadingBatches = false;

  patientQuery = '';
  patientResults: PatientOption[] = [];
  selectedPatientLabel = '';
  private patientSearch$ = new Subject<string>();

  /** Date to fetch bookable blocks for; switchMapped so only the latest wins. */
  private batchDate$ = new Subject<string>();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.patientSearch$
      .pipe(debounceTime(250), distinctUntilChanged())
      .subscribe((term) => this.runPatientSearch(term));

    this.batchDate$
      .pipe(
        switchMap((date) =>
          this.api
            .get<{ data: BatchOption[] }>('appointment-batches/available', { date })
            // A failed lookup must not kill the stream, or the picker would
            // stay stuck empty for the rest of the session.
            .pipe(catchError(() => of({ data: [] as BatchOption[] }))),
        ),
      )
      .subscribe((res) => this.applyAvailableBatches(res.data ?? []));

    this.loadDoctors();
    this.loadMonth();
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  loadDoctors(): void {
    this.api
      .get<{ data: DoctorOption[] }>('doctors', { isActive: true, pageSize: 100 })
      .subscribe({
        next: (res) => {
          this.doctors = res.data;
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  loadMonth(): void {
    this.isLoading = true;
    this.errorMessage = '';

    const first = new Date(this.viewMonth.getFullYear(), this.viewMonth.getMonth(), 1);
    const last = new Date(this.viewMonth.getFullYear(), this.viewMonth.getMonth() + 1, 0);

    this.api
      .get<{ data: Appointment[] }>('appointments', {
        from: this.toIsoDate(first),
        to: this.toIsoDate(last),
        doctorId: this.doctorFilter === '' ? null : this.doctorFilter,
      })
      .subscribe({
        next: (res) => {
          this.monthAppointments = res.data;
          this.buildGrid();
          this.selectDate(this.selectedDate, false);
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not load appointments.';
          this.isLoading = false;
          this.cdr.markForCheck();
        },
      });

    this.loadArchivedCount();
  }

  private loadArchivedCount(): void {
    this.api
      .get<{ total: number; data: Appointment[] }>('appointments', {
        includeArchived: true,
        doctorId: this.doctorFilter === '' ? null : this.doctorFilter,
      })
      .subscribe({
        next: (res) => {
          this.archivedCount = res.data.filter((a) => a.isArchived).length;
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  // ----------------------------------------------------------
  // Calendar grid
  // ----------------------------------------------------------

  /** Builds a 6-week grid padded with leading/trailing days from adjacent months. */
  private buildGrid(): void {
    const year = this.viewMonth.getFullYear();
    const month = this.viewMonth.getMonth();
    const firstOfMonth = new Date(year, month, 1);

    const cursor = new Date(firstOfMonth);
    cursor.setDate(cursor.getDate() - firstOfMonth.getDay());

    const today = this.startOfDay(new Date());
    const weeks: CalendarDay[][] = [];

    for (let week = 0; week < 6; week++) {
      const row: CalendarDay[] = [];
      for (let day = 0; day < 7; day++) {
        const date = new Date(cursor);
        row.push({
          date,
          inMonth: date.getMonth() === month,
          isToday: date.getTime() === today.getTime(),
          appointments: this.appointmentsOn(date),
        });
        cursor.setDate(cursor.getDate() + 1);
      }
      weeks.push(row);
    }

    this.weeks = weeks;
  }

  private appointmentsOn(date: Date): Appointment[] {
    const key = this.toIsoDate(date);
    return this.monthAppointments
      .filter((a) => a.scheduledAt.substring(0, 10) === key)
      .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt));
  }

  selectDate(date: Date, refreshGrid = true): void {
    this.selectedDate = this.startOfDay(date);
    this.dayAppointments = this.appointmentsOn(this.selectedDate);
    if (refreshGrid) this.cdr.markForCheck();
  }

  isSelected(day: CalendarDay): boolean {
    return this.startOfDay(day.date).getTime() === this.selectedDate.getTime();
  }

  shiftMonth(delta: number): void {
    this.viewMonth = new Date(
      this.viewMonth.getFullYear(),
      this.viewMonth.getMonth() + delta,
      1
    );
    this.loadMonth();
  }

  goToToday(): void {
    const today = new Date();
    this.viewMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    this.selectedDate = this.startOfDay(today);
    this.loadMonth();
  }

  onDoctorFilterChange(value: string): void {
    this.doctorFilter = value === '' ? '' : Number(value);
    this.loadMonth();
  }

  /** Cancelled and no-show entries are struck through in the grid. */
  isVoided(appointment: Appointment): boolean {
    return appointment.status === 'Cancelled' || appointment.status === 'NoShow';
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'CheckedIn': return 'Checked-in';
      case 'InProgress': return 'In Progress';
      case 'NoShow': return 'No Show';
      default: return status;
    }
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase();
  }

  // ----------------------------------------------------------
  // Status changes
  // ----------------------------------------------------------

  changeStatus(appointment: Appointment, status: string): void {
    this.api
      .patch<Appointment>(`appointments/${appointment.id}/status`, { status })
      .subscribe({
        next: () => {
          this.loadMonth();
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not update that appointment.';
          this.cdr.markForCheck();
        },
      });
  }

  archive(appointment: Appointment): void {
    if (!window.confirm(`Archive ${appointment.appointmentCode}? It stays in history but leaves the calendar.`)) {
      return;
    }

    this.api
      .patch<Appointment>(`appointments/${appointment.id}/archive`, { isArchived: true })
      .subscribe({
        next: () => {
          this.loadMonth();
          this.cdr.markForCheck();
        },
        error: () => {
          this.errorMessage = 'Could not archive that appointment.';
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // New appointment modal
  // ----------------------------------------------------------

  openModal(): void {
    this.isModalOpen = true;
    this.modalError = '';
    this.patientQuery = '';
    this.patientResults = [];
    this.selectedPatientLabel = '';

    this.form = {
      patientId: 0,
      doctorId: this.doctors[0]?.id ?? 0,
      service: 'General Consultation',
      date: this.toIsoDate(this.selectedDate),
      time: '09:00',
      type: 'Scheduled',
      chiefComplaint: '',
      notes: '',
      batchId: '',
    };

    this.loadAvailableBatches();
  }

  /**
   * Blocks with room on the chosen day, for the block picker.
   *
   * Goes through a switchMap rather than subscribing per call: opening the
   * sheet and then changing the date puts two requests in flight, and without
   * cancelling the first, whichever answers last wins — which showed the
   * previous day's blocks whenever the earlier response was the slower one.
   */
  loadAvailableBatches(): void {
    if (!this.form.date) {
      this.availableBatches = [];
      return;
    }

    this.isLoadingBatches = true;
    this.batchDate$.next(this.form.date);
  }

  private applyAvailableBatches(batches: BatchOption[]): void {
    this.availableBatches = batches;

    // A block chosen for the old date may not exist on the new one.
    if (!this.availableBatches.some((b) => b.id === this.form.batchId)) {
      this.form.batchId = '';
    }

    this.isLoadingBatches = false;
    this.cdr.markForCheck();
  }

  onFormDateChange(value: string): void {
    this.form.date = value;
    this.loadAvailableBatches();
  }

  /** Picking a block fixes the time, so the time field steps aside. */
  onBatchChange(value: number | ''): void {
    this.form.batchId = value === '' ? '' : Number(value);

    const batch = this.availableBatches.find((b) => b.id === this.form.batchId);
    if (batch) this.form.time = batch.startTime;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
  }

  onPatientQuery(term: string): void {
    this.patientQuery = term;
    this.selectedPatientLabel = '';
    this.form.patientId = 0;
    this.patientSearch$.next(term);
  }

  private runPatientSearch(term: string): void {
    if (!term.trim()) {
      this.patientResults = [];
      this.cdr.markForCheck();
      return;
    }

    this.api
      .get<{ data: PatientOption[] }>('patients', { search: term, pageSize: 8 })
      .subscribe({
        next: (res) => {
          this.patientResults = res.data;
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  choosePatient(patient: PatientOption): void {
    this.form.patientId = patient.id;
    this.selectedPatientLabel = `${patient.firstName} ${patient.lastName} · ${patient.patientCode}`;
    this.patientQuery = this.selectedPatientLabel;
    this.patientResults = [];
  }

  save(): void {
    if (!this.form.patientId) {
      this.modalError = 'Please choose a patient.';
      return;
    }
    if (!this.form.doctorId) {
      this.modalError = 'Please choose a doctor.';
      return;
    }
    if (!this.form.date) {
      this.modalError = 'Please set a date.';
      return;
    }
    if (!this.form.batchId && !this.form.time) {
      this.modalError = 'Please choose a time block, or set an exact time.';
      return;
    }

    this.isSaving = true;
    this.modalError = '';

    this.api
      .post<Appointment>('appointments', {
        patientId: this.form.patientId,
        doctorId: this.form.doctorId,
        service: this.form.service,
        // Send local wall-clock time; the API compares it against clinic hours.
        // With a block chosen the API overrides this with the block's start.
        scheduledAt: `${this.form.date}T${this.form.time || '09:00'}:00`,
        type: this.form.type,
        status: 'Scheduled',
        chiefComplaint: this.form.chiefComplaint,
        notes: this.form.notes,
        batchId: this.form.batchId === '' ? null : this.form.batchId,
      })
      .subscribe({
        next: (created) => {
          this.isSaving = false;
          this.isModalOpen = false;
          this.selectedDate = this.startOfDay(new Date(created.scheduledAt));
          this.loadMonth();
          this.cdr.markForCheck();
        },
        error: (err) => {
          // The API returns specific conflict / out-of-hours messages worth showing verbatim.
          this.modalError = err?.error?.message ?? 'Could not create the appointment.';
          this.isSaving = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // Helpers
  // ----------------------------------------------------------

  private startOfDay(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
  }

  /** Local YYYY-MM-DD — avoids the UTC shift that toISOString() would introduce. */
  private toIsoDate(date: Date): string {
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }
}
