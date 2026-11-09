import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ApiService } from '../../../../core/services/api';
import { PROCEDURE_BATCH_CARDS, PROCEDURE_SERVICES } from '../../appointment-services';

export interface BatchSlot {
  appointmentId: number;
  appointmentCode: string;
  position: number;
  patientId: number;
  patientName: string;
  patientCode: string;
  doctorName: string;
  service: string;
  status: string;
  type: string;
  hasArrived: boolean;
  bookedAt: string;
  /** Minutes set aside for this patient, as the server works them out. */
  minutes: number;
  /** Set only when this one patient has been given their own minutes. */
  durationMinutes: number | null;
  /** The booking's Chief Notes — shown in place of the doctor on Procedures. */
  chiefComplaint: string;
}

export interface Batch {
  id: number;
  batchDate: string;
  startTime: string;
  endTime: string;
  label: string;
  booked: number;
  totalMinutes: number;
  usedMinutes: number;
  remainingMinutes: number;
  defaultServiceMinutes: number;
  serviceDurations: Record<string, number>;
  isClosed: boolean;
  slots: BatchSlot[];
}

interface DayResponse {
  date: string;
  totalMinutes: number;
  usedMinutes: number;
  booked: number;
  data: Batch[];
}

/** One patient on the Procedures board, kept with the block they really sit in. */
export interface ProcedureRow {
  /**
   * The block the appointment physically belongs to — needed to save minutes.
   * Null when it was booked at an exact time and sits in no block; a procedure
   * is still run that day, so it belongs on the card either way.
   */
  batch: Batch | null;
  slot: BatchSlot;
}

/** A Procedures card: every patient booked for one procedure, across all blocks. */
export interface ProcedureGroup {
  procedure: string;
  rows: ProcedureRow[];
  /** Heads that still count — cancellations and no-shows are left out. */
  booked: number;
}

/** An appointment booked at an exact time, i.e. not placed in any block. */
export interface UnscheduledAppt {
  id: number;
  appointmentCode: string;
  patientName: string;
  patientCode: string;
  doctorName: string;
  service: string;
  status: string;
  type: string;
  scheduledAt: string;
  chiefComplaint: string;
  batchId: number | null;
  isArchived: boolean;
}

/**
 * The day's session blocks and the order patients are seen in each.
 *
 * Order is first come, first served by default — a booking joins the back of
 * its block. When somebody has not turned up the front desk drags them down,
 * or across into a later block, and that arrangement is what the clinic works
 * to for the rest of the day.
 */
@Component({
  selector: 'app-appointment-batches',
  standalone: false,
  templateUrl: './appointment-batches.html',
  styleUrl: './appointment-batches.scss',
})
export class AppointmentBatchesComponent implements OnInit, OnDestroy {
  batches: Batch[] = [];
  /** The day's patients grouped one card per procedure — the Procedures board. */
  procedureGroups: ProcedureGroup[] = [];
  /** Exact-time bookings for the day that sit in no block — shown on their own. */
  unscheduled: UnscheduledAppt[] = [];

  /** Every exact-time booking for the day, before either board claims its own. */
  private allUnscheduled: UnscheduledAppt[] = [];
  selectedDate = new Date();

  /** Which calendar the user came from, so the tab strip pairs Batches with it. */
  calendarMode: 'consultation' | 'procedure' = 'consultation';

  totalMinutes = 0;
  usedMinutes = 0;
  totalBooked = 0;

  isLoading = false;
  isSaving = false;
  errorMessage = '';
  noticeMessage = '';

  /** Active services with their category + clinic-wide default minutes (from the Services screen). */
  serviceCatalogue: { name: string; defaultMinutes: number; category: string }[] = [];

  /** Rows the admin has unlocked to override for this one block. */
  editingDurations = new Set<string>();

  /** Appointment ids whose minutes are currently unlocked on the board. */
  editingSlotMinutes = new Set<number>();

  /** The minutes being typed, per appointment, while a row is unlocked. */
  slotMinutesDraft: Record<number, number> = {};

  /** Ids of every block, so each list accepts drops from the others. */
  dropListIds: string[] = [];

  // --- Edit block modal ---
  isModalOpen = false;
  editingBatch: Batch | null = null;
  modalError = '';
  form: {
    startTime: string;
    endTime: string;
    isClosed: boolean;
    autoAdjust: boolean;
    defaultServiceMinutes: number;
    durations: Record<string, number>;
  } = { startTime: '09:00', endTime: '12:00', isClosed: false, autoAdjust: true, defaultServiceMinutes: 15, durations: {} };

  constructor(
    private api: ApiService,
    private cdr: ChangeDetectorRef,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.calendarMode = params.get('mode') === 'procedure' ? 'procedure' : 'consultation';
      this.cdr.markForCheck();
    });

    this.loadServices();
    this.load();

    // Keep the Active/Inactive pills honest as the clock moves, without a reload.
    this.clockTimer = setInterval(() => this.cdr.markForCheck(), 60_000);
  }

  /** The active services, with the default minutes set for each on the Services screen. */
  private loadServices(): void {
    this.api
      .get<{ data: { name: string; durationMinutes: number; category: string }[] }>('services', { status: 'active', pageSize: 200 })
      .subscribe({
        next: (res) => {
          this.serviceCatalogue = (res.data ?? [])
            .filter((s) => !!s.name)
            .map((s) => ({ name: s.name, defaultMinutes: s.durationMinutes || 15, category: s.category ?? '' }));
          if (this.isModalOpen) this.editorServices = this.buildEditorServices();
          this.cdr.markForCheck();
        },
        error: () => this.cdr.markForCheck(),
      });
  }

  // ----------------------------------------------------------
  // Loading
  // ----------------------------------------------------------

  /**
   * @param keepError Leaves a message already on screen in place. A refused
   * drag reloads the day to undo itself, and the reason it was refused has to
   * survive that reload — otherwise the board just snaps back unexplained.
   */
  load(keepError = false): void {
    this.isLoading = true;
    if (!keepError) this.errorMessage = '';

    this.api.get<DayResponse>('appointment-batches', { date: this.apiDate }).subscribe({
      next: response => {
        this.apply(response);
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.errorMessage = this.messageFrom(error, 'Could not load the schedule for this day.');
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });

    this.loadUnscheduled();
  }

  /**
   * Appointments booked for this day at an exact time — those sit in no block,
   * so the block board would otherwise never show them.
   */
  private loadUnscheduled(): void {
    this.api.get<{ data: UnscheduledAppt[] }>('appointments', { date: this.apiDate }).subscribe({
      next: res => {
        this.allUnscheduled = (res.data ?? [])
          .filter(a => a.batchId == null && !a.isArchived)
          .sort((a, b) => a.scheduledAt.localeCompare(b.scheduledAt));
        this.applyUnscheduled();
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  /**
   * Hands each exact-time booking to the board its service belongs to.
   *
   * On Consultations they stay a list of their own — a consultation really is
   * booked for a time of day. A procedure is not: it is run whenever the day's
   * list reaches it, so on Procedures these are folded into the card for their
   * procedure instead of being stranded in a separate "exact-time" strip.
   */
  private applyUnscheduled(): void {
    this.unscheduled = this.isProcedureMode
      ? []
      : this.allUnscheduled.filter(a => this.inMode(a.service));
    this.procedureGroups = this.buildProcedureGroups();
    this.recount();
  }

  /** An exact-time booking dressed as a board row, so one card draws both kinds. */
  private slotFrom(appt: UnscheduledAppt): BatchSlot {
    return {
      appointmentId: appt.id,
      appointmentCode: appt.appointmentCode,
      position: 0,
      patientId: 0,
      patientName: appt.patientName,
      patientCode: appt.patientCode,
      doctorName: appt.doctorName,
      service: appt.service,
      status: appt.status,
      type: appt.type,
      hasArrived: false,
      bookedAt: appt.scheduledAt,
      minutes: 0,
      durationMinutes: null,
      chiefComplaint: appt.chiefComplaint ?? '',
    };
  }

  /** Services that belong to the Procedures board; everything else is a consultation. */
  private readonly procedureServiceSet = new Set(PROCEDURE_SERVICES);

  /** Whether a booked service belongs on this board (procedure vs consultation). */
  private inMode(service: string): boolean {
    const isProcedure = this.procedureServiceSet.has(service);
    return this.isProcedureMode ? isProcedure : !isProcedure;
  }

  /**
   * Keeps only the slots that belong on this board and re-tallies each block
   * from them, so the Consultations board never shows procedures and vice
   * versa. The server still holds the full block; this is the view onto it.
   */
  private scopeToMode(batches: Batch[]): Batch[] {
    return batches.map(batch => {
      const slots = batch.slots.filter(slot => this.inMode(slot.service));
      let seen = 0;
      let used = 0;
      for (const slot of slots) {
        if (this.isReleased(slot.status)) {
          slot.position = 0;
        } else {
          slot.position = ++seen;
          used += slot.minutes;
        }
      }
      return {
        ...batch,
        slots,
        booked: seen,
        usedMinutes: used,
        remainingMinutes: Math.max(0, batch.totalMinutes - used),
      };
    });
  }

  private apply(response: DayResponse): void {
    this.batches = this.scopeToMode(response.data ?? []);
    // Block windows are unchanged; only the usage and head count are this
    // board's own, summed from the slots that belong to it.
    this.totalMinutes = response.totalMinutes ?? 0;
    this.usedMinutes = this.batches.reduce((sum, b) => sum + b.usedMinutes, 0);
    this.dropListIds = this.batches.map(b => this.dropListId(b));
    this.procedureGroups = this.buildProcedureGroups();
    this.recount();
  }

  /**
   * The day's head count for this board. On Procedures it is summed from the
   * cards, which take in exact-time bookings the blocks know nothing about.
   */
  private recount(): void {
    this.totalBooked = this.isProcedureMode
      ? this.procedureGroups.reduce((sum, g) => sum + g.booked, 0)
      : this.batches.reduce((sum, b) => sum + b.booked, 0);
  }

  // ----------------------------------------------------------
  // Procedure cards
  // ----------------------------------------------------------

  get isProcedureMode(): boolean {
    return this.calendarMode === 'procedure';
  }

  /**
   * One card per procedure on the Procedures board, each gathering the day's
   * patients booked for that procedure out of whichever block they sit in.
   * Rebuilt field — not a getter: a fresh array each change-detection pass
   * never lets the *ngFor settle under zoneless CD and freezes the page.
   */
  private buildProcedureGroups(): ProcedureGroup[] {
    return PROCEDURE_BATCH_CARDS.map(procedure => {
      const rows: ProcedureRow[] = [];
      for (const batch of this.batches) {
        for (const slot of batch.slots) {
          if (slot.service === procedure) rows.push({ batch, slot });
        }
      }
      // Booked at an exact time, so in no block — still this procedure's work.
      for (const appt of this.allUnscheduled) {
        if (appt.service === procedure) rows.push({ batch: null, slot: this.slotFrom(appt) });
      }
      const booked = rows.filter(r => !this.isReleased(r.slot.status)).length;
      return { procedure, rows, booked };
    });
  }

  trackProcedureGroup = (_: number, group: ProcedureGroup) => group.procedure;
  trackProcedureRow = (_: number, row: ProcedureRow) => row.slot.appointmentId;

  shiftDay(days: number): void {
    const next = new Date(this.selectedDate);
    next.setDate(next.getDate() + days);
    this.selectedDate = next;
    this.load();
  }

  goToToday(): void {
    this.selectedDate = new Date();
    this.load();
  }

  onDateChange(value: string): void {
    if (!value) return;
    const [year, month, day] = value.split('-').map(Number);
    this.selectedDate = new Date(year, month - 1, day);
    this.load();
  }

  // ----------------------------------------------------------
  // Drag and drop
  // ----------------------------------------------------------

  dropListId(batch: Batch): string {
    return `batch-${batch.id}`;
  }

  /**
   * A block that is closed, or already at capacity, refuses drops from
   * elsewhere. Reordering inside it stays allowed — being full is a reason not
   * to take another patient, not a reason to freeze the order of the ones in it.
   */
  canReceive = (drag: any, drop: any): boolean => {
    const target = this.batches.find(b => this.dropListId(b) === drop.id);
    if (!target) return false;
    if (drag.dropContainer.id === drop.id) return true;
    if (target.isClosed) return false;

    // Room is time: the dragged patient's service has to fit the minutes left.
    const slot = drag.data as BatchSlot | undefined;
    const need = slot && !this.isReleased(slot.status) ? this.slotCost(target, slot) : 0;
    return target.remainingMinutes >= need;
  };

  /**
   * Minutes a service takes in a block: the block's own override, then the
   * clinic-wide default from the Services screen, then the block's catch-all.
   * Mirrors the server so the board's arithmetic agrees with what it will save.
   */
  minutesFor(batch: Batch, service: string): number {
    const mapped = batch.serviceDurations?.[service];
    if (mapped && mapped > 0) return mapped;

    const catalogue = this.serviceCatalogue.find(s => s.name === service)?.defaultMinutes;
    if (catalogue && catalogue > 0) return catalogue;

    return Math.max(0, batch.defaultServiceMinutes);
  }

  /**
   * Minutes a booked patient takes: their own, else their service's. With no
   * block (an exact-time booking) there is no per-block override to apply, so
   * the clinic-wide default for the service stands.
   */
  slotCost(batch: Batch | null, slot: BatchSlot): number {
    if (slot.durationMinutes && slot.durationMinutes > 0) return slot.durationMinutes;
    return batch ? this.minutesFor(batch, slot.service) : this.defaultFor(slot.service);
  }

  onDrop(event: CdkDragDrop<BatchSlot[]>): void {
    if (event.previousContainer === event.container && event.previousIndex === event.currentIndex) {
      return;
    }

    // Move it in the local model first so the board responds immediately, then
    // persist. A rejected save reloads, which puts everything back.
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex,
      );
    }

    this.renumber();
    this.saveArrangement();
  }

  /**
   * Keeps the numbers and counts honest between the drop and the save, using
   * the same rule as the server: cancellations and no-shows take no number.
   */
  private renumber(): void {
    for (const batch of this.batches) {
      let seen = 0;
      let used = 0;
      for (const slot of batch.slots) {
        if (this.isReleased(slot.status)) {
          slot.position = 0;
        } else {
          slot.position = ++seen;
          used += this.slotCost(batch, slot);
        }
      }
      batch.booked = seen;
      batch.usedMinutes = used;
      batch.remainingMinutes = Math.max(0, batch.totalMinutes - used);
    }
    this.totalBooked = this.batches.reduce((sum, b) => sum + b.booked, 0);
    this.usedMinutes = this.batches.reduce((sum, b) => sum + b.usedMinutes, 0);
  }

  private saveArrangement(): void {
    this.isSaving = true;
    this.errorMessage = '';
    this.noticeMessage = '';

    const payload = {
      date: this.apiDate,
      // Scope the save to this board so the other category's bookings in these
      // blocks are left untouched by a drag here.
      mode: this.calendarMode,
      batches: this.batches.map(batch => ({
        batchId: batch.id,
        appointmentIds: batch.slots.map(slot => slot.appointmentId),
      })),
    };

    this.api.post<DayResponse>('appointment-batches/arrange', payload).subscribe({
      next: response => {
        this.apply(response);
        this.isSaving = false;
        this.flashNotice('Order saved.');
        this.cdr.markForCheck();
      },
      error: error => {
        this.errorMessage = this.messageFrom(error, 'Could not save the new order.');
        this.isSaving = false;
        // The server refused it, so the board must not keep showing the move.
        // Reload to undo it, keeping the reason on screen.
        this.load(true);
      },
    });
  }

  // ----------------------------------------------------------
  // Editing a block
  // ----------------------------------------------------------

  openEdit(batch: Batch): void {
    this.editingBatch = batch;
    this.editingDurations.clear();
    this.form = {
      startTime: batch.startTime,
      endTime: batch.endTime,
      isClosed: batch.isClosed,
      autoAdjust: true,
      defaultServiceMinutes: batch.defaultServiceMinutes || 15,
      durations: { ...(batch.serviceDurations ?? {}) },
    };
    this.editorServices = this.buildEditorServices();
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.editingBatch = null;
  }

  /**
   * Rows for the edit modal's duration table. A FIELD, rebuilt when the sheet
   * opens — not a getter: a getter returning a fresh array each call never lets
   * the *ngFor settle under zoneless change detection, which froze the page.
   */
  editorServices: { name: string; defaultMinutes: number }[] = [];

  trackEditorService = (_: number, s: { name: string }) => s.name;

  private buildEditorServices(): { name: string; defaultMinutes: number }[] {
    // The Procedures calendar only books Ultrasound / IUI / Procedure, so its
    // blocks map only those.
    if (this.calendarMode === 'procedure') {
      return PROCEDURE_SERVICES.map((name) => ({
        name,
        defaultMinutes: this.serviceCatalogue.find((s) => s.name === name)?.defaultMinutes ?? 15,
      }));
    }

    // Consultations: only services filed under the "Consultation" category.
    return this.serviceCatalogue
      .filter((s) => s.category === 'Consultation')
      .map((s) => ({ name: s.name, defaultMinutes: s.defaultMinutes }));
  }

  /** The service's clinic-wide default, falling back to the block's catch-all. */
  defaultFor(service: string): number {
    const found = this.serviceCatalogue.find((s) => s.name === service);
    return found ? found.defaultMinutes : (this.form.defaultServiceMinutes || 15);
  }

  /** This block's override for a service, or null when it uses the default. */
  getDuration(service: string): number | null {
    const v = this.form.durations[service];
    return v && v > 0 ? v : null;
  }

  setDuration(service: string, value: number | string): void {
    const minutes = Number(value);
    if (minutes && minutes > 0) this.form.durations[service] = minutes;
    else delete this.form.durations[service];
  }

  isEditingDuration(service: string): boolean {
    return this.editingDurations.has(service);
  }

  /** Pencil toggle: unlock a row to override it for this block, or lock it back. */
  toggleDurationEdit(service: string): void {
    if (this.editingDurations.has(service)) this.editingDurations.delete(service);
    else this.editingDurations.add(service);
  }

  saveBatch(): void {
    if (!this.editingBatch) return;

    this.isSaving = true;
    this.modalError = '';

    const editedId = this.editingBatch.id;
    const endChanged = this.form.endTime !== this.editingBatch.endTime;

    const body = {
      startTime: this.form.startTime,
      endTime: this.form.endTime,
      isClosed: this.form.isClosed,
      serviceDurations: this.cleanDurations(),
      defaultServiceMinutes: this.form.defaultServiceMinutes || 15,
    };

    this.api.put<DayResponse>(`appointment-batches/${editedId}`, body).subscribe({
      next: response => {
        this.apply(response);

        // Auto-adjust: having moved this block's end, push the later blocks
        // down so the day stays back-to-back. Second call, run off the saved day.
        if (this.form.autoAdjust && endChanged && this.hasLaterBlocks(editedId)) {
          this.cascadeFrom(editedId);
          return;
        }

        this.isSaving = false;
        this.closeModal();
        this.cdr.markForCheck();
      },
      error: error => {
        this.modalError = this.messageFrom(error, 'Could not save this block.');
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  private hasLaterBlocks(editedId: number): boolean {
    const ordered = this.orderedByStart();
    const idx = ordered.findIndex(b => b.id === editedId);
    return idx >= 0 && idx < ordered.length - 1;
  }

  /** Shifts every block after the edited one to run straight on from it. */
  private cascadeFrom(editedId: number): void {
    const ordered = this.orderedByStart();
    const idx = ordered.findIndex(b => b.id === editedId);

    const updates = ordered.map(b => ({ id: b.id, startTime: b.startTime, endTime: b.endTime }));

    let cursor = this.parseHHmm(ordered[idx].endTime);
    for (let i = idx + 1; i < ordered.length; i++) {
      const duration = Math.max(0, this.parseHHmm(ordered[i].endTime) - this.parseHHmm(ordered[i].startTime));
      updates[i] = {
        id: ordered[i].id,
        startTime: this.toHHmm(cursor),
        endTime: this.toHHmm(cursor + duration),
      };
      cursor += duration;
    }

    this.api
      .put<DayResponse>('appointment-batches/day', { date: this.apiDate, blocks: updates })
      .subscribe({
        next: response => {
          this.apply(response);
          this.isSaving = false;
          this.closeModal();
          this.flashNotice('Later blocks shifted to match.');
          this.cdr.markForCheck();
        },
        error: error => {
          this.errorMessage = this.messageFrom(error, 'Saved the block, but could not shift the later ones.');
          this.isSaving = false;
          this.closeModal();
          this.cdr.markForCheck();
        },
      });
  }

  toggleClosed(batch: Batch): void {
    this.isSaving = true;
    this.errorMessage = '';

    const body = {
      startTime: batch.startTime,
      endTime: batch.endTime,
      isClosed: !batch.isClosed,
      serviceDurations: batch.serviceDurations ?? {},
      defaultServiceMinutes: batch.defaultServiceMinutes || 15,
    };

    this.api.put<DayResponse>(`appointment-batches/${batch.id}`, body).subscribe({
      next: response => {
        this.apply(response);
        this.isSaving = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.errorMessage = this.messageFrom(error, 'Could not update this block.');
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  private cleanDurations(): Record<string, number> {
    const out: Record<string, number> = {};
    for (const [service, value] of Object.entries(this.form.durations)) {
      const minutes = Number(value);
      if (service && minutes > 0) out[service] = minutes;
    }
    return out;
  }

  private orderedByStart(): Batch[] {
    return [...this.batches].sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  /** "HH:mm" → minutes past midnight. */
  private parseHHmm(value: string): number {
    const [h, m] = (value || '0:0').split(':').map(Number);
    return (h || 0) * 60 + (m || 0);
  }

  /** Minutes past midnight → "HH:mm", clamped to a single day. */
  private toHHmm(total: number): string {
    const clamped = Math.max(0, Math.min(23 * 60 + 59, Math.round(total)));
    const h = Math.floor(clamped / 60);
    const m = clamped % 60;
    return `${`${h}`.padStart(2, '0')}:${`${m}`.padStart(2, '0')}`;
  }

  // ----------------------------------------------------------
  // One patient's minutes
  // ----------------------------------------------------------

  isEditingSlotMinutes(slot: BatchSlot): boolean {
    return this.editingSlotMinutes.has(slot.appointmentId);
  }

  /** Pencil: unlock this patient's minutes, starting from what they have now. */
  startSlotEdit(batch: Batch | null, slot: BatchSlot): void {
    this.slotMinutesDraft[slot.appointmentId] = this.slotCost(batch, slot);
    this.editingSlotMinutes.add(slot.appointmentId);
  }

  cancelSlotEdit(slot: BatchSlot): void {
    this.editingSlotMinutes.delete(slot.appointmentId);
    delete this.slotMinutesDraft[slot.appointmentId];
  }

  /**
   * Saves minutes for this one patient. The procedure's default is left alone
   * — typing the default back in simply hands the patient back to it.
   */
  saveSlotMinutes(batch: Batch | null, slot: BatchSlot): void {
    // Per-patient minutes only mean something inside a block's budget; the
    // board hides the pencil without one, so this is just the matching guard.
    if (!batch) {
      this.cancelSlotEdit(slot);
      return;
    }

    const minutes = Number(this.slotMinutesDraft[slot.appointmentId]);

    if (!Number.isFinite(minutes) || minutes < 1) {
      this.errorMessage = 'Minutes have to be a whole number of at least 1.';
      return;
    }

    const rounded = Math.round(minutes);
    const body = { minutes: rounded === this.minutesFor(batch, slot.service) ? null : rounded };

    this.isSaving = true;
    this.errorMessage = '';
    this.noticeMessage = '';

    this.api
      .put<DayResponse>(`appointment-batches/slots/${slot.appointmentId}/duration`, body)
      .subscribe({
        next: response => {
          this.apply(response);
          this.cancelSlotEdit(slot);
          this.isSaving = false;
          this.flashNotice('Minutes saved.');
          this.cdr.markForCheck();
        },
        error: error => {
          this.errorMessage = this.messageFrom(error, 'Could not save those minutes.');
          this.isSaving = false;
          this.cdr.markForCheck();
        },
      });
  }

  // ----------------------------------------------------------
  // Display
  // ----------------------------------------------------------

  get apiDate(): string {
    const d = this.selectedDate;
    const month = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
  }

  get isToday(): boolean {
    return this.apiDate === this.formatDate(new Date());
  }

  private formatDate(d: Date): string {
    const month = `${d.getMonth() + 1}`.padStart(2, '0');
    const day = `${d.getDate()}`.padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
  }

  isReleased(status: string): boolean {
    return status === 'Cancelled' || status === 'NoShow';
  }

  /**
   * A block is Active when the wall-clock time falls inside its window on
   * today's date — i.e. it is the session running right now. Every other block
   * (earlier today, later today, or any other day) is Inactive.
   */
  isActiveNow(batch: Batch): boolean {
    if (!this.isToday) return false;
    const now = new Date();
    const minutes = now.getHours() * 60 + now.getMinutes();
    return minutes >= this.parseHHmm(batch.startTime) && minutes < this.parseHHmm(batch.endTime);
  }

  statusClass(status: string): string {
    const map: Record<string, string> = {
      Scheduled: 'pill pill--info',
      Confirmed: 'pill pill--info',
      CheckedIn: 'pill pill--success',
      InProgress: 'pill pill--warning',
      Completed: 'pill pill--success',
      Pending: 'pill pill--neutral',
      Cancelled: 'pill pill--danger',
      NoShow: 'pill pill--danger',
    };
    return map[status] ?? 'pill pill--neutral';
  }

  /** "CheckedIn" reads badly in a list; the rest are already words. */
  statusLabel(status: string): string {
    if (status === 'CheckedIn') return 'Checked in';
    if (status === 'InProgress') return 'In progress';
    if (status === 'NoShow') return 'No show';
    return status;
  }

  trackBatch = (_: number, batch: Batch) => batch.id;
  trackSlot = (_: number, slot: BatchSlot) => slot.appointmentId;

  private messageFrom(error: any, fallback: string): string {
    return error?.error?.message || fallback;
  }

  /**
   * A drag saves itself with no button to press, so it needs to say so. The
   * confirmation clears itself rather than lingering over the next drag.
   */
  private noticeTimer?: ReturnType<typeof setTimeout>;
  private clockTimer?: ReturnType<typeof setInterval>;

  private flashNotice(message: string): void {
    this.noticeMessage = message;
    clearTimeout(this.noticeTimer);

    this.noticeTimer = setTimeout(() => {
      this.noticeMessage = '';
      // Zoneless: a bare timer callback would not trigger a re-render.
      this.cdr.markForCheck();
    }, 2500);
  }

  ngOnDestroy(): void {
    clearTimeout(this.noticeTimer);
    clearInterval(this.clockTimer);
  }
}
