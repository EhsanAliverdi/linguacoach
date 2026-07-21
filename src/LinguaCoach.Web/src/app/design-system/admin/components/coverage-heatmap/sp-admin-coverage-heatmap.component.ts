import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/** One row of the matrix (e.g. a CEFR level). `color` tints that row's cells; omit for the default primary tint. */
export interface SpAdminHeatmapRow {
  key: string;
  label: string;
  color?: string;
}

/** One column of the matrix (e.g. a skill). */
export interface SpAdminHeatmapColumn {
  key: string;
  label: string;
}

/** One rowKey x columnKey data point. `value` drives cell color intensity (relative to the grid's max). */
export interface SpAdminHeatmapCell {
  rowKey: string;
  columnKey: string;
  value: number;
  /** Small badge count shown in the cell's corner (e.g. "pending"). Omit or 0 to hide. */
  secondaryValue?: number;
  /** Renders the cell as actionable — hover affordance, emits (cellClick) on click. */
  clickable?: boolean;
}

/**
 * Generic rowKey x columnKey heatmap: color-intensity cells, optional secondary badge, hover
 * tooltip, and a fewer/more legend. Data-only — no title/card chrome, so it drops into any
 * sp-admin-card/sp-admin-section-header the same way sp-admin-bar-chart etc. do.
 *
 * Usage:
 *   <sp-admin-coverage-heatmap
 *     [rows]="[{key:'A1', label:'A1', color:'#13B07C'}, ...]"
 *     [columns]="[{key:'writing', label:'writing'}, ...]"
 *     [cells]="[{rowKey:'A1', columnKey:'writing', value:7, secondaryValue:0}, ...]"
 *     valueLabel="approved" secondaryLabel="pending"
 *     (cellClick)="onGapClick($event)"
 *   />
 */
@Component({
  selector: 'sp-admin-coverage-heatmap',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sp-hmap-root" [attr.aria-label]="ariaLabel || 'Coverage heatmap'">
      @if (!rows.length || !columns.length) {
        <div class="sp-hmap-empty">{{ emptyMessage }}</div>
      } @else {
        @if (showScale) {
          <div class="sp-hmap-scale-row">
            <span class="sp-hmap-scale-label">fewer</span>
            <span class="sp-hmap-scale-bar"></span>
            <span class="sp-hmap-scale-label">more</span>
          </div>
        }
        <div class="sp-hmap-scroll">
          <div class="sp-hmap-grid" [style.grid-template-columns]="'var(--sp-hmap-rowhead-w, 90px) repeat(' + columns.length + ', minmax(44px, 1fr))'">
            <div></div>
            @for (col of columns; track col.key) {
              <div class="sp-hmap-colhead">{{ col.label }}</div>
            }
            @for (row of rows; track row.key) {
              <div class="sp-hmap-rowhead" [style.color]="row.color || 'var(--sp-admin-primary,#5B4BE8)'">{{ row.label }}</div>
              @for (col of columns; track col.key) {
                @let cell = cellOf(row.key, col.key);
                @let hovered = hoverKey === cellKey(row.key, col.key);
                <div
                  class="sp-hmap-cell"
                  [class.sp-hmap-cell--hovered]="hovered"
                  [class.sp-hmap-cell--clickable]="cell?.clickable"
                  [style.background]="cellBackground(cell, row)"
                  [style.color]="cellTextColor(cell)"
                  (mouseenter)="hoverKey = cellKey(row.key, col.key)"
                  (mouseleave)="hoverKey = hoverKey === cellKey(row.key, col.key) ? null : hoverKey"
                  (click)="cell?.clickable && cellClick.emit(cell!)"
                >
                  {{ cell?.value ?? 0 }}
                  @if (cell?.secondaryValue) {
                    <span class="sp-hmap-secondary" [attr.title]="cell!.secondaryValue + ' ' + secondaryLabel">{{ cell!.secondaryValue }}</span>
                  }
                  @if (hovered) {
                    <div class="sp-hmap-tooltip">
                      {{ row.label }} · {{ col.label }}: {{ cell?.value ?? 0 }} {{ valueLabel }}{{ cell?.secondaryValue ? ', ' + cell!.secondaryValue + ' ' + secondaryLabel : '' }}
                    </div>
                  }
                </div>
              }
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-hmap-root { display: flex; flex-direction: column; gap: 10px; }
    .sp-hmap-empty { font-size: 13px; color: var(--sp-admin-text-muted, #8B85A0); padding: 8px 0; }

    .sp-hmap-scale-row {
      display: flex; align-items: center; gap: 6px;
      font-size: 11px; font-weight: 700; color: var(--sp-admin-text-muted, #8B85A0);
      justify-content: flex-end;
    }
    .sp-hmap-scale-bar {
      width: 56px; height: 8px; border-radius: 99px;
      background: linear-gradient(90deg, #fff, var(--sp-admin-primary, #5B4BE8));
      border: 1px solid var(--sp-admin-border, #ECE9F5);
    }

    .sp-hmap-scroll { overflow-x: auto; }
    .sp-hmap-grid { display: grid; gap: 4px; min-width: 560px; }

    .sp-hmap-colhead {
      font-size: 10.5px; font-weight: 800; color: var(--sp-admin-text-muted, #8B85A0);
      text-transform: capitalize; text-align: center; padding-bottom: 6px;
      writing-mode: vertical-rl; transform: rotate(180deg);
      height: 70px; display: flex; align-items: center; justify-content: center;
    }
    .sp-hmap-rowhead {
      font-size: 12.5px; font-weight: 800;
      display: flex; align-items: center; padding-right: 8px;
    }
    .sp-hmap-cell {
      position: relative; border-radius: 7px;
      display: flex; align-items: center; justify-content: center;
      font-size: 13px; font-weight: 800; min-height: 38px;
      cursor: default; transition: transform .1s;
    }
    .sp-hmap-cell--clickable { cursor: pointer; }
    .sp-hmap-cell--hovered { transform: scale(1.08); z-index: 2; box-shadow: 0 4px 12px rgba(33,27,54,.18); }

    .sp-hmap-secondary {
      position: absolute; top: 3px; right: 3px;
      background: var(--sp-admin-warn-ink, #B26410); color: #fff;
      font-size: 9px; font-weight: 800; min-width: 13px; height: 13px;
      border-radius: 99px; display: flex; align-items: center; justify-content: center;
      padding: 0 3px; line-height: 1;
    }
    .sp-hmap-tooltip {
      position: absolute; bottom: 100%; left: 50%; transform: translateX(-50%);
      margin-bottom: 6px; background: var(--sp-admin-text, #211B36); color: #fff;
      font-size: 11px; font-weight: 700; padding: 4px 8px; border-radius: 6px;
      white-space: nowrap; text-transform: capitalize; z-index: 5;
    }
  `],
})
export class SpAdminCoverageHeatmapComponent implements OnChanges {
  @Input({ required: true }) rows: SpAdminHeatmapRow[] = [];
  @Input({ required: true }) columns: SpAdminHeatmapColumn[] = [];
  @Input({ required: true }) cells: SpAdminHeatmapCell[] = [];
  /** Noun used in the hover tooltip and secondary-badge title (e.g. "approved" / "pending"). */
  @Input() valueLabel = 'value';
  @Input() secondaryLabel = 'pending';
  @Input() showScale = true;
  @Input() emptyMessage = 'No data yet.';
  @Input() ariaLabel = '';
  @Output() cellClick = new EventEmitter<SpAdminHeatmapCell>();

  hoverKey: string | null = null;

  private cellMap = new Map<string, SpAdminHeatmapCell>();
  private maxValue = 1;

  ngOnChanges(): void {
    this.cellMap = new Map(this.cells.map(c => [this.cellKey(c.rowKey, c.columnKey), c]));
    this.maxValue = Math.max(1, ...this.cells.map(c => c.value));
  }

  cellKey(rowKey: string, columnKey: string): string {
    return `${rowKey}::${columnKey}`;
  }

  cellOf(rowKey: string, columnKey: string): SpAdminHeatmapCell | undefined {
    return this.cellMap.get(this.cellKey(rowKey, columnKey));
  }

  cellBackground(cell: SpAdminHeatmapCell | undefined, row: SpAdminHeatmapRow): string {
    const color = row.color || 'var(--sp-admin-primary,#5B4BE8)';
    const t = (cell?.value ?? 0) / this.maxValue;
    return `color-mix(in oklch, ${color} ${10 + t * 70}%, #fff)`;
  }

  cellTextColor(cell: SpAdminHeatmapCell | undefined): string {
    const t = (cell?.value ?? 0) / this.maxValue;
    return t > 0.55 ? '#fff' : 'var(--sp-admin-text,#211B36)';
  }
}
