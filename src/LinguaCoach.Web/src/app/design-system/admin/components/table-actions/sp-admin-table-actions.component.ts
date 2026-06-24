import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
  ViewChild,
  AfterViewInit,
  ChangeDetectorRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SpAdminTableAction {
  label: string;
  danger?: boolean;
  disabled?: boolean;
}

/**
 * Generic row action dropdown for admin tables.
 * Based on TailAdmin table-dropdown pattern (shared/components/common/table-dropdown).
 *
 * The dropdown menu is rendered with position:fixed, coordinates computed from
 * the trigger button's getBoundingClientRect(). This escapes any overflow:hidden/auto
 * ancestor (e.g., a scrollable table container) so the menu never causes the table
 * to scroll when it opens near the bottom of the viewport.
 *
 * Usage:
 *   <sp-admin-table-actions [actions]="rowActions" (actionClick)="onAction($event, row)" />
 *
 * Or with full content projection:
 *   <sp-admin-table-actions>
 *     <button (click)="view(row)">View</button>
 *     <button (click)="edit(row)">Edit</button>
 *   </sp-admin-table-actions>
 */
@Component({
  selector: 'sp-admin-table-actions',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-adm-row-actions relative inline-block">
      <!-- Trigger: •••  ghost xs button matching JSX adm-btn-ghost adm-btn-xs -->
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
        <!--
          Menu rendered with position:fixed + top/left from getBoundingClientRect().
          This escapes overflow:hidden table containers and prevents scroll-on-open.
          Flips upward automatically when near the bottom of the viewport.
        -->
        <div
          #menuRef
          role="menu"
          class="sp-adm-actions-menu"
          style="position:fixed;display:flex;flex-direction:column;"
          [style.top.px]="menuTop"
          [style.left.px]="menuLeft"
          (click)="isOpen = false"
        >
          @if (actions.length > 0) {
            @for (action of actions; track action.label) {
              <button
                type="button"
                role="menuitem"
                class="sp-adm-action-item"
                [class.sp-adm-action-danger]="action.danger"
                [class.sp-adm-action-disabled]="action.disabled"
                [disabled]="action.disabled || false"
                (click)="onActionClick(action, $event)"
              >{{ action.label }}</button>
            }
          } @else {
            <!-- Projected content for fully custom actions -->
            <ng-content />
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: inline-block; }

    /* Trigger — JSX: adm-btn-ghost adm-btn-xs, padding 4px 8px, letterSpacing 2 */
    .sp-adm-actions-trigger {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 4px 8px;
      border-radius: 6px;
      border: 1.5px solid #E2DEF0;
      background: #fff;
      color: #211B36;
      font-size: 12px;
      font-weight: 700;
      font-family: inherit;
      letter-spacing: 2px;
      cursor: pointer;
      line-height: 1;
      transition: opacity .12s;
      white-space: nowrap;
    }
    .sp-adm-actions-trigger:hover { opacity: .88; }

    /* Fixed-positioned dropdown panel — matches JSX: minWidth 160, radius 10, shadow */
    .sp-adm-actions-menu {
      position: fixed;
      min-width: 160px;
      border-radius: 10px;
      border: 1px solid #ECE9F5;
      background: #fff;
      box-shadow: 0 8px 24px rgba(33,27,54,.12);
      padding: 4px 0;
      z-index: 500;
      overflow: hidden;
    }

    @media (prefers-color-scheme: dark) {
      .sp-adm-actions-menu {
        background: #111827;
        border-color: #1f2937;
      }
    }

    /* Menu items — ::ng-deep to cover both internal and projected items */
    :host ::ng-deep .sp-adm-action-item {
      display: flex;
      align-items: center;
      gap: 10px;
      width: 100%;
      text-align: left;
      padding: 9px 14px;
      font-size: 13.5px;
      font-weight: 600;
      font-family: inherit;
      line-height: 1.4;
      color: #4B4462;
      background: none;
      border: none;
      cursor: pointer;
      text-decoration: none;
      transition: background .08s;
      white-space: nowrap;
      box-sizing: border-box;
    }
    :host ::ng-deep .sp-adm-action-item:hover {
      background: #F6F4FB;
      color: #211B36;
    }
    :host ::ng-deep .sp-adm-action-item:focus-visible {
      outline: 2px solid #5B4BE8;
      outline-offset: -2px;
    }
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-danger {
      color: #C0392B;
    }
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-danger:hover {
      background: #F6F4FB;
      color: #C0392B;
    }
    :host ::ng-deep .sp-adm-action-item:disabled,
    :host ::ng-deep .sp-adm-action-item.sp-adm-action-disabled {
      opacity: 0.4;
      cursor: not-allowed;
      pointer-events: none;
    }
  `],
})
export class SpAdminTableActionsComponent {
  @Input() actions: SpAdminTableAction[] = [];
  @Output() actionClick = new EventEmitter<SpAdminTableAction>();

  @ViewChild('triggerRef') triggerRef!: ElementRef<HTMLButtonElement>;

  isOpen = false;
  menuTop = 0;
  menuLeft = 0;

  /** Approximate menu height used for upward-flip calculation. */
  private readonly MENU_HEIGHT_ESTIMATE = 200;
  /** Gap between trigger bottom and menu top. */
  private readonly MENU_GAP = 4;
  /** Menu width estimate for right-align calculation (min-width 160px). */
  private readonly MENU_WIDTH = 160;

  constructor(
    private elRef: ElementRef<HTMLElement>,
    private cdr: ChangeDetectorRef,
  ) {}

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    if (!this.isOpen) {
      this.computeMenuPosition();
    }
    this.isOpen = !this.isOpen;
  }

  /**
   * Compute fixed coordinates for the dropdown from the trigger's bounding rect.
   * Opens below the trigger by default; flips upward if near viewport bottom.
   */
  private computeMenuPosition(): void {
    if (!this.triggerRef) return;
    const rect = this.triggerRef.nativeElement.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const spaceBelow = viewportHeight - rect.bottom;

    // Flip upward if not enough space below
    if (spaceBelow < this.MENU_HEIGHT_ESTIMATE && rect.top > this.MENU_HEIGHT_ESTIMATE) {
      this.menuTop = rect.top - this.MENU_HEIGHT_ESTIMATE;
    } else {
      this.menuTop = rect.bottom + this.MENU_GAP;
    }

    // Right-align with trigger; clamp to viewport left edge
    this.menuLeft = Math.max(0, rect.right - this.MENU_WIDTH);
  }

  onActionClick(action: SpAdminTableAction, event: MouseEvent): void {
    if (action.disabled) { event.stopPropagation(); return; }
    this.actionClick.emit(action);
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMousedown(event: MouseEvent): void {
    if (this.isOpen && !this.elRef.nativeElement.contains(event.target as Node)) {
      this.isOpen = false;
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.isOpen) this.isOpen = false;
  }

  @HostListener('window:scroll', ['$event'])
  onWindowScroll(): void {
    // Close on any scroll to keep position accurate
    if (this.isOpen) this.isOpen = false;
  }
}
