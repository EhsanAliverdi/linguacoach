import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminStatusGridColumns = 2 | 3 | 4 | 'auto';

@Component({
  selector: 'sp-admin-status-grid',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="sp-sg-root" [class]="colClass"><ng-content /></div>`,
  styles: [`
    :host { display: block; }
    .sp-sg-root { display: grid; gap: 12px; }
    .sp-sg-2    { grid-template-columns: repeat(2, 1fr); }
    .sp-sg-3    { grid-template-columns: repeat(3, 1fr); }
    .sp-sg-4    { grid-template-columns: repeat(4, 1fr); }
    .sp-sg-auto { grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); }
    @media(max-width:639px) {
      .sp-sg-2, .sp-sg-3, .sp-sg-4 { grid-template-columns: repeat(2, 1fr); }
    }
  `],
})
export class SpAdminStatusGridComponent {
  @Input() columns: SpAdminStatusGridColumns = 'auto';
  get colClass(): string { return `sp-sg-${this.columns}`; }
}
