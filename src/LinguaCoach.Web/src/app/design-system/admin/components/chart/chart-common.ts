import { Directive, Input } from '@angular/core';

export interface SpAdminChartSeries {
  name: string;
  data: number[];
  color?: string;
}

/**
 * Shared Input contract for every axis-based sp-admin-*-chart component
 * (area, bar/column, horizontal-bar, line, combo). Angular supports @Input
 * inheritance from an undecorated-selector @Directive() base — this keeps
 * every axis chart's public API identical without repeating Inputs per file.
 */
@Directive()
export abstract class SpAdminAxisChartBase {
  @Input() series: SpAdminChartSeries[] = [];
  @Input() categories: string[] = [];
  @Input() height: number | string = 200;
  @Input() width: number | string = '100%';
  @Input() colors: string[] = [];
  @Input() showGrid = true;
  @Input() showLegend = false;
  @Input() legendPosition: 'top' | 'bottom' = 'bottom';
  @Input() showTooltip = true;
  @Input() showDataLabels = false;
  @Input() showXAxisLabels = true;
  @Input() showYAxisLabels = true;
  @Input() valuePrefix = '';
  @Input() valueSuffix = '';
  @Input() decimals = 0;
  @Input() emptyMessage = 'No data for this period';
  @Input() ariaLabel = '';

  protected get hasData(): boolean {
    return this.series.length > 0 && this.series.some(s => s.data.length > 0);
  }

  protected get format() {
    return { prefix: this.valuePrefix, suffix: this.valueSuffix, decimals: this.decimals };
  }
}
