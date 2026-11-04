import { ChangeDetectorRef, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ApiService } from '../../../../core/services/api';
import { ConfirmService } from '../../../../core/services/confirm';
import { ToastService } from '../../../../core/services/toast';
import { CONSULTATION_SERVICES, PROCEDURE_SERVICES } from '../../appointment-services';

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

/** A session block on the chosen date that still has room (minute budget). */
interface BatchOption {
  id: number;
  startTime: string;
  endTime: string;
  label: string;
  booked: number;
  totalMinutes: number;
  usedMinutes: number;
  remainingMinutes: number;
  defaultServiceMinutes: number;
  serviceDurations: Record<string, number>;
}

/** One cell in the month grid. */
interface CalendarDay {
  date: Date;
  inMonth: boolean;
  isToday: boolean;
  appointments: Appointment[];
  /** Day-cell tallies, shown in place of individual appointment chips. */
  newPatients: number;
  followUps: number;
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

  /** Services booked from the Consultations calendar. */
  readonly consultationServices = CONSULTATION_SERVICES;

  /** Services booked from the Procedures calendar. */
  readonly procedureServices = PROCEDURE_SERVICES;

  /** Fast membership test that tells a procedure booking from a consultation. */
  private readonly procedureServiceSet = new Set(this.procedureServices);

  /** Which calendar this page is — set from the route. Drives filtering + labels. */
  mode: 'consultation' | 'procedure' = 'consultation';

  /** Patient category, reusing the appointment Type field (see save()). */
  readonly typeOptions = ['New Patient', 'Follow-up'];

  /** The service picker offered in the booking sheet, per calendar. */
  get serviceOptions(): string[] {
    return this.mode === 'procedure' ? this.procedureServices : this.consultationServices;
  }

  viewMonth = new Date();
  selectedDate = new Date();
  weeks: CalendarDay[][] = [];

  /** Which layout is shown: the month grid, a single week, or one day. */
  calendarView: 'month' | 'week' | 'day' = 'month';
  /** The Sun–Sat week containing selectedDate, for the week view. */
  weekDays: CalendarDay[] = [];

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

  /** The scrollable modal body, so an error can bring itself into view. */
  @ViewChild('modalBody') private modalBody?: ElementRef<HTMLElement>;

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef,
    private confirm: ConfirmService,
    private toast: ToastService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // Consultations and Procedures are the same component on two routes; the
    // route's `mode` decides which appointments show and which services the
    // booking sheet offers. Subscribe (not a one-off read) so switching between
    // the two re-filters even if Angular reuses this component instance.
    this.route.data.subscribe((data) => {
      this.mode = data['mode'] === 'procedure' ? 'procedure' : 'consultation';
      this.rebuildForMode();
    });

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

    // Load the whole 6-week grid range (Sunday before the 1st, +41 days), not
    // just the calendar month — so week views at the month's edges still have
    // their appointments.
    const gridStart = new Date(this.viewMonth.getFullYear(), this.viewMonth.getMonth(), 1);
    gridStart.setDate(gridStart.getDate() - gridStart.getDay());
    const gridEnd = new Date(gridStart);
    gridEnd.setDate(gridStart.getDate() + 41);

    this.api
      .get<{ data: Appointment[] }>('appointments', {
        from: this.toIsoDate(gridStart),
        to: this.toIsoDate(gridEnd),
        doctorId: this.doctorFilter === '' ? null : this.doctorFilter,
      })
      .subscribe({
        next: (res) => {
          this.monthAppointments = res.data;
          this.buildGrid();
          this.buildWeek();
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
          this.archivedCount = res.data.filter((a) => a.isArchived && this.inMode(a)).length;
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
        row.push(this.buildDay(date, date.getMonth() === month, date.getTime() === today.getTime()));
        cursor.setDate(cursor.getDate() + 1);
      }
      weeks.push(row);
    }

    this.weeks = weeks;
  }

  /** Builds the Sun–Sat week around selectedDate, for the week view. */
  private buildWeek(): void {
    const base = this.startOfDay(this.selectedDate);
    const sunday = new Date(base);
    sunday.setDate(base.getDate() - base.getDay());

    const today = this.startOfDay(new Date());
    const month = this.viewMonth.getMonth();

    this.weekDays = Array.from({ length: 7 }, (_, i) => {
      const date = new Date(sunday);
      date.setDate(sunday.getDate() + i);
      return this.buildDay(date, date.getMonth() === month, date.getTime() === today.getTime());
    });
  }

  /** One calendar cell with its appointments and new/follow-up tallies. */
  private buildDay(date: Date, inMonth: boolean, isToday: boolean): CalendarDay {
    const appointments = this.appointmentsOn(date);
    const followUps = appointments.filter((a) => a.type === 'Follow-up').length;
    return {
      date,
      inMonth,
      isToday,
      appointments,
      newPatients: appointments.length - followUps,
      followUps,
    };
  }

  setView(view: 'month' | 'week' | 'day'): void {
    this.calendarView = view;
    this.buildWeek();
    this.cdr.markForCheck();
  }

  /** Heading for the current view: the month, the week range, or the day. */
  get calendarHeading(): string {
    if (this.calendarView === 'week' && this.weekDays.length === 7) {
      const start = this.weekDays[0].date;
      const end = this.weekDays[6].date;
      const startStr = start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
      const endStr = end.toLocaleDateString(undefined, {
        month: start.getMonth() === end.getMonth() ? undefined : 'short',
        day: 'numeric',
        year: 'numeric',
      });
      return `${startStr} – ${endStr}`;
    }
    if (this.calendarView === 'day') {
      return this.selectedDate.toLocaleDateString(undefined, {
        weekday: 'long', month: 'long', day: 'numeric', year: 'numeric',
      });
    }
    return this.viewMonth.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
  }

  /** Totals bar shows only in week/month; the day view is unchanged. */
  get showTotals(): boolean {
    return this.calendarView !== 'day';
  }

  /**
   * New-patient vs follow-up counts for the appointments currently in view —
   * the whole month in month view, the shown week in week view — honouring the
   * doctor filter (already applied when loading) and this calendar's mode.
   * Follow-up is Type === 'Follow-up'; everything else counts as a new patient.
   */
  get viewTotals(): { newPatient: number; followUp: number; total: number } {
    let appts: Appointment[];
    if (this.calendarView === 'week') {
      appts = this.weekDays.flatMap((d) => d.appointments);
    } else {
      const prefix = `${this.viewMonth.getFullYear()}-${`${this.viewMonth.getMonth() + 1}`.padStart(2, '0')}`;
      appts = this.monthAppointments.filter(
        (a) => this.inMode(a) && a.scheduledAt.substring(0, 7) === prefix,
      );
    }

    const followUp = appts.filter((a) => a.type === 'Follow-up').length;
    return { newPatient: appts.length - followUp, followUp, total: appts.length };
  }

  /** Hero title/subtitle and booking-sheet title follow the active calendar. */
  get pageTitle(): string {
    return this.mode === 'procedure' ? 'Procedures' : 'Consultations';
  }

  get pageSubtitle(): string {
    return this.mode === 'procedure'
      ? 'Schedule ultrasound, IUI and other procedures'
      : 'Manage and view patient consultations';
  }

  get modalTitle(): string {
    return this.mode === 'procedure' ? 'New Procedure' : 'New Appointment';
  }

  private appointmentsOn(date: Date): Appointment[] {
    const key = this.toIsoDate(date);
    return this.monthAppointments
      .filter((a) => this.inMode(a) && a.scheduledAt.substring(0, 10) === key)
      .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt));
  }

  /** True when an appointment belongs on this calendar (consultation vs procedure). */
  private inMode(appointment: Appointment): boolean {
    const isProcedure = this.procedureServiceSet.has(appointment.service);
    return this.mode === 'procedure' ? isProcedure : !isProcedure;
  }

  /** Re-filter the grid/week/day for the active calendar; appointments are already loaded. */
  private rebuildForMode(): void {
    this.buildGrid();
    this.buildWeek();
    this.dayAppointments = this.appointmentsOn(this.selectedDate);
    this.loadArchivedCount();
    this.cdr.markForCheck();
  }

  selectDate(date: Date, refreshGrid = true): void {
    this.selectedDate = this.startOfDay(date);
    this.dayAppointments = this.appointmentsOn(this.selectedDate);
    this.buildWeek();
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

  /** Previous/next, stepping by the active view's unit (month, week, or day). */
  shift(delta: number): void {
    if (this.calendarView === 'month') {
      this.shiftMonth(delta);
      return;
    }

    const step = this.calendarView === 'week' ? 7 * delta : delta;
    const next = new Date(this.selectedDate);
    next.setDate(next.getDate() + step);
    this.selectedDate = this.startOfDay(next);

    // Moving into another month needs that month's data; otherwise just rebuild.
    if (next.getMonth() !== this.viewMonth.getMonth() ||
        next.getFullYear() !== this.viewMonth.getFullYear()) {
      this.viewMonth = new Date(next.getFullYear(), next.getMonth(), 1);
      this.loadMonth();
    } else {
      this.dayAppointments = this.appointmentsOn(this.selectedDate);
      this.buildWeek();
      this.cdr.markForCheck();
    }
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

  /** Shown beside the month heading so the active doctor filter is obvious at a glance. */
  get doctorFilterLabel(): string {
    if (this.doctorFilter === '') return 'All Doctors';
    return this.doctors.find((d) => d.id === this.doctorFilter)?.fullName ?? 'All Doctors';
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

  async archive(appointment: Appointment): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Archive appointment',
      message: `Archive ${appointment.appointmentCode}?`,
      detail: 'It stays in history but leaves the calendar.',
      confirmLabel: 'Archive',
    });
    if (!confirmed) return;

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

  /** Double-clicking a day jumps straight into booking for that date. */
  openModalForDate(date: Date): void {
    this.selectDate(date);
    this.openModal();
  }

  openModal(): void {
    this.isModalOpen = true;
    this.modalError = '';
    this.patientQuery = '';
    this.patientResults = [];
    this.selectedPatientLabel = '';

    this.form = {
      patientId: 0,
      doctorId: this.doctors[0]?.id ?? 0,
      service: this.serviceOptions[0],
      date: this.toIsoDate(this.selectedDate),
      time: '09:00',
      type: 'New Patient',
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

  /** Minutes past midnight for an "HH:mm" value. */
  private minutesOf(time: string): number {
    const [h, m] = (time || '0:0').split(':').map(Number);
    return (h || 0) * 60 + (m || 0);
  }

  /** The available block whose window contains the given time, or null. */
  private blockForTime(time: string): BatchOption | null {
    if (!time) return null;
    const mins = this.minutesOf(time);
    return this.availableBatches.find(
      (b) => this.minutesOf(b.startTime) <= mins && mins < this.minutesOf(b.endTime),
    ) ?? null;
  }

  /** Tells the user which block the chosen time will land the booking in. */
  get timeBlockHint(): string {
    if (!this.form.time) return '';
    const block = this.blockForTime(this.form.time);
    return block
      ? `Goes into the ${block.label} block on the Batches board.`
      : 'No block covers this time — it books at this exact time.';
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
      this.failModal('Please choose a patient.');
      return;
    }
    if (!this.form.doctorId) {
      this.failModal('Please choose a doctor.');
      return;
    }
    if (!this.form.date) {
      this.failModal('Please set a date.');
      return;
    }
    if (!this.form.time) {
      this.failModal('Please set a time.');
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
        // Matched to a block, the API overrides this with the block's start.
        scheduledAt: `${this.form.date}T${this.form.time || '09:00'}:00`,
        type: this.form.type,
        status: 'Scheduled',
        chiefComplaint: this.form.chiefComplaint,
        notes: this.form.notes,
        // The time picks the block automatically — whichever window contains it.
        batchId: this.blockForTime(this.form.time)?.id ?? null,
      })
      .subscribe({
        next: (created) => {
          this.isSaving = false;
          this.isModalOpen = false;
          this.selectedDate = this.startOfDay(new Date(created.scheduledAt));
          this.loadMonth();
          this.toast.success(`Appointment booked for ${created.patientName}.`);
          this.cdr.markForCheck();
        },
        error: (err) => {
          // The API returns specific conflict / out-of-hours messages worth showing verbatim.
          this.isSaving = false;
          this.failModal(err?.error?.message ?? 'Could not create the appointment.');
        },
      });
  }

  /**
   * Shows an error inside the modal and scrolls it back into view. The Create
   * button sits at the foot of a scrollable sheet, so after a failed submit the
   * message at the top would otherwise be out of sight.
   */
  private failModal(message: string): void {
    this.modalError = message;
    this.cdr.markForCheck();
    setTimeout(() => this.modalBody?.nativeElement.scrollTo({ top: 0, behavior: 'smooth' }));
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
