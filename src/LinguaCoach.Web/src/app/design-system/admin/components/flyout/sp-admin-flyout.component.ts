import {
  Component, Input, Output, EventEmitter, HostListener,
  ElementRef, ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminFlyoutAlign = 'left' | 'right';

/**
 * Lightweight inline-positioned flyout panel.
 * The trigger element should sit inside a `position:relative` container.
 * Toggle `open` from the parent; the flyout emits `(closed)` when the user
 * clicks outside or presses Escape so the parent can clear its state.
 *
 * Usage:
 *   <div class="sp-admin-flyout-anchor">
 *     <button (click)="open = !open">Open</button>
 *     <sp-admin-flyout [open]="open" (closed)="open = false">
 *       ... content ...
 *     </sp-admin-flyout>
 *   </div>
 */
@Component({
  selector: 'sp-admin-flyout',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open) {
      <div
        class="sp-adm-flyout"
        [class.sp-adm-flyout--left]="align === 'left'"
        [class.sp-adm-flyout--right]="align === 'right'"
        role="dialog"
        [attr.aria-label]="ariaLabel || 'Options'"
        (click)="$event.stopPropagation()">
        <ng-content />
      </div>
    }
  `,
  styles: [`
    :host { position: relative; display: inline-block; }

    .sp-adm-flyout {
      position: absolute;
      top: calc(100% + 8px);
      z-index: 300;
      min-width: 220px;
      background: var(--sp-admin-surface, #fff);
      border: 1.5px solid var(--sp-admin-border, #E2DEF0);
      border-radius: 12px;
      box-shadow: 0 8px 24px rgba(33, 27, 54, 0.13);
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      animation: sp-adm-flyout-in .13s ease;
    }
    @keyframes sp-adm-flyout-in {
      from { opacity: 0; transform: translateY(-4px); }
      to   { opacity: 1; transform: translateY(0); }
    }

    .sp-adm-flyout--right { right: 0; }
    .sp-adm-flyout--left  { left: 0; }
  `],
})
export class SpAdminFlyoutComponent {
  @Input() open = false;
  @Input() align: SpAdminFlyoutAlign = 'right';
  @Input() ariaLabel = '';

  @Output() closed = new EventEmitter<void>();

  constructor(private el: ElementRef) {}

  @HostListener('document:click', ['$event'])
  onDocClick(e: MouseEvent): void {
    if (this.open && !this.el.nativeElement.contains(e.target)) {
      this.closed.emit();
    }
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    if (this.open) this.closed.emit();
  }
}
