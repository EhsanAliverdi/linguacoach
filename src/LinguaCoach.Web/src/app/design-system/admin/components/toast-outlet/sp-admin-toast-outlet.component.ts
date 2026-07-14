import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../../core/services/toast.service';

@Component({
  selector: 'sp-admin-toast-outlet',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="sp-to-host" aria-live="polite" aria-label="Notifications">
      @for (toast of toastService.messages(); track toast.id) {
        <div
          class="sp-to-item"
          [class.sp-to-success]="toast.kind === 'success'"
          [class.sp-to-error]="toast.kind === 'error'"
          [class.sp-to-warning]="toast.kind === 'warning'"
          [class.sp-to-info]="toast.kind === 'info' || toast.kind === 'progress'"
          [class.sp-to-progress]="toast.kind === 'progress'"
          role="status"
        >
          @if (toast.kind === 'progress') {
            <div class="sp-to-progress-body">
              <span class="sp-to-msg">{{ toast.message }}</span>
              <div class="sp-to-progress-track">
                <div class="sp-to-progress-fill"
                     [style.width.%]="toast.progressTotal ? (toast.progressCurrent! / toast.progressTotal * 100) : 0"></div>
              </div>
            </div>
          } @else {
            <span class="sp-to-msg">{{ toast.message }}</span>
          }
          <button
            type="button"
            class="sp-to-dismiss"
            (click)="toastService.dismiss(toast.id)"
            aria-label="Dismiss notification"
          >
            <svg width="12" height="12" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" viewBox="0 0 24 24">
              <path d="M18 6 6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>
      }
    </section>
  `,
  styles: [`
    .sp-to-host {
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: var(--sp-admin-z-toast);
      display: flex;
      flex-direction: column;
      gap: 8px;
      pointer-events: none;
    }
    .sp-to-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px 14px;
      border-radius: var(--sp-admin-radius-md);
      font-size: 13px;
      font-weight: 600;
      box-shadow: 0 4px 16px rgba(0,0,0,.12);
      pointer-events: all;
      min-width: 240px;
      max-width: 360px;
    }
    .sp-to-success { background: var(--sp-admin-green-bg);   color: var(--sp-admin-green);   border: 1px solid var(--sp-admin-green-ring); }
    .sp-to-error   { background: var(--sp-admin-danger-bg);  color: var(--sp-admin-danger);  border: 1px solid #FECACA; }
    .sp-to-warning { background: var(--sp-admin-amber-bg);  color: var(--sp-admin-amber);   border: 1px solid #FDE68A; }
    .sp-to-info    { background: var(--sp-admin-primary-bg); color: var(--sp-admin-primary); border: 1px solid var(--sp-admin-primary-focus); }
    .sp-to-msg { flex: 1; }
    .sp-to-progress { align-items: flex-start; }
    .sp-to-progress-body { flex: 1; display: flex; flex-direction: column; gap: 6px; }
    .sp-to-progress-track { height: 5px; background: rgba(0,0,0,.1); border-radius: 3px; overflow: hidden; }
    .sp-to-progress-fill { height: 100%; background: currentColor; transition: width .2s; }
    .sp-to-dismiss {
      background: none;
      border: none;
      cursor: pointer;
      color: inherit;
      opacity: .6;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2px;
      flex-shrink: 0;
    }
    .sp-to-dismiss:hover { opacity: 1; }
  `],
})
export class SpAdminToastOutletComponent {
  constructor(public toastService: ToastService) {}
}

