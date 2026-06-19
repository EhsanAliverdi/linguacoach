import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';
import { SpAdminErrorStateComponent } from '../error-state/sp-admin-error-state.component';

export type SpAdminSlideOverSize = 'sm' | 'md' | 'lg' | 'xl';

const SIZE_MAP: Record<SpAdminSlideOverSize, string> = {
  sm:  '360px',
  md:  '480px',
  lg:  '600px',
  xl:  '768px',
};

/**
 * Slide-over panel that appears within the admin content area (not full-screen fixed).
 * Use for detail/edit/view secondary flows: student preferences, audit history,
 * rule editing, prompt preview, entity detail panels.
 *
 * Slots:
 *   [slot=header-actions]  — buttons rendered in the panel header, beside the title
 *   (default ng-content)  — body content
 *   [slot=footer]          — footer actions (save/cancel row)
 *
 * Inputs:
 *   open            — controls visibility
 *   title           — panel heading
 *   subtitle        — optional secondary label under heading
 *   size            — 'sm' | 'md' | 'lg' | 'xl' (default: 'md')
 *   loading         — shows loading state instead of body
 *   loadingMessage  — message shown in loading state
 *   error           — error message string; shows error state block in body when set
 *   errorTitle      — title for the error block (default: 'Something went wrong')
 *   closeOnBackdrop — whether clicking the backdrop closes the panel (default: true)
 *
 * Outputs:
 *   closed — emits void when the panel should close
 */
@Component({
  selector: 'sp-admin-slide-over',
  standalone: true,
  imports: [CommonModule, SpAdminLoadingStateComponent, SpAdminErrorStateComponent],
  template: `
    @if (open) {
      <div
        class="sp-adm-so-backdrop"
        (click)="closeOnBackdrop && close()"
        aria-hidden="true"
      ></div>
      <aside
        class="sp-adm-so-panel"
        [style.width]="panelWidth"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title"
      >
        <header class="sp-adm-so-header">
          <div class="sp-adm-so-titles">
            <h2 class="sp-adm-so-title">{{ title }}</h2>
            @if (subtitle) {
              <p class="sp-adm-so-subtitle">{{ subtitle }}</p>
            }
          </div>
          <div class="sp-adm-so-header-actions">
            <ng-content select="[slot=header-actions]" />
            <button
              type="button"
              class="sp-adm-so-close"
              (click)="close()"
              aria-label="Close panel"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path fill-rule="evenodd" clip-rule="evenodd"
                  d="M6.04289 16.5413C5.65237 16.9318 5.65237 17.565 6.04289 17.9555C6.43342 18.346 7.06658 18.346 7.45711 17.9555L11.9987 13.4139L16.5408 17.956C16.9313 18.3466 17.5645 18.3466 17.955 17.956C18.3455 17.5655 18.3455 16.9323 17.955 16.5418L13.4129 11.9997L17.955 7.4576C18.3455 7.06707 18.3455 6.43391 17.955 6.04338C17.5645 5.65286 16.9313 5.65286 16.5408 6.04338L11.9987 10.5855L7.45711 6.0439C7.06658 5.65338 6.43342 5.65338 6.04289 6.0439C5.65237 6.43442 5.65237 7.06759 6.04289 7.45811L10.5845 11.9997L6.04289 16.5413Z"
                  fill="currentColor"
                />
              </svg>
            </button>
          </div>
        </header>

        <div class="sp-adm-so-body">
          @if (loading) {
            <sp-admin-loading-state [message]="loadingMessage" />
          } @else if (error) {
            <sp-admin-error-state [title]="errorTitle" [message]="error" />
          } @else {
            <ng-content />
          }
        </div>

        <div class="sp-adm-so-footer">
          <ng-content select="[slot=footer]" />
        </div>
      </aside>
    }
  `,
  styles: [`
    /* Backdrop: light tint, covers page body only (positioned relative to stacking context) */
    .sp-adm-so-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(107, 114, 128, 0.3);
      backdrop-filter: blur(2px);
      z-index: 400;
    }

    /* Panel: slides from right edge of viewport, sits above backdrop */
    .sp-adm-so-panel {
      position: fixed;
      top: 0;
      right: 0;
      height: 100vh;
      max-width: 100vw;
      background: #fff;
      border-left: 1px solid #e5e7eb;
      box-shadow: -4px 0 32px rgba(0, 0, 0, 0.10);
      z-index: 401;
      display: flex;
      flex-direction: column;
      overflow: hidden;
      animation: sp-adm-so-slide-in 0.22s cubic-bezier(0.4, 0, 0.2, 1);
    }

    @media (prefers-color-scheme: dark) {
      .sp-adm-so-panel {
        background: #111827;
        border-left-color: #1f2937;
      }
    }

    @keyframes sp-adm-so-slide-in {
      from { transform: translateX(100%); opacity: 0.6; }
      to   { transform: translateX(0);    opacity: 1;   }
    }

    /* Header */
    .sp-adm-so-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
      padding: 20px 20px 16px;
      border-bottom: 1px solid #f3f4f6;
      flex-shrink: 0;
    }

    .sp-adm-so-titles {
      flex: 1;
      min-width: 0;
    }

    .sp-adm-so-title {
      margin: 0;
      font-size: 15px;
      font-weight: 600;
      color: #111827;
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sp-adm-so-subtitle {
      margin: 3px 0 0;
      font-size: 12px;
      color: #6b7280;
      line-height: 1.4;
    }

    .sp-adm-so-header-actions {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }

    /* Close button */
    .sp-adm-so-close {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border: none;
      border-radius: 50%;
      background: #f3f4f6;
      color: #6b7280;
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
      flex-shrink: 0;
      padding: 0;
    }

    .sp-adm-so-close:hover {
      background: #e5e7eb;
      color: #374151;
    }

    .sp-adm-so-close:focus-visible {
      outline: 3px solid #465fff;
      outline-offset: 2px;
    }

    /* Body */
    .sp-adm-so-body {
      flex: 1;
      overflow-y: auto;
      padding: 20px;
    }

    /* Footer */
    .sp-adm-so-footer {
      flex-shrink: 0;
      border-top: 1px solid #f3f4f6;
      padding: 14px 20px;
      display: flex;
      gap: 10px;
      justify-content: flex-end;
    }

    .sp-adm-so-footer:empty {
      display: none;
    }

    /* Mobile: near full-width */
    @media (max-width: 480px) {
      .sp-adm-so-panel {
        width: calc(100vw - 40px) !important;
      }
    }
  `],
})
export class SpAdminSlideOverComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() subtitle = '';
  @Input() size: SpAdminSlideOverSize = 'md';
  @Input() loading = false;
  @Input() loadingMessage = 'Loading';
  @Input() error = '';
  @Input() errorTitle = 'Something went wrong';
  @Input() closeOnBackdrop = true;
  @Output() closed = new EventEmitter<void>();

  get panelWidth(): string {
    return SIZE_MAP[this.size] ?? '480px';
  }

  close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) this.close();
  }
}
