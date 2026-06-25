import { Component, HostBinding, Input } from '@angular/core';

const GAP: Record<string, string> = { sm: '8px', md: '16px', lg: '24px' };

/** Vertical flex stack with configurable gap. Replaces ad-hoc flex-column wrappers. */
@Component({
  selector: 'sp-admin-stack',
  standalone: true,
  template: `<ng-content />`,
  styles: [`:host { display:flex; flex-direction:column; }`],
})
export class SpAdminStackComponent {
  @Input() gap: 'sm' | 'md' | 'lg' = 'md';

  @HostBinding('style.gap')
  get gapStyle(): string { return GAP[this.gap] ?? '16px'; }
}
