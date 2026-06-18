import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  HostListener,
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
        <!-- TailAdmin dropdown panel: absolute z-40 right-0 mt-2 rounded-xl border border-gray-200 bg-white shadow-lg -->
        <div
          #menuRef
          role="menu"
          class="absolute z-40 right-0 mt-1 w-44 rounded-xl border border-gray-200 bg-white shadow-lg dark:border-gray-800 dark:bg-gray-900 py-1"
          (click)="isOpen = false"
        >
          @if (actions.length > 0) {
            @for (action of actions; track action.label) {
              <button
                type="button"
                role="menuitem"
                class="sp-adm-action-item w-full text-left px-4 py-2 text-sm transition-colors"
                [class.text-red-600]="action.danger"
                [class.hover:bg-red-50]="action.danger"
                [class.dark:hover:bg-red-900]="action.danger"
                [class.text-gray-700]="!action.danger"
                [class.dark:text-gray-300]="!action.danger"
                [class.hover:bg-gray-50]="!action.danger"
                [class.dark:hover:bg-gray-800]="!action.danger"
                [class.opacity-40]="action.disabled"
                [class.cursor-not-allowed]="action.disabled"
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
    /* TailAdmin-backed: absolute z-40 right-0 rounded-xl border border-gray-200 bg-white shadow-lg */
    :host { display: inline-block; }
  `],
})
export class SpAdminTableActionsComponent {
  @Input() actions: SpAdminTableAction[] = [];
  @Output() actionClick = new EventEmitter<SpAdminTableAction>();

  isOpen = false;

  constructor(private elRef: ElementRef<HTMLElement>) {}

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.isOpen = !this.isOpen;
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
}
