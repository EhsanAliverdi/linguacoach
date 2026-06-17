import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type StudentBadgeVariant = 'success' | 'warn' | 'info' | 'muted' | 'writing' | 'speaking' | 'listening' | 'vocabulary';

@Component({
  selector: 'sp-badge',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    :host { display: inline-flex; }
    .sp-badge {
      display: inline-flex; align-items: center;
      font-size: 10px; font-weight: 700;
      padding: 2px 8px; border-radius: var(--sp-r-full);
      white-space: nowrap;
    }
    .sp-badge--success { background: var(--sp-success-soft); color: var(--sp-success); }
    .sp-badge--warn    { background: var(--sp-warn-soft);    color: var(--sp-warn); }
    .sp-badge--info    { background: var(--sp-writing-soft); color: var(--sp-writing-ink); }
    .sp-badge--muted   { background: var(--sp-canvas2);      color: var(--sp-muted); }
    .sp-badge--writing     { background: var(--sp-writing-soft);     color: var(--sp-writing-ink); }
    .sp-badge--speaking    { background: var(--sp-speaking-soft);    color: var(--sp-speaking-ink); }
    .sp-badge--listening   { background: var(--sp-listening-soft);   color: var(--sp-listening-ink); }
    .sp-badge--vocabulary  { background: var(--sp-vocabulary-soft);  color: var(--sp-vocabulary-ink); }
  `],
  template: `<span class="sp-badge" [class]="'sp-badge sp-badge--' + variant"><ng-content /></span>`,
})
export class StudentBadgeComponent {
  @Input() variant: StudentBadgeVariant = 'muted';
}
