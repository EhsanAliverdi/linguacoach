import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminSpinnerComponent } from '../spinner/sp-admin-spinner.component';

@Component({
  selector: 'sp-admin-loading-state',
  standalone: true,
  imports: [CommonModule, SpAdminSpinnerComponent],
  template: `
    <div class="sp-adm-loading" role="status">
      <sp-admin-spinner />
      <span>{{ message }}</span>
    </div>
  `,
  styles: [`
    .sp-adm-loading {
      display: grid;
      place-items: center;
      gap: 12px;
      padding: 40px 24px;
      color: var(--sp-admin-text-dim, #9ca3af);
      font-size: 13px;
      font-weight: 500;
      text-align: center;
    }
  `],
})
export class SpAdminLoadingStateComponent {
  @Input() message = 'Loading';
}
