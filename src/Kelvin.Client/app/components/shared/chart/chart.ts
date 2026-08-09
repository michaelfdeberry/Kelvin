import { consume } from '@lit/context';
import { LitElement, TemplateResult, html, svg } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';

import chartStyles from './chart.styles.js';
import { preferencesContext } from '../../../contexts/preferences-context.js';
import { Preferences } from '../../../models/preferences.js';
import sharedStyles from '../../../shared.styles.js';

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

type HoveredLine = {
  dataset: Extract<ChartDataset, { type: 'line' }>;
  point?: ChartPoint;
};

export type ChartDataset =
  | {
      type: 'state';
      intervals: ChartInterval[];
      color: string;
      label?: string;
    }
  | {
      type: 'line';
      points: ChartPoint[];
      color: string;
      min?: number;
      max?: number;
      label?: string;
      valueFormatter?: (value: number) => string;
    };

@customElement('app-kelvin-chart')
export class KelvinChart extends LitElement {
  static override styles = [sharedStyles, chartStyles];

  private static readonly plot = { left: 56, right: 984, top: 8, bottom: 82 };

  @consume({ context: preferencesContext, subscribe: true })
  private preferences!: Preferences;

  @property({ type: Array })
  datasets: ChartDataset[] = [];

  @property({ attribute: false })
  domain?: ChartDomain;

  @state()
  private hoveredLines: HoveredLine[] = [];

  @state()
  private hoveredX?: number;

  @state()
  private hoveredAt?: number;

  override render(): TemplateResult {
    const lineScale = this.domain ? this.getLineScale(this.domain) : undefined;

    return html`
      <svg
        viewBox="0 0 1000 100"
        preserveAspectRatio="none"
        @pointermove=${this.handlePointerMove}
        @pointerleave=${this.handlePointerLeave}
      >
        <rect
          class="chart-surface"
          x="0"
          y="0"
          width="1000"
          height="100"
        />
        ${this.domain ? this.datasets.filter(dataset => dataset.type === 'state').map(dataset => this.renderState(dataset, this.domain!)) : ''}
        ${this.domain ? this.renderGrid(this.domain, lineScale) : ''}
        ${this.domain && lineScale ? this.datasets.filter(dataset => dataset.type === 'line').map(dataset => this.renderLine(dataset, this.domain!, lineScale)) : ''}
        ${this.renderTooltip()}
      </svg>
    `;
  }

  // Renders binary data (e.g., HVAC On/Off) as shaded background bars
  private renderState(dataset: Extract<ChartDataset, { type: 'state' }>, domain: ChartDomain) {
    const { intervals, color } = dataset;
    if (!intervals.length) return '';

    return svg`
      <g fill="${color}" fill-opacity="0.24">
        ${intervals.map(interval => {
          const from = Math.max(interval.from, domain.from);
          const to = Math.min(interval.to, domain.to);
          if (to <= from) return '';

          const x = this.toX(from, domain);
          const width = this.toX(to, domain) - x;
          return svg`<rect x="${x}" y="0" width="${width}" height="100" />`;
        })}
      </g>
    `;
  }

  // Renders continuous data (e.g., Temp, Humidity) as a line graph
  private renderLine(dataset: Extract<ChartDataset, { type: 'line' }>, domain: ChartDomain, scale: ChartDomain) {
    const { color } = dataset;
    const pointsInDomain = dataset.points.filter(
      point => Number.isFinite(point.at) && Number.isFinite(point.value) && point.at >= domain.from && point.at <= domain.to,
    );
    if (pointsInDomain.length < 2) return '';

    // Map the raw data to X,Y SVG coordinates
    const points = pointsInDomain
      .map(point => {
        const x = this.toX(point.at, domain);
        // SVG Y-axis is inverted (0 is top, 100 is bottom), so we subtract from 100
        const y = this.toY(point.value, scale);
        return `${x},${y}`;
      })
      .join(' ');

    return svg`
      <polyline 
        points="${points}" 
        fill="none" 
        stroke="${color}" 
        stroke-width="2" 
        /* Prevents the line from getting fat when the SVG stretches horizontally */
        vector-effect="non-scaling-stroke" 
      />
    `;
  }

  private toX(at: number, domain: ChartDomain) {
    const { left, right } = KelvinChart.plot;
    return left + ((at - domain.from) / (domain.to - domain.from)) * (right - left);
  }

  private toY(value: number, scale: ChartDomain) {
    const { top, bottom } = KelvinChart.plot;
    return bottom - ((value - scale.from) / (scale.to - scale.from)) * (bottom - top);
  }

  private getLineScale(domain: ChartDomain): ChartDomain | undefined {
    const lineDatasets = this.datasets.filter(dataset => dataset.type === 'line');
    const values = lineDatasets.flatMap(dataset =>
      dataset.points
        .filter(point => Number.isFinite(point.at) && Number.isFinite(point.value) && point.at >= domain.from && point.at <= domain.to)
        .map(point => point.value),
    );

    if (!values.length) return undefined;

    const min = Math.min(...lineDatasets.map(dataset => dataset.min ?? Math.min(...values)));
    const max = Math.max(...lineDatasets.map(dataset => dataset.max ?? Math.max(...values)));
    const range = max - min || Math.max(Math.abs(max), 1);
    const padding = range * 0.1;

    return { from: min - padding, to: max + padding };
  }

  private renderGrid(domain: ChartDomain, scale?: ChartDomain) {
    const { left, right, top, bottom } = KelvinChart.plot;
    const horizontalLines = [0.25, 0.5, 0.75].map(fraction => top + (bottom - top) * fraction);
    const verticalLines = [0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875].map(fraction => left + (right - left) * fraction);

    return svg`
      <g class="chart-grid" vector-effect="non-scaling-stroke">
        ${horizontalLines.map(y => svg`<line x1="${left}" y1="${y}" x2="${right}" y2="${y}" />`)}
        ${verticalLines.map(x => svg`<line x1="${x}" y1="${top}" x2="${x}" y2="${bottom}" />`)}
      </g>
      ${scale ? this.renderValueLabels(scale) : ''}
      ${this.renderTimeLabels(domain)}
    `;
  }

  private renderValueLabels(scale: ChartDomain) {
    const { left, top, bottom } = KelvinChart.plot;
    const values = [scale.to, (scale.from + scale.to) / 2, scale.from];
    const positions = [top, (top + bottom) / 2, bottom];

    return svg`
      <g class="chart-axis-labels chart-axis-labels--values">
        ${values.map((value, index) => svg`<text x="${left - 8}" y="${positions[index]}" text-anchor="end" dominant-baseline="middle">${this.formatValue(value, this.getAxisValueFormatter())}</text>`)}
      </g>
    `;
  }

  private renderTimeLabels(domain: ChartDomain) {
    const { left, right } = KelvinChart.plot;

    return svg`
      <g class="chart-axis-labels chart-axis-labels--time">
        <text x="${left}" y="96" text-anchor="start">${this.formatTime(domain.from)}</text>
        <text x="${right}" y="96" text-anchor="end">${this.formatTime(domain.to)}</text>
      </g>
    `;
  }

  private renderTooltip() {
    if (this.hoveredX === undefined || !this.hoveredLines.length) return '';

    const { left, right, top, bottom } = KelvinChart.plot;
    const tooltipWidth = 150;
    const tooltipHeight = this.hoveredLines.length * 10 + 20;
    const x = Math.min(Math.max(this.hoveredX + 12, left), right - tooltipWidth);
    const y = Math.min(top + 4, bottom - tooltipHeight);

    return svg`
      <g class="chart-tooltip">
        <line class="chart-tooltip__guide" x1="${this.hoveredX}" y1="${top}" x2="${this.hoveredX}" y2="${bottom}" vector-effect="non-scaling-stroke" />
        <rect x="${x}" y="${y}" width="${tooltipWidth}" height="${tooltipHeight}" rx="2" vector-effect="non-scaling-stroke" />
        ${this.hoveredLines.map(
          (hoveredLine, index) => svg`
          <text x="${x + 6}" y="${y + 8 + index * 10}">
            ${hoveredLine.dataset.label ?? 'Value'}: ${hoveredLine.point ? this.formatValue(hoveredLine.point.value, hoveredLine.dataset.valueFormatter) : '--'}
          </text>
        `,
        )}
        <text class="chart-tooltip__time" x="${x + 6}" y="${y + tooltipHeight - 5}">
          ${this.hoveredAt === undefined ? '' : this.formatTooltipTime(this.hoveredAt)}
        </text>
      </g>
    `;
  }

  private handlePointerMove(event: PointerEvent) {
    if (!this.domain) return;

    const svgElement = event.currentTarget as SVGSVGElement;
    const bounds = svgElement.getBoundingClientRect();
    const relativeX = ((event.clientX - bounds.left) / bounds.width) * 1000;
    const { left, right } = KelvinChart.plot;

    if (relativeX < left || relativeX > right) {
      this.handlePointerLeave();
      return;
    }

    const at = this.domain.from + ((relativeX - left) / (right - left)) * (this.domain.to - this.domain.from);
    this.hoveredX = relativeX;
    this.hoveredAt = at;
    this.hoveredLines = this.datasets
      .filter(dataset => dataset.type === 'line')
      .map(dataset => ({ dataset, point: this.findClosestPoint(dataset.points, at) }));
  }

  private handlePointerLeave() {
    this.hoveredX = undefined;
    this.hoveredAt = undefined;
    this.hoveredLines = [];
  }

  private findClosestPoint(points: ChartPoint[], at: number) {
    const pointsInDomain = points.filter(
      point => Number.isFinite(point.at) && Number.isFinite(point.value) && this.domain && point.at >= this.domain.from && point.at <= this.domain.to,
    );

    const firstPoint = pointsInDomain.at(0);
    const lastPoint = pointsInDomain.at(-1);
    if (!firstPoint || !lastPoint || at < firstPoint.at || at > lastPoint.at) {
      return undefined;
    }

    return pointsInDomain.reduce<ChartPoint | undefined>(
      (closestPoint, point) => (!closestPoint || Math.abs(point.at - at) < Math.abs(closestPoint.at - at) ? point : closestPoint),
      undefined,
    );
  }

  private getAxisValueFormatter() {
    return this.datasets.find(dataset => dataset.type === 'line')?.valueFormatter;
  }

  private formatValue(value: number, formatter?: (value: number) => string) {
    if (formatter) return formatter(value);

    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value);
  }

  private formatTime(value: number) {
    return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' }).format(value);
  }

  private formatTooltipTime(value: number) {
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
