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
      <!-- Trigger: three-dot button matching TailAdmin table-dropdown button style -->
      <button
        #triggerRef
        type="button"
        class="sp-adm-actions-trigger flex items-center justify-center w-8 h-8 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 dark:hover:bg-gray-800 dark:hover:text-gray-300 transition-colors"
        [attr.aria-expanded]="isOpen"
        aria-haspopup="menu"
        aria-label="Row actions"
        (click)="toggle($event)"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
          <circle cx="5" cy="12" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="19" cy="12" r="2"/>
        </svg>
      </button>

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

    /* Fixed-positioned dropdown panel — escapes any overflow:hidden/auto ancestor */
    .sp-adm-actions-menu {
      position: fixed;
      width: 176px;      /* w-44 = 11rem = 176px */
      border-radius: 12px;
      border: 1px solid #e5e7eb;
      background: #fff;
      box-shadow: 0 4px 24px rgba(0,0,0,0.10);
      padding: 4px 0;
      z-index: 500;
    }

    @media (prefers-color-scheme: dark) {
      .sp-adm-actions-menu {
        background: #111827;
        border-color: #1f2937;
      }
    }

    /* Menu items — common base */
    .sp-adm-action-item {
      display: block;
      width: 100%;
      text-align: left;
      padding: 8px 16px;
      font-size: 13px;
      font-weight: 500;
      line-height: 1.4;
      color: #374151;
      background: transparent;
      border: none;
      cursor: pointer;
      text-decoration: none;
      transition: background 0.1s, color 0.1s;
      white-space: nowrap;
    }
    .sp-adm-action-item:hover {
      background: #f9fafb;
      color: #111827;
    }
    .sp-adm-action-item:focus-visible {
      outline: 2px solid #6366f1;
      outline-offset: -2px;
    }
    .sp-adm-action-item.sp-adm-action-danger {
      color: #dc2626;
    }
    .sp-adm-action-item.sp-adm-action-danger:hover {
      background: #fef2f2;
      color: #b91c1c;
    }
    .sp-adm-action-item.sp-adm-action-disabled,
    .sp-adm-action-item:disabled {
      opacity: 0.4;
      cursor: not-allowed;
      pointer-events: none;
    }

    /* Dark mode item overrides */
    @media (prefers-color-scheme: dark) {
      .sp-adm-action-item { color: #d1d5db; }
      .sp-adm-action-item:hover { background: #1f2937; color: #f9fafb; }
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
  /** Menu width matches .sp-adm-actions-menu width (176px). */
  private readonly MENU_WIDTH = 176;

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
