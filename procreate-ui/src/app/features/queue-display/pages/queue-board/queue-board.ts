import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { Subscription, interval } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

interface QueueEntry {
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

/**
 * The waiting-room board. Opened on a second screen from the sidebar, so it
 * runs outside the console shell: no nav, no chrome, large type, and it
 * refreshes itself rather than expecting anyone to touch it.
 */
@Component({
  selector: 'app-queue-board',
  standalone: false,
  templateUrl: './queue-board.html',
  styleUrl: './queue-board.scss',
})
export class QueueBoardComponent implements OnInit, OnDestroy {
  entries: QueueEntry[] = [];
  waiting = 0;
  served = 0;

  clock = new Date();
  hasLoaded = false;
  isOffline = false;

  private subscriptions = new Subscription();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.load();

    // The board is unattended, so it polls rather than waiting for a click.
    this.subscriptions.add(interval(10000).subscribe(() => this.load()));
    this.subscriptions.add(
      interval(1000).subscribe(() => {
        this.clock = new Date();
        this.cdr.markForCheck();
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  load(): void {
    this.api.get<QueueResponse>('queue').subscribe({
      next: (res) => {
        this.entries = res.data ?? [];
        this.waiting = res.waiting;
        this.served = res.served;
        this.hasLoaded = true;
        this.isOffline = false;
        this.cdr.markForCheck();
      },
      error: () => {
        // Keep showing the last good numbers; a blank board in a waiting room
        // is worse than a slightly stale one.
        this.isOffline = true;
        this.hasLoaded = true;
        this.cdr.markForCheck();
      },
    });
  }

  /** The ticket at the desk: the most recently called one. */
  get nowServing(): QueueEntry | null {
    const called = this.entries.filter((e) => e.status === 'Called');
    if (called.length === 0) return null;

    return called.reduce((latest, entry) =>
      (entry.calledAt ?? '') > (latest.calledAt ?? '') ? entry : latest
    );
  }

  /** The next few tickets, so people can judge how long they have. */
  get upNext(): QueueEntry[] {
    return this.entries.filter((e) => e.status === 'Waiting').slice(0, 6);
  }

  /** Ticket numbers read at a distance, so they are zero-padded. */
  ticket(entry: QueueEntry): string {
    return String(entry.queueNumber).padStart(3, '0');
  }

  /** Only the first name and an initial go on a public screen. */
  displayName(entry: QueueEntry): string {
    const parts = (entry.patientName ?? '').trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return 'Guest';
    if (parts.length === 1) return parts[0];
    return `${parts[0]} ${parts[parts.length - 1][0]}.`;
  }
}
