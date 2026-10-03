import { Component, HostListener, inject } from '@angular/core';
import { IconComponent } from '../icon/icon';
import { ConfirmService } from '../../services/confirm';

/**
 * Global confirmation dialog, replacing window.confirm so prompts match the
 * app rather than the browser. Rendered once at the app root; callers go
 * through ConfirmService.ask().
 */
@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [IconComponent],
  template: `
    @if (confirmService.request(); as request) {
      <div class="modal-backdrop confirm-backdrop" (click)="confirmService.dismiss()">
        <div
          class="modal modal-sm confirm"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirmTitle"
          (click)="$event.stopPropagation()"
        >
          <div class="confirm__body">
            <span class="confirm__icon" [class.confirm__icon--danger]="request.tone === 'danger'">
              <app-icon name="alert-circle" [size]="26"></app-icon>
            </span>
            <div class="confirm__text">
              <h3 class="confirm__title" id="confirmTitle">{{ request.title }}</h3>
              <p class="confirm__message">{{ request.message }}</p>
              @if (request.detail) {
                <p class="confirm__detail">{{ request.detail }}</p>
              }
            </div>
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" (click)="confirmService.dismiss()">
              {{ request.cancelLabel }}
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-danger]="request.tone === 'danger'"
              [class.btn-primary]="request.tone !== 'danger'"
              autofocus
              (click)="confirmService.accept()"
            >
              {{ request.confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .confirm-backdrop {
      z-index: 1200;
    }

    .confirm {
      z-index: 1210;
    }

    .confirm__body {
      display: flex;
      gap: 16px;
      padding: 1.75rem 1.5rem 1.25rem;
    }

    .confirm__icon {
      flex: none;
      width: 48px;
      height: 48px;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: rgba(13, 110, 253, 0.12);
      color: var(--primary);
    }

    .confirm__icon--danger {
      background: rgba(220, 53, 69, 0.12);
      color: var(--danger);
    }

    .confirm__text {
      flex: 1;
      min-width: 0;
    }

    .confirm__title {
      margin: 0 0 0.35rem;
      font-size: var(--font-size-lg);
    }

    .confirm__message {
      margin: 0;
      color: var(--text-primary);
      line-height: 1.5;
      overflow-wrap: anywhere;
    }

    .confirm__detail {
      margin: 0.5rem 0 0;
      color: var(--text-muted);
      font-size: var(--font-size-sm);
      line-height: 1.5;
    }
  `]
})
export class ConfirmDialogComponent {
  protected readonly confirmService = inject(ConfirmService);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.confirmService.request()) this.confirmService.dismiss();
  }
}
