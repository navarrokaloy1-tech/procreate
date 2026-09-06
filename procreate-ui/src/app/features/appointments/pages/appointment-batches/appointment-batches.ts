import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { ApiService } from '../../../../core/services/api';

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
}

export interface Batch {
  id: number;
  batchDate: string;
  startTime: string;
  endTime: string;
  label: string;
  capacity: number;
  booked: number;
  remaining: number;
  isClosed: boolean;
  slots: BatchSlot[];
}

interface DayResponse {
  date: string;
  capacity: number;
  booked: number;
  data: Batch[];
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
  selectedDate = new Date();

  totalCapacity = 0;
  totalBooked = 0;

  isLoading = false;
  isSaving = false;
  errorMessage = '';
  noticeMessage = '';

  /** Ids of every block, so each list accepts drops from the others. */
  dropListIds: string[] = [];

  // --- Edit block modal ---
  isModalOpen = false;
  editingBatch: Batch | null = null;
  modalError = '';
  form = { startTime: '09:00', endTime: '12:00', capacity: 5, isClosed: false };

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.load();
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
  }

  private apply(response: DayResponse): void {
    this.batches = response.data ?? [];
    this.totalCapacity = response.capacity ?? 0;
    this.totalBooked = response.booked ?? 0;
    this.dropListIds = this.batches.map(b => this.dropListId(b));
  }

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
    return !target.isClosed && target.remaining > 0;
  };

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
      for (const slot of batch.slots) {
        slot.position = this.isReleased(slot.status) ? 0 : ++seen;
      }
      batch.booked = seen;
      batch.remaining = Math.max(0, batch.capacity - seen);
    }
    this.totalBooked = this.batches.reduce((sum, b) => sum + b.booked, 0);
  }

  private saveArrangement(): void {
    this.isSaving = true;
    this.errorMessage = '';
    this.noticeMessage = '';

    const payload = {
      date: this.apiDate,
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
    this.form = {
      startTime: batch.startTime,
      endTime: batch.endTime,
      capacity: batch.capacity,
      isClosed: batch.isClosed,
    };
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.editingBatch = null;
  }

  saveBatch(): void {
    if (!this.editingBatch) return;

    this.isSaving = true;
    this.modalError = '';

    this.api
      .put<DayResponse>(`appointment-batches/${this.editingBatch.id}`, this.form)
      .subscribe({
        next: response => {
          this.apply(response);
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

  toggleClosed(batch: Batch): void {
    this.isSaving = true;
    this.errorMessage = '';

    const body = {
      startTime: batch.startTime,
      endTime: batch.endTime,
      capacity: batch.capacity,
      isClosed: !batch.isClosed,
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
  }
}
