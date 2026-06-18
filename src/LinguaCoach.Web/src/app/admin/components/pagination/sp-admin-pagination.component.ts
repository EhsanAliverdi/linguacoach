import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';

@Component({
  selector: 'sp-admin-pagination',
  standalone: true,
  imports: [CommonModule, SpAdminButtonComponent],
  template: `
    <!--
      TailAdmin pagination pattern: flex items-center justify-between px-4 py-3
      border-t border-gray-100, prev/next as rounded-lg border buttons,
      page indicator text-sm text-gray-500.
    -->
    <nav class="sp-adm-pagination flex items-center justify-between px-5 py-3 border-t border-gray-100 dark:border-gray-800" aria-label="Pagination">
      <span class="text-sm text-gray-500 dark:text-gray-400">Page {{ page }} of {{ totalPages }}</span>
      <div class="flex items-center gap-2">
        <sp-admin-button variant="ghost" size="sm" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">Previous</sp-admin-button>
        <sp-admin-button variant="ghost" size="sm" [disabled]="page >= totalPages" (click)="pageChange.emit(page + 1)">Next</sp-admin-button>
      </div>
    </nav>
  `,
  styles: [`/* TailAdmin-backed: flex justify-between border-t border-gray-100 pagination */`],
})
export class SpAdminPaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();
}
