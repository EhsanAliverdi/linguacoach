import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminLoadingStateComponent } from '../loading-state/sp-admin-loading-state.component';
import { SpAdminErrorStateComponent } from '../error-state/sp-admin-error-state.component';

/**
 * Width standard: ALL admin slide-overs render at 600px by default.
 * Do NOT set a custom size on normal create/edit/configure drawers.
 * The size input is for rare exceptions only.
 *
 * Size vocabulary:
 *   default / md / lg / wide  -- 600px  standard for all admin forms
 *   compact / sm              -- 420px  only for tiny confirm panels (avoid)
 *   extra-wide / xl           -- 768px  only for dashboard/large-data panels (avoid)
 *
 * Rules:
 *   - Do NOT pass size on normal admin drawers -- omit the attribute entirely.
 *   - Do NOT style the footer in page components -- footer CSS lives here.
 *   - Do NOT use sp-admin-modal for admin create/edit/configure flows.
 *   - Place <sp-admin-button-group slot="footer" .../> directly -- no wrapper div.
 *   - Footer button sizing is controlled here, not by page-level size/appearance inputs.
 */
export type SpAdminSlideOverSize = 'sm' | 'md' | 'lg' | 'xl' | 'compact' | 'default' | 'wide' | 'extra-wide';

const SIZE_MAP: Record<SpAdminSlideOverSize, string> = {
  default:      '600px',
  md:           '600px',
  lg:           '600px',
  wide:         '600px',
  compact:      '420px',
  sm:           '420px',
  'extra-wide': '768px',
  xl:          '768px',
};

/** Base z-index for the first (non-stacked) slide-over backdrop. */
const Z_BASE = 1000;
/** Each additional stacked panel gets this many extra z-index units. */
const Z_STACK_STEP = 50;

/**
 * Slide-over panel rendered at fixed position, above the entire admin shell
 * (header, sidebar, any drawers). z-index base is 1000; each stacked panel
 * receives a higher index via stackIndex input.
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
 *   closeOnBackdrop — whether clicking the backdrop closes the panel (default: false)
 *   stackIndex      — stacking depth (0=first, 1=second …); each level adds 50 to z-index
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
        [style.z-index]="backdropZ"
        (click)="closeOnBackdrop && close()"
        aria-hidden="true"
      ></div>
      <aside
        class="sp-adm-so-panel"
        [style.width]="panelWidth"
        [style.z-index]="panelZ"
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
    /* Backdrop: covers entire viewport including admin header and sidebar.
       z-index is set dynamically via [style.z-index] (base 1000 + stackIndex*50).
       The static fallback here ensures correct rendering in SSR / test contexts. */
    .sp-adm-so-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(107, 114, 128, 0.3);
      backdrop-filter: blur(2px);
      z-index: 1000;
    }

    /* Panel: slides from right edge of viewport, sits above backdrop.
       z-index is set dynamically via [style.z-index] (base 1001 + stackIndex*50). */
    .sp-adm-so-panel {
      position: fixed;
      top: 0;
      right: 0;
      height: 100vh;
      max-width: 100vw;
      background: #fff;
      border-left: 1px solid var(--sp-admin-border,#ECE9F5);
      box-shadow: -4px 0 32px rgba(0, 0, 0, 0.08);
      z-index: 1001;
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
      padding: 20px 24px 16px;
      border-bottom: 1px solid var(--sp-admin-border-subtle,#F4F2FC);
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
      color: var(--sp-admin-text,#0F172A);
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sp-adm-so-subtitle {
      margin: 3px 0 0;
      font-size: 12px;
      color: var(--sp-admin-text-muted,#64748B);
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
      background: var(--sp-admin-surface-subtle,#FBFAFE);
      color: var(--sp-admin-text-muted,#64748B);
      cursor: pointer;
      transition: background 0.15s, color 0.15s;
      flex-shrink: 0;
      padding: 0;
    }

    .sp-adm-so-close:hover {
      background: var(--sp-admin-border,#ECE9F5);
      color: var(--sp-admin-text,#0F172A);
    }

    .sp-adm-so-close:focus-visible {
      outline: 3px solid var(--sp-admin-primary,#5B4BE8);
      outline-offset: 2px;
    }

    /* Body */
    .sp-adm-so-body {
      flex: 1;
      overflow-y: auto;
      padding: 24px;
    }

    /* Footer: right-aligned flex row. Pages must NOT override this.
       Place <sp-admin-button-group slot="footer" .../> directly — no wrapper div. */
    .sp-adm-so-footer {
      flex-shrink: 0;
      border-top: 1px solid var(--sp-admin-border-subtle,#F4F2FC);
      padding: 14px 24px;
      display: flex;
      gap: 10px;
      justify-content: flex-end;
      align-items: center;
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
  /** Whether clicking the backdrop closes the panel. Defaults to false. */
  @Input() closeOnBackdrop = false;
  /**
   * Stacking depth for layered panels. 0 = outermost (default).
   * Each increment raises both backdrop and panel z-index by Z_STACK_STEP,
   * so a second slide-over opened on top should receive stackIndex=1.
   */
  @Input() stackIndex = 0;
  @Output() closed = new EventEmitter<void>();

  get panelWidth(): string {
    return SIZE_MAP[this.size] ?? '480px';
  }

  get backdropZ(): number {
    return Z_BASE + this.stackIndex * Z_STACK_STEP;
  }

  get panelZ(): number {
    return Z_BASE + this.stackIndex * Z_STACK_STEP + 1;
  }

  close(): void {
    this.closed.emit();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) this.close();
  }
}
