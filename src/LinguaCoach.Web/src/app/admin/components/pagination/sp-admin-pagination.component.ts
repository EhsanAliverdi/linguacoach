import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SpAdminButtonComponent } from '../button/sp-admin-button.component';

@Component({
  selector: 'sp-admin-pagination',
  standalone: true,
  imports: [CommonModule, SpAdminButtonComponent],
  template: `
    <nav class="sp-adm-pagination" aria-label="Pagination">
      <sp-admin-button variant="ghost" size="sm" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">Previous</sp-admin-button>
      <span>Page {{ page }} of {{ totalPages }}</span>
      <sp-admin-button variant="ghost" size="sm" [disabled]="page >= totalPages" (click)="pageChange.emit(page + 1)">Next</sp-admin-button>
    </nav>
  `,
  styles: [`
    .sp-adm-pagination {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 14px;
      padding: 14px 0;
      color: var(--sp-admin-text-muted);
      font-size: 12px;
      font-weight: 700;
    }
  `],
})
export class SpAdminPaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Output() pageChange = new EventEmitter<number>();
}
