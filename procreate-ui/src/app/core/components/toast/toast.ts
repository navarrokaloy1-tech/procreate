import { Component, inject } from '@angular/core';
import { ToastService } from '../../services/toast';

/**
 * Global toast host. Rendered once at the app root; feature code emits
 * through ToastService. Standalone so lazy feature modules can import it too.
 */
@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="toast-stack" aria-live="polite" aria-atomic="true">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class.toast--success]="toast.kind === 'success'" [class.toast--error]="toast.kind === 'error'" role="status">
          <span class="toast__icon" aria-hidden="true">{{ toast.kind === 'success' ? '✓' : '!' }}</span>
          <span class="toast__message">{{ toast.message }}</span>
          <button type="button" class="toast__close" aria-label="Dismiss" (click)="toastService.dismiss(toast.id)">&times;</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-stack {
      position: fixed;
      right: 24px;
      bottom: 24px;
      z-index: 2000;
      display: flex;
      flex-direction: column;
      gap: 10px;
      align-items: flex-end;
      pointer-events: none;
    }

    .toast {
      pointer-events: auto;
      display: flex;
      align-items: center;
      gap: 14px;
      min-width: 320px;
      max-width: 440px;
      min-height: 72px;
      padding: 18px 20px;
      border-radius: 12px;
      border-left: 6px solid transparent;
      background: #ffffff;
      box-shadow: 0 14px 34px rgba(15, 23, 42, 0.22);
      font-size: 1rem;
      font-weight: 600;
      line-height: 1.4;
      animation: toast-in 180ms ease-out;
    }

    .toast--success {
      border-left-color: #16a34a;
      background: #ecfdf5;
      color: #14532d;
    }

    .toast--error {
      border-left-color: #dc2626;
      background: #fef2f2;
      color: #7f1d1d;
    }

    .toast__icon {
      flex: none;
      width: 28px;
      height: 28px;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 1rem;
      font-weight: 700;
      color: #ffffff;
    }

    .toast--success .toast__icon { background: #16a34a; }
    .toast--error .toast__icon { background: #dc2626; }

    .toast__message { flex: 1; }

    .toast__close {
      flex: none;
      border: 0;
      background: transparent;
      color: inherit;
      opacity: 0.6;
      font-size: 1.4rem;
      line-height: 1;
      cursor: pointer;
      padding: 0 4px;
    }

    .toast__close:hover { opacity: 1; }

    @keyframes toast-in {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (prefers-reduced-motion: reduce) {
      .toast { animation: none; }
    }
  `]
})
export class ToastComponent {
  protected readonly toastService = inject(ToastService);
}
