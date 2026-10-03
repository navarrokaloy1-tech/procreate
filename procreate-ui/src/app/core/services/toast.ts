import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);

  private nextId = 1;
  private timers = new Map<number, ReturnType<typeof setTimeout>>();

  success(message: string, durationMs = 3500): void {
    this.show('success', message, durationMs);
  }

  error(message: string, durationMs = 5000): void {
    this.show('error', message, durationMs);
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(kind: ToastKind, message: string, durationMs: number): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { id, kind, message }]);
    this.timers.set(id, setTimeout(() => this.dismiss(id), durationMs));
  }
}
