import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title?: string;
  message: string;
  detail?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** 'danger' paints the confirm button red; use it for destructive actions. */
  tone?: 'danger' | 'primary';
}

interface ConfirmRequest extends Required<Omit<ConfirmOptions, 'detail'>> {
  detail: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly request = signal<ConfirmRequest | null>(null);

  private resolver: ((answer: boolean) => void) | null = null;

  ask(options: ConfirmOptions): Promise<boolean> {
    // A second ask while one is open would strand the first caller.
    this.settle(false);

    this.request.set({
      title: options.title ?? 'Please confirm',
      message: options.message,
      detail: options.detail ?? '',
      confirmLabel: options.confirmLabel ?? 'Confirm',
      cancelLabel: options.cancelLabel ?? 'Cancel',
      tone: options.tone ?? 'danger',
    });

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  accept(): void {
    this.settle(true);
  }

  dismiss(): void {
    this.settle(false);
  }

  private settle(answer: boolean): void {
    const resolve = this.resolver;
    this.resolver = null;
    this.request.set(null);
    resolve?.(answer);
  }
}
