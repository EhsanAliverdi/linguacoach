import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Identity cell layout: avatar/icon slot on left, stacked text on right.
 *
 * Slots:
 *   [slot=avatar]    — avatar or icon (sp-admin-avatar, sp-admin-icon, img)
 *   (default)        — primary label + secondary text/copyable
 *
 * Usage:
 *   <sp-admin-identity-cell>
 *     <sp-admin-avatar slot="avatar" initials="A" seed="a@b.com" />
 *     <span class="sp-admin-identity-name">Alice</span>
 *     <sp-admin-copyable-text value="a@b.com" class="sp-admin-table-muted" />
 *   </sp-admin-identity-cell>
 */
@Component({
  selector: 'sp-admin-identity-cell',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-ic-root">
      <ng-content select="[slot=avatar]" />
      <div class="sp-adm-ic-body">
        <ng-content />
      </div>
    </div>
  `,
  styles: [`
    .sp-adm-ic-root {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
    }
    .sp-adm-ic-body {
      display: flex;
      flex-direction: column;
      gap: 1px;
      min-width: 0;
    }
  `],
})
export class SpAdminIdentityCellComponent {}
