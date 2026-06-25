import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
  ViewChild,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';

// ── Built-in action icon paths (Feather/Heroicons subset, 24×24 viewBox) ──
const ACTION_ICON_PATHS: Record<string, string> = {
  view:       '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
  eye:        '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
  edit:       '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>',
  pencil:     '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>',
  activate:   '<polyline points="20 6 9 17 4 12"/>',
  check:      '<polyline points="20 6 9 17 4 12"/>',
  deactivate: '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  x:          '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
  delete:     '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/>',
  archive:    '<polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/>',
  reset:      '<polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
  settings:   '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
};

export type SpAdminActionTone = 'default' | 'danger' | 'warning';
export type SpAdminTableActionsMode = 'dropdown' | 'buttons';

/** Rich action descriptor — use with [actions] input. */
export interface SpAdminRowAction {
  id: string;
  label: string;
  icon?: string;
  tone?: SpAdminActionTone;
  disabled?: boolean;
  hidden?: boolean;
  dividerBefore?: boolean;
}

/** Legacy simple action — kept for backward compatibility. */
export interface SpAdminTableAction {
  label: string;
  danger?: boolean;
  disabled?: boolean;
}

@Component({
  selector: 'sp-admin-table-actions',
  standalone: true,
  imports: [CommonModule, SpAdminButtonComponent],
  template: `
    @if (mode === 'buttons') {
      <!-- Button group mode: render actions as visible buttons -->
      <div class="sp-adm-action-btn-group">
        @for (a of visibleRichActions; track a.id) {
          <sp-admin-button
            [variant]="a.tone === 'danger' ? 'danger' : 'neutral'"
            [appearance]="a.tone === 'danger' ? 'ghost' : 'outline'"
            size="sm"
            [disabled]="a.disabled ?? false"
            (click)="emitRich(a)">
            @if (a.icon && iconSvg(a.icon)) {
              <span [innerHTML]="iconSvg(a.icon)" class="sp-adm-btn-icon" slot="leading"></span>
            }
            {{ a.label }}
          </sp-admin-button>
        }
      </div>
    } @else {
      <!-- Dropdown mode (default) -->
      <div class="sp-adm-row-actions">
        <button
          #triggerRef
          type="button"
          class="sp-adm-actions-trigger"
          [attr.aria-expanded]="isOpen"
          aria-haspopup="menu"
          aria-label="Row actions"
          (click)="toggle($event)"
        >•••</button>

        @if (isOpen) {
          <div
            #menuRef
            role="menu"
            class="sp-adm-actions-menu"
            style="position:fixed;display:flex;flex-direction:column;"
            [style.top.px]="menuTop"
            [style.left.px]="menuLeft"
            (click)="isOpen = false"
          >
            @if (richActions.length > 0) {
              <!-- Rich action mode -->
              @for (a of visibleRichActions; track a.id) {
                @if (a.dividerBefore) {
                  <hr class="sp-adm-action-divider" />
                }
                <button
                  type="button"
                  role="menuitem"
                  class="sp-adm-action-item"
                  [class.sp-adm-action-danger]="a.tone === 'danger'"
                  [class.sp-adm-action-disabled]="a.disabled"
                  [disabled]="a.disabled || false"
                  (click)="emitRich(a, $event)">
                  @if (a.icon && iconSvg(a.icon)) {
                    <span [innerHTML]="iconSvg(a.icon)" class="sp-adm-item-icon" aria-hidden="true"></span>
                  }
                  {{ a.label }}
                </button>
              }
            } @else if (legacyActions.length > 0) {
              <!-- Legacy [actions] mode -->
              @for (action of legacyActions; track action.label) {
                <button
                  type="button"
                  role="menuitem"
                  class="sp-adm-action-item"
                  [class.sp-adm-action-danger]="action.danger"
                  [class.sp-adm-action-disabled]="action.disabled"
                  [disabled]="action.disabled || false"
                  (click)="onLegacyClick(action, $event)"
                >{{ action.label }}</button>
              }
            } @else {
              <!-- Projected content for fully custom actions -->
              <ng-content />
            }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: inline-block; }

    /* Trigger */
    .sp-adm-actions-trigger {
      display: inline-flex; align-items: center; justify-content: center;
      padding: 4px 8px; border-radius: 6px;
      border: 1.5px solid #E2DEF0; background: #fff; color: #211B36;
      font-size: 12px; font-weight: 700; font-family: inherit;
      letter-spacing: 2px; cursor: pointer; line-height: 1; transition: opacity .12s;
    }
    .sp-adm-actions-trigger:hover { opacity: .88; }

    /* Fixed dropdown panel */
    .sp-adm-actions-menu {
      position: fixed; min-width: 160px; border-radius: 10px;
      border: 1px solid #ECE9F5; background: #fff;
      box-shadow: 0 8px 24px rgba(33,27,54,.12);
      padding: 4px 0; z-index: 500; overflow: hidden;
    }

    /* Divider */
    .sp-adm-action-divider { border: none; border-top: 1px solid #ECE9F5; margin: 4px 0; }

    /* Icon wrappers — normalise SVG to 14×14 */
    .sp-adm-item-icon, .sp-adm-btn-icon {
      display: inline-flex; align-items: center; justify-content: center;
      flex-shrink: 0; line-height: 0;
    }
    .sp-adm-item-icon svg, .sp-adm-btn-icon svg { width: 14px; height: 14px; }

    /* Menu items — ::ng-deep covers both internal and projected */
    :host ::ng-deep .sp-adm-action-item {
      display: flex; align-items: center; gap: 10px; width: 100%;
      text-align: left; padding: 9px 14px;
      font-size: 13.5px; font-weight: 600; font-family: inherit;
      line-height: 1.4; color: #4B4462;
      background: none; border: none; cursor: pointer;
      transition: background .08s; white-space: nowrap; box-sizing: border-box;
    }
    :host ::ng-deep .sp-adm-action-item:hover { background: #F6F4FB; color: #211B36; }
    :host ::ng-deep .sp-adm-action-item:focus-visible { outline: 2px solid #5B4BE8; outline-offset: -2px; }
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-danger { color: #C0392B; }
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-danger:hover { background: #F6F4FB; color: #C0392B; }
    :host ::ng-deep .sp-adm-action-item:disabled,
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-disabled { opacity: 0.4; cursor: not-allowed; pointer-events: none; }

    .sp-adm-row-actions { display: flex; justify-content: flex-end; }
    /* Button group mode */
    .sp-adm-action-btn-group { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
  `],
})
export class SpAdminTableActionsComponent {
  /** Rich action descriptors — preferred API. */
  @Input() richActions: SpAdminRowAction[] = [];
  /** Legacy simple actions — kept for backward compat. */
  @Input() set actions(v: SpAdminTableAction[]) { this.legacyActions = v; }
  legacyActions: SpAdminTableAction[] = [];

  @Input() mode: SpAdminTableActionsMode = 'dropdown';

  /** Emitted when a rich action is selected. Payload is the action id. */
  @Output() actionSelected = new EventEmitter<string>();
  /** Legacy event — emits the full action object. */
  @Output() actionClick = new EventEmitter<SpAdminTableAction>();

  @ViewChild('triggerRef') triggerRef!: ElementRef<HTMLButtonElement>;

  isOpen = false;
  menuTop = 0;
  menuLeft = 0;

  private readonly MENU_HEIGHT_ESTIMATE = 200;
  private readonly MENU_GAP = 4;
  private readonly MENU_WIDTH = 160;

  constructor(
    private elRef: ElementRef<HTMLElement>,
    private cdr: ChangeDetectorRef,
    private sanitizer: DomSanitizer,
  ) {}

  get visibleRichActions(): SpAdminRowAction[] {
    return this.richActions.filter(a => !a.hidden);
  }

  iconSvg(key: string): SafeHtml | null {
    const paths = ACTION_ICON_PATHS[key];
    if (!paths) return null;
    const svg = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths}</svg>`;
    return this.sanitizer.bypassSecurityTrustHtml(svg);
  }

  emitRich(action: SpAdminRowAction, event?: MouseEvent): void {
    if (action.disabled) { event?.stopPropagation(); return; }
    this.actionSelected.emit(action.id);
  }

  onLegacyClick(action: SpAdminTableAction, event: MouseEvent): void {
    if (action.disabled) { event.stopPropagation(); return; }
    this.actionClick.emit(action);
  }

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    if (!this.isOpen) this.computeMenuPosition();
    this.isOpen = !this.isOpen;
  }

  private computeMenuPosition(): void {
    if (!this.triggerRef) return;
    const rect = this.triggerRef.nativeElement.getBoundingClientRect();
    const spaceBelow = window.innerHeight - rect.bottom;
    this.menuTop = spaceBelow < this.MENU_HEIGHT_ESTIMATE && rect.top > this.MENU_HEIGHT_ESTIMATE
      ? rect.top - this.MENU_HEIGHT_ESTIMATE
      : rect.bottom + this.MENU_GAP;
    this.menuLeft = Math.max(0, rect.right - this.MENU_WIDTH);
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMousedown(event: MouseEvent): void {
    if (this.isOpen && !this.elRef.nativeElement.contains(event.target as Node)) this.isOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { if (this.isOpen) this.isOpen = false; }

  @HostListener('window:scroll')
  onWindowScroll(): void { if (this.isOpen) this.isOpen = false; }
}
