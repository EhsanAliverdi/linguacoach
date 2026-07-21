import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartComponent, ApexNonAxisChartSeries, ApexChart, ApexLegend, ApexDataLabels, ApexTooltip, ApexNoData, ApexPlotOptions, ApexStroke } from 'ng-apexcharts';
import { getSpAdminChartPalette, getSpAdminChartFont, getSpAdminChartSurface, buildSpAdminChartNoData } from '../chart/chart-theme.util';

export interface DonutSegment {
  label: string;
  pct: number;
  color: string;
}

/**
 * Flowbite-styled donut chart backed by ApexCharts. Kept on the existing
 * `title` + `segments` (label/pct/color) Input shape rather than the axis
 * chart `series`/`categories` contract — donut/pie data is naturally a flat
 * set of labelled proportions, and this shape matches what call sites
 * already compute (see admin-ai-usage's aggCategoryDonutSegments()).
 */
@Component({
  selector: 'sp-admin-donut-chart',
  standalone: true,
  imports: [CommonModule, ChartComponent],
  template: `
    @if (title) {
      <div class="sp-donut-title">{{ title }}</div>
    }
    <div class="sp-donut-body" [attr.aria-label]="ariaLabel || title || 'Donut chart'">
      <apx-chart
        [series]="apexSeries"
        [chart]="apexChart"
        [labels]="apexLabels"
        [colors]="apexColors"
        [legend]="apexLegend"
        [dataLabels]="apexDataLabels"
        [tooltip]="apexTooltip"
        [stroke]="apexStroke"
        [plotOptions]="apexPlotOptions"
        [noData]="apexNoData"
      ></apx-chart>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-donut-title { font-size: 13.5px; font-weight: 700; color: var(--sp-admin-text,#211B36); margin-bottom: 14px; }
    .sp-donut-body { width: 100%; }
  `],
})
export class SpAdminDonutChartComponent implements OnChanges {
  @Input() title = '';
  @Input() segments: DonutSegment[] = [];
  @Input() size = 80;
  @Input() showLegend = true;
  @Input() showDataLabels = false;
  @Input() ariaLabel = '';
  @Input() emptyMessage = 'No data';

  apexSeries: ApexNonAxisChartSeries = [];
  apexLabels: string[] = [];
  apexColors: string[] = [];
  apexChart!: ApexChart;
  apexLegend!: ApexLegend;
  apexDataLabels!: ApexDataLabels;
  apexTooltip!: ApexTooltip;
  apexStroke!: ApexStroke;
  apexPlotOptions!: ApexPlotOptions;
  apexNoData!: ApexNoData;

  ngOnChanges(): void {
    const palette = getSpAdminChartPalette();

    this.apexSeries = this.segments.map(s => s.pct);
    this.apexLabels = this.segments.map(s => s.label);
    this.apexColors = this.segments.map((s, i) => s.color ?? palette[i % palette.length]);

    this.apexChart = {
      type: 'donut',
      height: this.size * 2.1,
      fontFamily: getSpAdminChartFont(),
      toolbar: { show: false },
    };

    this.apexStroke = { width: 2, colors: [getSpAdminChartSurface()] };

    this.apexPlotOptions = {
      pie: {
        donut: {
          size: '68%',
          labels: { show: false },
        },
      },
    };

    this.apexLegend = {
      show: this.showLegend,
      position: 'right',
      fontSize: '12px',
      fontFamily: getSpAdminChartFont(),
      formatter: (label: string, opts: { w: { globals: { series: number[] } }; seriesIndex: number }) =>
        `${label} — ${opts.w.globals.series[opts.seriesIndex]}%`,
      markers: { strokeWidth: 0 },
      itemMargin: { horizontal: 6, vertical: 4 },
    };

    this.apexDataLabels = {
      enabled: this.showDataLabels,
      formatter: (val: number) => `${Math.round(val)}%`,
      style: { fontSize: '10px', fontFamily: getSpAdminChartFont(), fontWeight: 700 },
    };

    this.apexTooltip = {
      enabled: true,
      style: { fontSize: '12px', fontFamily: getSpAdminChartFont() },
      y: { formatter: (val: number) => `${val}%` },
    };

    this.apexNoData = buildSpAdminChartNoData(this.emptyMessage);
  }
}
