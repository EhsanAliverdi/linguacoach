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
    <nav class="sp-adm-pagination" aria-label="Pagination">
      <span class="sp-adm-pagination-label">Page {{ safePage }} of {{ safeTotalPages }}</span>
      <div class="sp-adm-pagination-actions">
        <sp-admin-button variant="neutral" appearance="outline" size="sm" [disabled]="safePage <= 1" (click)="goTo(safePage - 1)">Previous</sp-admin-button>
        <sp-admin-button variant="neutral" appearance="outline" size="sm" [disabled]="safePage >= safeTotalPages" (click)="goTo(safePage + 1)">Next</sp-admin-button>
      </div>
    </nav>
  `,
  styles: [`
    .sp-adm-pagination {
      display:flex;
      align-items:center;
      justify-content:space-between;
      gap:12px;
      flex-wrap:wrap;
      padding:14px 18px;
      border-top:1px solid #f1f5f9;
      background:#fff;
    }
    .sp-adm-pagination-label {
      color:#667085;
      font-size:13px;
      font-weight:600;
    }
    .sp-adm-pagination-actions {
      display:flex;
      align-items:center;
      gap:8px;
    }
  `],
})
export class SpAdminPaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();

  get safeTotalPages(): number {
    return Math.max(1, Number(this.totalPages) || 1);
  }

  get safePage(): number {
    return Math.min(Math.max(1, Number(this.page) || 1), this.safeTotalPages);
  }

  goTo(page: number): void {
    const nextPage = Math.min(Math.max(1, page), this.safeTotalPages);
    if (nextPage !== this.safePage) {
      this.pageChange.emit(nextPage);
    }
  }
}
