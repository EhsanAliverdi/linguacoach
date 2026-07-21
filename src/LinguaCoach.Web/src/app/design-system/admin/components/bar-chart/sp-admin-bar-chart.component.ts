import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartComponent, ApexAxisChartSeries, ApexChart, ApexPlotOptions, ApexXAxis, ApexYAxis, ApexGrid, ApexTooltip, ApexLegend, ApexDataLabels, ApexNoData } from 'ng-apexcharts';
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
 * Flowbite-styled column (vertical bar) chart backed by ApexCharts. Set
 * `horizontal` to render Flowbite's horizontal-bar variant from the same
 * component instead of a second one, since ApexCharts toggles orientation
 * via a single plotOptions flag.
 */
@Component({
  selector: 'sp-admin-bar-chart',
  standalone: true,
  imports: [CommonModule, ChartComponent],
  template: `
    <div class="sp-bc-root" [attr.aria-label]="ariaLabel || 'Bar chart'">
      <apx-chart
        [series]="apexSeries"
        [chart]="apexChart"
        [plotOptions]="apexPlotOptions"
        [xaxis]="apexXAxis"
        [yaxis]="apexYAxis"
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
    .sp-bc-root { width: 100%; }
  `],
})
export class SpAdminBarChartComponent extends SpAdminAxisChartBase implements OnChanges {
  @Input() horizontal = false;
  @Input() borderRadius = 5;

  apexSeries: ApexAxisChartSeries = [];
  apexChart!: ApexChart;
  apexPlotOptions!: ApexPlotOptions;
  apexXAxis!: ApexXAxis;
  apexYAxis!: ApexYAxis;
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
      type: 'bar',
      height: this.height,
      width: this.width,
      fontFamily: getSpAdminChartFont(),
      toolbar: { show: false },
      stacked: false,
    };

    this.apexPlotOptions = {
      bar: {
        horizontal: this.horizontal,
        borderRadius: this.borderRadius,
        borderRadiusApplication: 'end',
        columnWidth: '55%',
        barHeight: '55%',
      },
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
