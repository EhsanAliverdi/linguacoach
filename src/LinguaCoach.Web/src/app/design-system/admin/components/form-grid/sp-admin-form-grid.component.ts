import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminFormGridColumns = 1 | 2 | 3 | 4;

@Component({
  selector: 'sp-admin-form-grid',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="sp-fg-root" [class]="colClass"><ng-content /></div>`,
  styles: [`
    :host { display: block; }
    .sp-fg-root { display: grid; gap: 16px; }
    .sp-fg-1 { grid-template-columns: 1fr; }
    .sp-fg-2 { grid-template-columns: repeat(2, 1fr); }
    .sp-fg-3 { grid-template-columns: repeat(3, 1fr); }
    .sp-fg-4 { grid-template-columns: repeat(4, 1fr); }
    @media(max-width:767px) {
      .sp-fg-2, .sp-fg-3, .sp-fg-4 { grid-template-columns: 1fr; }
    }
    @media(min-width:768px) and (max-width:1023px) {
      .sp-fg-3, .sp-fg-4 { grid-template-columns: repeat(2, 1fr); }
    }
  `],
})
export class SpAdminFormGridComponent {
  @Input() columns: SpAdminFormGridColumns = 2;
  get colClass(): string { return `sp-fg-${this.columns}`; }
}
