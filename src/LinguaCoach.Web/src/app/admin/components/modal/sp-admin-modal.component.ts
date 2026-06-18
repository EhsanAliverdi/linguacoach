import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (open) {
      <!--
        TailAdmin modal pattern (shared/components/ui/modal/modal.component.html):
        Backdrop: fixed inset-0 bg-gray-400/50 backdrop-blur-[32px]
        Panel:    fixed inset-0 flex items-center justify-center z-[99999]
                  relative w-full rounded-3xl bg-white dark:bg-gray-900
        Close:    absolute right-3 top-3 h-9.5 w-9.5 rounded-full bg-gray-100
                  hover:bg-gray-200 text-gray-400 hover:text-gray-700
      -->
      <div
        class="sp-modal-backdrop fixed inset-0 bg-gray-400/50 backdrop-blur-sm z-[99998]"
        (click)="onBackdropClick()"
        aria-hidden="true"
      ></div>
      <div class="fixed inset-0 flex items-center justify-center z-[99999] p-4">
        <div
          class="sp-modal-panel relative w-full rounded-3xl bg-white dark:bg-gray-900 shadow-2xl"
          role="dialog"
          [attr.aria-label]="title"
          aria-modal="true"
          style="max-width:520px;max-height:calc(100vh - 64px);overflow-y:auto"
        >
          <!-- TailAdmin close button: absolute right-3 top-3 rounded-full bg-gray-100 -->
          <button
            (click)="close()"
            class="sp-modal-close absolute right-3 top-3 flex h-9 w-9 items-center justify-center rounded-full bg-gray-100 text-gray-400 hover:bg-gray-200 hover:text-gray-700 dark:bg-gray-800 dark:hover:bg-gray-700 dark:hover:text-white transition-colors"
            aria-label="Close dialog"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
              <path fill-rule="evenodd" clip-rule="evenodd"
                d="M6.04289 16.5413C5.65237 16.9318 5.65237 17.565 6.04289 17.9555C6.43342 18.346 7.06658 18.346 7.45711 17.9555L11.9987 13.4139L16.5408 17.956C16.9313 18.3466 17.5645 18.3466 17.955 17.956C18.3455 17.5655 18.3455 16.9323 17.955 16.5418L13.4129 11.9997L17.955 7.4576C18.3455 7.06707 18.3455 6.43391 17.955 6.04338C17.5645 5.65286 16.9313 5.65286 16.5408 6.04338L11.9987 10.5855L7.45711 6.0439C7.06658 5.65338 6.43342 5.65338 6.04289 6.0439C5.65237 6.43442 5.65237 7.06759 6.04289 7.45811L10.5845 11.9997L6.04289 16.5413Z"
                fill="currentColor"/>
            </svg>
          </button>
          <div class="sp-modal-header px-6 pt-6 pb-0">
            <div class="sp-modal-title text-base font-semibold text-gray-800 dark:text-white/90 pr-8">{{ title }}</div>
            @if (subtitle) {
              <div class="sp-modal-subtitle mt-1 text-sm text-gray-500 dark:text-gray-400">{{ subtitle }}</div>
            }
          </div>
          <div class="sp-modal-body px-6 py-5">
            <ng-content />
          </div>
          <div class="sp-modal-footer px-6 pb-6 flex gap-3 justify-end">
            <ng-content select="[slot=footer]" />
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    /* TailAdmin-backed: rounded-3xl bg-white fixed modal with backdrop-blur */
    .sp-modal-footer:empty { display: none; }
  `],
})
export class SpAdminModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() subtitle = '';
  @Input() closeOnBackdrop = true;
  @Output() closed = new EventEmitter<void>();

  close(): void { this.closed.emit(); }

  onBackdropClick(): void {
    if (this.closeOnBackdrop) this.close();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open) this.close();
  }
}
