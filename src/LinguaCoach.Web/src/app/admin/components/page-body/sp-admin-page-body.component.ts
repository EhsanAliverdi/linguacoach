import { Component } from '@angular/core';

/**
 * Vertical stack wrapper for admin page sections.
 * Provides consistent gap-6 spacing between sp-admin-card / sp-admin-section-card blocks.
 * Use directly inside each admin page component, after sp-admin-page-header.
 *
 * Usage:
 *   <sp-admin-page-header title="..." />
 *   <sp-admin-page-body>
 *     <sp-admin-card title="Section one">...</sp-admin-card>
 *     <sp-admin-card title="Section two">...</sp-admin-card>
 *   </sp-admin-page-body>
 */
@Component({
  selector: 'sp-admin-page-body',
  standalone: true,
  template: `<ng-content />`,
  styles: [`:host { display: flex; flex-direction: column; gap: 24px; }`],
})
export class SpAdminPageBodyComponent {}
