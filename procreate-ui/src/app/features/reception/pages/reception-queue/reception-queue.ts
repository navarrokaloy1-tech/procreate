import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ApiService } from '../../../../core/services/api';

export interface QueueEntry {
  id: number;
  queueNumber: number;
  patientName: string;
  patientId: number | null;
  status: string;
  addedAt: string;
  calledAt: string | null;
}

interface QueueResponse {
  date: string;
  waiting: number;
  called: number;
  served: number;
  data: QueueEntry[];
}

@Component({
  selector: 'app-reception-queue',
  standalone: false,
  templateUrl: './reception-queue.html',
  styleUrl: './reception-queue.scss',
})
export class ReceptionQueueComponent implements OnInit {
  entries: QueueEntry[] = [];
  queueDate = new Date();
  waiting = 0;
  called = 0;
  served = 0;

  newName = '';
  isLoading = false;
  isSaving = false;
  errorMessage = '';

  /** The ticket currently rendered into the print-only slip. */
  slipEntry: QueueEntry | null = null;

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadQueue();
  }

  loadQueue(): void {
    this.isLoading = true;
    this.api.get<QueueResponse>('queue').subscribe({
      next: (res) => {
        this.entries = res.data;
        this.queueDate = new Date(res.date);
        this.waiting = res.waiting;
        this.called = res.called;
        this.served = res.served;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not load the queue. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  /** Issue the next ticket, then send it straight to the printer. */
  addToQueue(): void {
    const name = this.newName.trim();
    if (!name || this.isSaving) return;

    this.isSaving = true;
    this.errorMessage = '';

    this.api.post<QueueEntry>('queue', { patientName: name }).subscribe({
      next: (entry) => {
        this.newName = '';
        this.isSaving = false;
        this.loadQueue();
        this.printSlip(entry);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Could not add to the queue.';
        this.isSaving = false;
        this.cdr.markForCheck();
      },
    });
  }

  callNext(): void {
    this.errorMessage = '';
    this.api.post<QueueEntry>('queue/call-next', {}).subscribe({
      next: () => {
        this.loadQueue();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Nobody is waiting.';
        this.cdr.markForCheck();
      },
    });
  }

  setStatus(entry: QueueEntry, status: string): void {
    this.api.patch<QueueEntry>(`queue/${entry.id}/status`, { status }).subscribe({
      next: () => {
        this.loadQueue();
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not update that ticket.';
        this.cdr.markForCheck();
      },
    });
  }

  remove(entry: QueueEntry): void {
    if (!window.confirm(`Remove queue number ${entry.queueNumber} (${entry.patientName})?`)) return;

    this.api.delete<void>(`queue/${entry.id}`).subscribe({
      next: () => {
        this.loadQueue();
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not remove that ticket.';
        this.cdr.markForCheck();
      },
    });
  }

  /**
   * Renders the ticket into the print-only slip and opens the print dialog.
   * The body class lets the global print rules hide the rest of the console.
   */
  printSlip(entry: QueueEntry): void {
    this.slipEntry = entry;
    this.cdr.detectChanges();

    document.body.classList.add('printing-slip');

    const cleanup = () => {
      document.body.classList.remove('printing-slip');
      this.slipEntry = null;
      this.cdr.markForCheck();
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);
    window.print();

    // Safety net for browsers that never fire afterprint.
    setTimeout(cleanup, 1000);
  }

  statusPill(status: string): string {
    switch (status) {
      case 'Waiting': return 'pill pill--warning';
      case 'Called': return 'pill pill--success';
      case 'Served': return 'pill pill--neutral';
      case 'Skipped': return 'pill pill--danger';
      default: return 'pill pill--neutral';
    }
  }
}
