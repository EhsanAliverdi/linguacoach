import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpAdminDrawerSide = 'left' | 'right';
export type SpAdminDrawerSize = 'sm' | 'md' | 'lg' | 'xl';

const DRAWER_SIZE_MAP: Record<SpAdminDrawerSize, string> = {
  sm: '320px',
  md: '420px',
  lg: '560px',
  xl: '720px',
};

// TailAdmin drawer (shared/components/ui/drawer):
// fixed top-0 right-0 h-screen bg-white border-l border-gray-200 shadow-2xl w-[420px]
@Component({
  selector: 'sp-admin-drawer',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (open) {
      <div
        class="sp-adm-drawer-backdrop fixed inset-0 bg-gray-400/50 backdrop-blur-sm z-[99998]"
        (click)="closeOnBackdrop && closed.emit()"
        aria-hidden="true"
      ></div>
      <aside
        class="sp-adm-drawer fixed top-0 h-screen bg-white dark:bg-gray-900 shadow-2xl z-[99999] overflow-auto flex flex-col"
        [class]="drawerClasses"
        [style.width]="drawerWidth"
        [style.max-width]="'100vw'"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="title"
      >
        <header class="sp-adm-drawer-header flex items-center justify-between gap-3 px-5 py-4 border-b border-gray-100 dark:border-gray-800 shrink-0">
          <ng-content select="[slot=header]" />
          @if (title && !hasHeaderSlot) {
            <h2 class="text-base font-semibold text-gray-800 dark:text-white/90 m-0">{{ title }}</h2>
          }
          <button
            type="button"
            (click)="closed.emit()"
            class="flex h-9 w-9 items-center justify-center rounded-full bg-gray-100 text-gray-400 hover:bg-gray-200 hover:text-gray-700 dark:bg-gray-800 dark:hover:bg-gray-700 dark:hover:text-white transition-colors ml-auto"
            aria-label="Close drawer"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path fill-rule="evenodd" clip-rule="evenodd"
                d="M6.04289 16.5413C5.65237 16.9318 5.65237 17.565 6.04289 17.9555C6.43342 18.346 7.06658 18.346 7.45711 17.9555L11.9987 13.4139L16.5408 17.956C16.9313 18.3466 17.5645 18.3466 17.955 17.956C18.3455 17.5655 18.3455 16.9323 17.955 16.5418L13.4129 11.9997L17.955 7.4576C18.3455 7.06707 18.3455 6.43391 17.955 6.04338C17.5645 5.65286 16.9313 5.65286 16.5408 6.04338L11.9987 10.5855L7.45711 6.0439C7.06658 5.65338 6.43342 5.65338 6.04289 6.0439C5.65237 6.43442 5.65237 7.06759 6.04289 7.45811L10.5845 11.9997L6.04289 16.5413Z"
                fill="currentColor"/>
            </svg>
          </button>
        </header>
        <div class="sp-adm-drawer-body p-5 flex-1 overflow-y-auto">
          <ng-content />
        </div>
        <div class="sp-adm-drawer-footer">
          <ng-content select="[slot=footer]" />
        </div>
      </aside>
    }
  `,
  styles: [`
    /* TailAdmin-backed: fixed right-0 / left-0 bg-white border shadow-2xl drawer */
    .sp-adm-drawer-right { right:0; border-left:1px solid #e5e7eb; }
    .sp-adm-drawer-left  { left:0;  border-right:1px solid #e5e7eb; }
    .sp-adm-drawer-footer:empty { display:none; }
  `],
})
export class SpAdminDrawerComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() side: SpAdminDrawerSide = 'right';
  @Input() size: SpAdminDrawerSize = 'md';
  @Input() closeOnBackdrop = true;
  @Output() closed = new EventEmitter<void>();

  readonly hasHeaderSlot = false;

  get drawerWidth(): string {
    return DRAWER_SIZE_MAP[this.size] ?? '420px';
  }

  get drawerClasses(): string {
    return `sp-adm-drawer-${this.side}`;
  }
}
