import type {
  ApexDataLabels,
  ApexGrid,
  ApexLegend,
  ApexTooltip,
  ApexXAxis,
  ApexYAxis,
  ApexNoData,
} from 'ng-apexcharts';

/**
 * Bridges the sp-admin-* CSS custom property token system into ApexCharts
 * option fragments, so every chart automatically matches the design system
 * (and reacts to admin dark/light theme) without each chart component
 * repeating color literals.
 */

const DEFAULT_PALETTE = ['#5B4BE8', '#13B07C', '#B45CF0', '#F0982C', '#0A7468', '#FF7A59'];

function readToken(name: string, fallback: string): string {
  if (typeof document === 'undefined') return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name)?.trim();
  return value || fallback;
}

/** Default multi-series color sequence, sourced from the sp-admin tone tokens. */
export function getSpAdminChartPalette(): string[] {
  return [
    readToken('--sp-admin-primary', DEFAULT_PALETTE[0]),
    readToken('--sp-admin-green', DEFAULT_PALETTE[1]),
    readToken('--sp-admin-violet', DEFAULT_PALETTE[2]),
    readToken('--sp-admin-amber', DEFAULT_PALETTE[3]),
    readToken('--sp-admin-teal', DEFAULT_PALETTE[4]),
    readToken('--sp-admin-coral', DEFAULT_PALETTE[5]),
  ];
}

export function getSpAdminChartFont(): string {
  return readToken('--sp-admin-font', `"Plus Jakarta Sans", ui-sans-serif, system-ui, -apple-system, sans-serif`);
}

export function getSpAdminChartTextMuted(): string {
  return readToken('--sp-admin-text-muted', '#8B85A0');
}

export function getSpAdminChartBorder(): string {
  return readToken('--sp-admin-border', '#ECE9F5');
}

export function getSpAdminChartSurface(): string {
  return readToken('--sp-admin-surface', '#FFFFFF');
}

export interface SpAdminChartFormat {
  prefix: string;
  suffix: string;
  decimals: number;
}

export function formatSpAdminChartValue(value: number, fmt: SpAdminChartFormat): string {
  return `${fmt.prefix}${value.toFixed(fmt.decimals)}${fmt.suffix}`;
}

/** Shared axis-label style fragment, reused by every axis (x/y) definition. */
export function spAdminChartAxisLabelStyle() {
  return {
    colors: getSpAdminChartTextMuted(),
    fontSize: '11px',
    fontFamily: getSpAdminChartFont(),
    fontWeight: 600,
  };
}

export function buildSpAdminChartGrid(showGrid: boolean): ApexGrid {
  return {
    show: showGrid,
    borderColor: getSpAdminChartBorder(),
    strokeDashArray: 4,
    xaxis: { lines: { show: false } },
    yaxis: { lines: { show: showGrid } },
    padding: { left: 4, right: 4 },
  };
}

export function buildSpAdminChartDataLabels(show: boolean): ApexDataLabels {
  return {
    enabled: show,
    style: {
      fontSize: '10px',
      fontFamily: getSpAdminChartFont(),
      fontWeight: 700,
    },
  };
}

export function buildSpAdminChartTooltip(show: boolean, fmt: SpAdminChartFormat): ApexTooltip {
  return {
    enabled: show,
    theme: 'light',
    style: { fontSize: '12px', fontFamily: getSpAdminChartFont() },
    y: { formatter: (value: number) => formatSpAdminChartValue(value, fmt) },
  };
}

export function buildSpAdminChartLegend(show: boolean, position: 'top' | 'bottom'): ApexLegend {
  return {
    show,
    position,
    fontSize: '12px',
    fontFamily: getSpAdminChartFont(),
    labels: { colors: getSpAdminChartTextMuted() },
    markers: { strokeWidth: 0 },
    itemMargin: { horizontal: 8, vertical: 4 },
  };
}

export function buildSpAdminChartXAxis(categories: string[], show: boolean): ApexXAxis {
  return {
    categories,
    labels: { show, style: spAdminChartAxisLabelStyle() },
    axisBorder: { show: false },
    axisTicks: { show: false },
  };
}

export function buildSpAdminChartYAxis(show: boolean, fmt: SpAdminChartFormat): ApexYAxis {
  return {
    show,
    labels: {
      show,
      style: spAdminChartAxisLabelStyle(),
      formatter: (value: number) => formatSpAdminChartValue(value, fmt),
    },
  };
}

export function buildSpAdminChartNoData(message: string): ApexNoData {
  return {
    text: message,
    style: {
      color: getSpAdminChartTextMuted(),
      fontSize: '13px',
      fontFamily: getSpAdminChartFont(),
    },
  };
}
