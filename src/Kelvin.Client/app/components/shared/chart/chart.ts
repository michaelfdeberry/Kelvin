import { consume } from '@lit/context';
import {
  Chart,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  TimeScale,
  Tooltip,
  type ChartDataset as ChartJsDataset,
  type LegendItem,
  type Plugin,
  type ScaleOptionsByType,
} from 'chart.js';
import 'chartjs-adapter-date-fns';
import { LitElement, html } from 'lit';
import { customElement, property, query } from 'lit/decorators.js';

import chartStyles from './chart.styles.js';
import { preferencesContext } from '../../../contexts/preferences-context.js';
import { Preferences } from '../../../models/preferences.js';
import sharedStyles from '../../../shared.styles.js';

Chart.register(LineController, LineElement, PointElement, LinearScale, TimeScale, Tooltip, Legend, Filler);

export type ChartDomain = {
  from: number;
  to: number;
};

export type ChartPoint = {
  at: number;
  value: number;
};

export type ChartInterval = {
  from: number;
  to: number;
};

export type ChartAxis = 'y' | 'y1';

export type ChartDataset =
  | {
      type: 'state';
      /** Stable identity used to preserve the dataset's hidden/shown state across re-renders. */
      key: string;
      intervals: ChartInterval[];
      color: string;
      label?: string;
    }
  | {
      type: 'line';
      /** Stable identity used to preserve the dataset's hidden/shown state across re-renders. */
      key: string;
      points: ChartPoint[];
      color: string;
      axis?: ChartAxis;
      min?: number;
      max?: number;
      label?: string;
      /** Whether the series starts hidden, toggled on via the legend (e.g. per-sensor overlays). */
      hidden?: boolean;
      valueFormatter?: (value: number) => string;
    };

type LineDataset = Extract<ChartDataset, { type: 'line' }>;
type StateDataset = Extract<ChartDataset, { type: 'state' }>;
type StateBandsConfig = { bands: StateDataset[]; hiddenKeys: Set<string> };

// Kept outside chart.js's options object (rather than a custom plugin option) so chart.js's DeepPartial
// option typing doesn't cascade through our dataset types and turn every field optional.
const stateBandsByChart = new WeakMap<Chart, StateBandsConfig>();

// Sentinel offset so legend clicks on state bands (which aren't real Chart.js datasets) can be told apart
// from clicks on real line datasets, whose datasetIndex is always a small non-negative number.
const BAND_LEGEND_INDEX_OFFSET = 100_000;

const stateBandsPlugin: Plugin<'line'> = {
  id: 'stateBands',
  beforeDatasetsDraw(chart) {
    const config = stateBandsByChart.get(chart);
    if (!config?.bands.length) return;

    const { ctx, chartArea, scales } = chart;
    const xScale = scales.x;
    if (!chartArea || !xScale) return;

    ctx.save();
    ctx.globalAlpha = 0.24;
    for (const band of config.bands) {
      if (config.hiddenKeys.has(band.key)) continue;

      ctx.fillStyle = band.color;
      for (const interval of band.intervals) {
        const x1 = xScale.getPixelForValue(interval.from);
        const x2 = xScale.getPixelForValue(interval.to);
        ctx.fillRect(Math.min(x1, x2), chartArea.top, Math.abs(x2 - x1), chartArea.bottom - chartArea.top);
      }
    }
    ctx.restore();
  },
};

@customElement('app-kelvin-chart')
export class KelvinChart extends LitElement {
  static override styles = [sharedStyles, chartStyles];

  @consume({ context: preferencesContext, subscribe: true })
  private preferences!: Preferences;

  @property({ type: Array })
  datasets: ChartDataset[] = [];

  @property({ attribute: false })
  domain?: ChartDomain;

  @query('canvas')
  private canvasElement?: HTMLCanvasElement;

  private chart?: Chart<'line'>;
  private hiddenBandKeys = new Set<string>();

  override render() {
    return html`<div class="chart-container"><canvas></canvas></div>`;
  }

  override firstUpdated() {
    this.createChart();
  }

  override updated() {
    if (this.chart) this.applyData();
  }

  override disconnectedCallback() {
    this.chart?.destroy();
    this.chart = undefined;
    super.disconnectedCallback();
  }

  private createChart() {
    if (!this.canvasElement) return;

    this.chart = new Chart(this.canvasElement, {
      type: 'line',
      data: { datasets: [] },
      plugins: [stateBandsPlugin],
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
          x: {
            type: 'time',
            grid: { color: this.resolveColor('var(--border-subtle)') },
            ticks: {
              color: this.resolveColor('var(--text-muted)'),
              maxRotation: 0,
              autoSkip: true,
              callback: value => this.formatTick(Number(value)),
            },
          },
        },
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              color: this.resolveColor('var(--text-muted)'),
              boxWidth: 12,
              usePointStyle: false,
              generateLabels: chart => this.generateLegendLabels(chart),
            },
            onClick: (event, legendItem, legend) => this.handleLegendClick(legendItem, legend.chart),
          },
          tooltip: {
            backgroundColor: this.resolveColor('var(--bg-panel)'),
            titleColor: this.resolveColor('var(--text-main)'),
            bodyColor: this.resolveColor('var(--text-main)'),
            borderColor: this.resolveColor('var(--border-subtle)'),
            borderWidth: 1,
            callbacks: {
              title: items => (items[0] ? this.formatTooltipTime(items[0].parsed.x ?? 0) : ''),
              label: item => this.formatTooltipLabel(item.dataset.label, item.parsed.y ?? 0),
            },
          },
        },
      },
    });

    this.applyData();
  }

  private applyData() {
    if (!this.chart) return;

    const lineSpecs = this.datasets.filter((dataset): dataset is LineDataset => dataset.type === 'line');
    const stateSpecs = this.datasets.filter((dataset): dataset is StateDataset => dataset.type === 'state');

    this.reconcileLineDatasets(lineSpecs);
    stateBandsByChart.set(this.chart, { bands: stateSpecs, hiddenKeys: this.hiddenBandKeys });

    if (this.domain) {
      const xScale = this.chart.options.scales!.x! as { min?: number; max?: number };
      xScale.min = this.domain.from;
      xScale.max = this.domain.to;
    }

    this.applyAxisBounds('y', lineSpecs);
    this.applyAxisBounds('y1', lineSpecs);

    this.chart.update();
  }

  // Updates existing Chart.js datasets in place (matched by key) instead of replacing the array, so
  // legend-toggled hidden state survives data refreshes (e.g. switching the selected range).
  private reconcileLineDatasets(specs: LineDataset[]) {
    const chart = this.chart!;
    const existingByKey = new Map(chart.data.datasets.map(dataset => [(dataset as ChartJsDataset<'line'> & { _key: string })._key, dataset]));
    const nextKeys = new Set(specs.map(spec => spec.key));

    for (const key of [...existingByKey.keys()]) {
      if (!nextKeys.has(key)) {
        const index = chart.data.datasets.findIndex(dataset => (dataset as ChartJsDataset<'line'> & { _key: string })._key === key);
        if (index >= 0) chart.data.datasets.splice(index, 1);
      }
    }

    for (const spec of specs) {
      const color = this.resolveColor(spec.color);
      const data = spec.points.map(point => ({ x: point.at, y: point.value }));
      const existing = existingByKey.get(spec.key) as (ChartJsDataset<'line'> & { _key: string }) | undefined;

      if (existing) {
        existing.data = data;
        existing.label = spec.label;
        existing.borderColor = color;
        existing.backgroundColor = color;
        existing.yAxisID = spec.axis ?? 'y';
        continue;
      }

      chart.data.datasets.push({
        _key: spec.key,
        label: spec.label,
        data,
        borderColor: color,
        backgroundColor: color,
        yAxisID: spec.axis ?? 'y',
        pointRadius: 0,
        borderWidth: 2,
        tension: 0.15,
        spanGaps: true,
        hidden: spec.hidden ?? false,
      } as ChartJsDataset<'line'> & { _key: string });
    }
  }

  private applyAxisBounds(axis: ChartAxis, specs: LineDataset[]) {
    const chart = this.chart!;
    const onAxis = specs.filter(spec => (spec.axis ?? 'y') === axis);

    if (!onAxis.length) {
      if (chart.options.scales![axis]) delete chart.options.scales![axis];
      return;
    }

    // Always build a fresh plain object rather than reading back chart.options.scales![axis]: once chart.js
    // has processed a scale config it wraps it in internal resolver machinery, and re-mutating that wrapped
    // object on the next update corrupts it (surfaced as "Ignoring resolver passed as options for scale").
    const scale: { type: 'linear'; position: 'left' | 'right'; min?: number; max?: number; grid: unknown; ticks: unknown } = {
      type: 'linear',
      position: axis === 'y1' ? 'right' : 'left',
      min: onAxis.find(spec => spec.min !== undefined)?.min,
      max: onAxis.find(spec => spec.max !== undefined)?.max,
      grid: { display: axis === 'y', color: this.resolveColor('var(--border-subtle)') },
      ticks: { color: this.resolveColor('var(--text-muted)'), callback: (value: unknown) => this.formatAxisValue(Number(value), onAxis) },
    };

    chart.options.scales![axis] = scale as ScaleOptionsByType<'linear'>;
  }

  private generateLegendLabels(chart: Chart): LegendItem[] {
    const lineLabels = (Chart.defaults.plugins.legend.labels.generateLabels as (chart: Chart) => LegendItem[])(chart);
    const stateSpecs = this.datasets.filter((dataset): dataset is StateDataset => dataset.type === 'state');

    const bandLabels: LegendItem[] = stateSpecs.map((spec, index) => ({
      text: spec.label ?? 'State',
      fillStyle: this.resolveColor(spec.color),
      strokeStyle: this.resolveColor(spec.color),
      fontColor: this.resolveColor('var(--text-muted)'),
      hidden: this.hiddenBandKeys.has(spec.key),
      datasetIndex: BAND_LEGEND_INDEX_OFFSET + index,
    }));

    return [...lineLabels, ...bandLabels];
  }

  private handleLegendClick(legendItem: LegendItem, chart: Chart) {
    const datasetIndex = legendItem.datasetIndex;
    if (datasetIndex === undefined) return;

    if (datasetIndex >= BAND_LEGEND_INDEX_OFFSET) {
      const stateSpecs = this.datasets.filter((dataset): dataset is StateDataset => dataset.type === 'state');
      const spec = stateSpecs[datasetIndex - BAND_LEGEND_INDEX_OFFSET];
      if (!spec) return;

      if (this.hiddenBandKeys.has(spec.key)) this.hiddenBandKeys.delete(spec.key);
      else this.hiddenBandKeys.add(spec.key);

      chart.update();
      return;
    }

    const meta = chart.getDatasetMeta(datasetIndex);
    meta.hidden = meta.hidden === null ? !chart.data.datasets[datasetIndex]?.hidden : !meta.hidden;
    chart.update();
  }

  private resolveColor(color: string): string {
    const match = /^var\((--[a-z0-9-]+)\)$/i.exec(color.trim());
    if (!match?.[1]) return color;

    return getComputedStyle(this).getPropertyValue(match[1]).trim() || color;
  }

  private formatAxisValue(value: number, onAxis: LineDataset[]): string {
    const formatter = onAxis[0]?.valueFormatter;
    if (formatter) return formatter(value);

    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value);
  }

  private formatTooltipLabel(label: string | undefined, value: number): string {
    const spec = this.datasets.find((dataset): dataset is LineDataset => dataset.type === 'line' && dataset.label === label);
    const formatted = spec?.valueFormatter
      ? spec.valueFormatter(value)
      : new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value);
    return `${label ?? 'Value'}: ${formatted}`;
  }

  private formatTick(value: number): string {
    const rangeMs = this.domain ? this.domain.to - this.domain.from : 0;
    const showTimeOnly = rangeMs > 0 && rangeMs <= 26 * 60 * 60 * 1000;

    return new Intl.DateTimeFormat(
      undefined,
      showTimeOnly ? { hour: 'numeric', minute: '2-digit', hour12: this.preferences.timeFormat === 'Hour12' } : { month: 'short', day: 'numeric' },
    ).format(value);
  }

  private formatTooltipTime(value: number): string {
    return new Intl.DateTimeFormat(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: this.preferences.timeFormat === 'Hour12',
    }).format(value);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'app-kelvin-chart': KelvinChart;
  }
}
