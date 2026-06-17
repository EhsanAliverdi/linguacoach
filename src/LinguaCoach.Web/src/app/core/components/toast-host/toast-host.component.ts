import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-toast-host',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="sp-toast-host" aria-live="polite" aria-label="Notifications">
      @for (toast of toastService.messages(); track toast.id) {
        <div class="sp-toast" [class.sp-toast-success]="toast.kind === 'success'" [class.sp-toast-error]="toast.kind === 'error'" [class.sp-toast-warning]="toast.kind === 'warning'" [class.sp-toast-info]="toast.kind === 'info'">
          <span>{{ toast.message }}</span>
          <button type="button" (click)="toastService.dismiss(toast.id)" aria-label="Dismiss notification">
            <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" viewBox="0 0 24 24">
              <path d="M18 6 6 18M6 6l12 12"/>
            </svg>
          </button>
        </div>
      }
    </section>
  `,
})
export class ToastHostComponent {
  constructor(public toastService: ToastService) {}
}
