import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'sp-admin-pagination',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Matches .adm-pagination: flex space-between, 12/16px padding, border-top border -->
    <nav class="sp-adm-pagination" aria-label="Pagination">
      <span class="sp-adm-pag-info">Page {{ safePage }} of {{ safeTotalPages }}{{ totalRows != null ? ' · ' + totalRows + ' rows' : '' }}</span>
      <div class="sp-adm-pag-btns">
        <button
          class="sp-adm-pag-btn"
          [disabled]="safePage <= 1 || null"
          (click)="goTo(safePage - 1)"
          aria-label="Previous page"
        >Previous</button>
        @for (entry of pageEntries; track entry.key) {
          @if (entry.ellipsis) {
            <span class="sp-adm-pag-ellipsis">…</span>
          } @else {
            <button
              class="sp-adm-pag-btn"
              [class.sp-adm-pag-btn-cur]="entry.page === safePage"
              (click)="goTo(entry.page!)"
              [attr.aria-current]="entry.page === safePage ? 'page' : null"
            >{{ entry.page }}</button>
          }
        }
        <button
          class="sp-adm-pag-btn"
          [disabled]="safePage >= safeTotalPages || null"
          (click)="goTo(safePage + 1)"
          aria-label="Next page"
        >Next</button>
      </div>
    </nav>
  `,
  styles: [`
    /* .adm-pagination: flex, space-between, 12/16px padding, border-top */
    .sp-adm-pagination {
      display:flex;
      align-items:center;
      justify-content:space-between;
      gap:12px;
      flex-wrap:wrap;
      padding:12px 16px;
      border-top:1px solid #ECE9F5;
      background:#fff;
    }
    /* .adm-pag-info: 13px/muted */
    .sp-adm-pag-info {
      font-size:13px;
      color:#8B85A0;
      font-weight:500;
    }
    .sp-adm-pag-btns {
      display:flex;
      gap:6px;
      align-items:center;
    }
    /* .adm-pag-btn: 30px height, 0/11px padding, 7px radius, 13px/600, border-2 */
    .sp-adm-pag-btn {
      height:30px;
      padding:0 11px;
      border-radius:7px;
      font-size:13px;
      font-weight:600;
      border:1.5px solid #E2DEF0;
      background:#fff;
      color:#211B36;
      cursor:pointer;
      transition:background .1s;
      font-family:inherit;
      line-height:1;
    }
    .sp-adm-pag-btn:hover:not(:disabled) { background:#F6F4FB; }
    .sp-adm-pag-btn:disabled { opacity:.35; cursor:default; }
    /* .adm-pag-btn.cur: indigo bg, white text, indigo border */
    .sp-adm-pag-btn-cur {
      background:#5B4BE8;
      color:#fff;
      border-color:#5B4BE8;
    }
    .sp-adm-pag-btn-cur:hover:not(:disabled) { background:#3A2EA8; }
    .sp-adm-pag-ellipsis { padding: 0 4px; color: #8B85A0; font-size: 13px; line-height: 30px; }
  `],
})
export class SpAdminPaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Input() totalRows: number | null = null;
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

  get pageEntries(): { key: string; page?: number; ellipsis?: boolean }[] {
    const total = this.safeTotalPages;
    const cur = this.safePage;
    const nums: number[] = total <= 7
      ? Array.from({ length: total }, (_, i) => i + 1)
      : [...new Set([1, total, cur - 1, cur, cur + 1].filter(p => p >= 1 && p <= total))].sort((a, b) => a - b);

    const entries: { key: string; page?: number; ellipsis?: boolean }[] = [];
    for (let i = 0; i < nums.length; i++) {
      if (i > 0 && nums[i] - nums[i - 1] > 1) {
        entries.push({ key: `ellipsis-${i}`, ellipsis: true });
      }
      entries.push({ key: `page-${nums[i]}`, page: nums[i] });
    }
    return entries;
  }
}
