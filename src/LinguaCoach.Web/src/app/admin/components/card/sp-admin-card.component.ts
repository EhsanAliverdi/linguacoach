import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="sp-adm-card" [class.sp-adm-card-tight]="padding === 'sm'" [class.sp-adm-card-dashed]="dashed">
      @if (title) {
        <header class="sp-adm-card-header">
          <h2>{{ title }}</h2>
          <ng-content select="[slot=actions]" />
        </header>
      }
      <ng-content />
    </section>
  `,
  styles: [`
    .sp-adm-card {
      background: var(--sp-admin-surface);
      border: 1px solid var(--sp-admin-border);
      border-radius: var(--sp-admin-radius-lg);
      padding: 18px;
      box-shadow: var(--sp-admin-shadow-card);
      min-width: 0;
    }
    .sp-adm-card-tight { padding: 14px; }
    .sp-adm-card-dashed { border-style: dashed; }
    .sp-adm-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 14px;
    }
    .sp-adm-card-header h2 {
      margin: 0;
      font-size: 14px;
      font-weight: 800;
      color: var(--sp-admin-text);
    }
  `],
})
export class SpAdminCardComponent {
  @Input() title = '';
  @Input() padding: 'sm' | 'md' = 'md';
  @Input() dashed = false;
}
