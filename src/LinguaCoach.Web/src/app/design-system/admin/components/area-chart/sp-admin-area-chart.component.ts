import { Component, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartComponent, ApexAxisChartSeries, ApexChart, ApexStroke, ApexFill, ApexXAxis, ApexYAxis, ApexGrid, ApexTooltip, ApexLegend, ApexDataLabels, ApexNoData } from 'ng-apexcharts';
import { SpAdminAxisChartBase } from '../chart/chart-common';
import {
  getSpAdminChartPalette,
  getSpAdminChartFont,
  buildSpAdminChartGrid,
  buildSpAdminChartDataLabels,
  buildSpAdminChartTooltip,
  buildSpAdminChartLegend,
  buildSpAdminChartXAxis,
  buildSpAdminChartYAxis,
  buildSpAdminChartNoData,
} from '../chart/chart-theme.util';

/**
 * Flowbite-styled area chart backed by ApexCharts. Supports single or
 * multi-series data via the shared `series`/`categories` Input contract
 * (see SpAdminAxisChartBase).
 */
@Component({
  selector: 'sp-admin-area-chart',
  standalone: true,
  imports: [CommonModule, ChartComponent],
  template: `
    <div class="sp-ac-root" [attr.aria-label]="ariaLabel || 'Area chart'">
      <apx-chart
        [series]="apexSeries"
        [chart]="apexChart"
        [xaxis]="apexXAxis"
        [yaxis]="apexYAxis"
        [stroke]="apexStroke"
        [fill]="apexFill"
        [colors]="apexColors"
        [grid]="apexGrid"
        [dataLabels]="apexDataLabels"
        [tooltip]="apexTooltip"
        [legend]="apexLegend"
        [noData]="apexNoData"
      ></apx-chart>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .sp-ac-root { width: 100%; }
  `],
})
export class SpAdminAreaChartComponent extends SpAdminAxisChartBase implements OnChanges {
  apexSeries: ApexAxisChartSeries = [];
  apexChart!: ApexChart;
  apexXAxis!: ApexXAxis;
  apexYAxis!: ApexYAxis;
  apexStroke!: ApexStroke;
  apexFill!: ApexFill;
  apexColors: string[] = [];
  apexGrid!: ApexGrid;
  apexDataLabels!: ApexDataLabels;
  apexTooltip!: ApexTooltip;
  apexLegend!: ApexLegend;
  apexNoData!: ApexNoData;

  ngOnChanges(): void {
    const palette = this.colors.length ? this.colors : getSpAdminChartPalette();

    this.apexSeries = this.series.map(s => ({ name: s.name, data: s.data }));
    this.apexColors = this.series.map((s, i) => s.color ?? palette[i % palette.length]);

    this.apexChart = {
      type: 'area',
      height: this.height,
      width: this.width,
      fontFamily: getSpAdminChartFont(),
      toolbar: { show: false },
      zoom: { enabled: false },
    };

    this.apexStroke = { curve: 'smooth', width: 2.5 };
    this.apexFill = {
      type: 'gradient',
      gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0.02, stops: [0, 90, 100] },
    };

    this.apexXAxis = buildSpAdminChartXAxis(this.categories, this.showXAxisLabels);
    this.apexYAxis = buildSpAdminChartYAxis(this.showYAxisLabels, this.format);
    this.apexGrid = buildSpAdminChartGrid(this.showGrid);
    this.apexDataLabels = buildSpAdminChartDataLabels(this.showDataLabels);
    this.apexTooltip = buildSpAdminChartTooltip(this.showTooltip, this.format);
    this.apexLegend = buildSpAdminChartLegend(this.showLegend, this.legendPosition);
    this.apexNoData = buildSpAdminChartNoData(this.emptyMessage);
  }
}
