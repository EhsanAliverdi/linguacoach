import { Component } from '@angular/core';

@Component({
  selector: 'sp-admin-spinner',
  standalone: true,
  template: `<div class="sp-spinner" role="status" aria-label="Loading"></div>`,
  styles: [`
    .sp-spinner {
      width: 20px;
      height: 20px;
      border: 2.5px solid var(--sp-admin-border);
      border-top-color: var(--sp-admin-primary);
      border-radius: 50%;
      animation: sp-spin .7s linear infinite;
      margin: 0 auto;
    }
    @keyframes sp-spin { to { transform: rotate(360deg); } }
  `],
})
export class SpAdminSpinnerComponent {}
