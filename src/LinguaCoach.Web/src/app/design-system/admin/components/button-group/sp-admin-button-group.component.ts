import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';
import { SpAdminButtonVariant, SpAdminButtonAppearance, SpAdminButtonSize } from '../button/sp-admin-button.component';

const BTN_ICON_PATHS: Record<string, string> = {
  edit:    '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>',
  save:    '<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/>',
  create:  '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/>',
  close:   '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  cancel:  '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  delete:  '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/>',
  deactivate: '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  activate:   '<polyline points="20 6 9 17 4 12"/>',
  view:    '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
  refresh: '<polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
};

export type SpAdminButtonGroupAlign = 'left' | 'right' | 'center' | 'between';

export interface SpAdminButtonGroupAction {
  id: string;
  label: string;
  variant?: SpAdminButtonVariant;
  appearance?: SpAdminButtonAppearance;
  icon?: string;
  iconColor?: string;
  disabled?: boolean;
  loading?: boolean;
  hidden?: boolean;
  ariaLabel?: string;
}

@Component({
  selector: 'sp-admin-button-group',
  standalone: true,
  imports: [CommonModule, SpAdminButtonComponent],
  template: `
    <div class="sp-adm-btn-group" [class]="alignClass">
      @for (action of visibleActions; track action.id) {
        <sp-admin-button
          [variant]="action.variant ?? 'primary'"
          [appearance]="action.appearance ?? 'solid'"
          [size]="size"
          [disabled]="action.disabled ?? false"
          [loading]="action.loading ?? false"
          [fullWidth]="fullWidth"
          [attr.aria-label]="action.ariaLabel || null"
          (click)="emit(action)">
          @if (action.icon && iconSvg(action.icon, action.iconColor)) {
            <span [innerHTML]="iconSvg(action.icon, action.iconColor)" class="sp-adm-btngrp-icon" slot="leading"></span>
          }
          {{ action.label }}
        </sp-admin-button>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-adm-btn-group { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .sp-adm-btn-group-left    { justify-content: flex-start; }
    .sp-adm-btn-group-right   { justify-content: flex-end; }
    .sp-adm-btn-group-center  { justify-content: center; }
    .sp-adm-btn-group-between { justify-content: space-between; }
    .sp-adm-btngrp-icon {
      display: inline-flex; align-items: center; justify-content: center;
      flex-shrink: 0; line-height: 0;
    }
    .sp-adm-btngrp-icon svg { width: 13px; height: 13px; }
  `],
})
export class SpAdminButtonGroupComponent {
  @Input() actions: SpAdminButtonGroupAction[] = [];
  @Input() align: SpAdminButtonGroupAlign = 'left';
  @Input() size: SpAdminButtonSize = 'md';
  @Input() fullWidth = false;
  @Output() actionSelected = new EventEmitter<string>();

  constructor(private sanitizer: DomSanitizer) {}

  get visibleActions(): SpAdminButtonGroupAction[] {
    return this.actions.filter(a => !a.hidden);
  }

  get alignClass(): string {
    return `sp-adm-btn-group-${this.align}`;
  }

  emit(action: SpAdminButtonGroupAction): void {
    if (!action.disabled && !action.loading) this.actionSelected.emit(action.id);
  }

  iconSvg(key: string, color?: string): SafeHtml | null {
    const paths = BTN_ICON_PATHS[key];
    if (!paths) return null;
    const stroke = color ?? 'currentColor';
    const svg = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="${stroke}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(svg);
  }
}
