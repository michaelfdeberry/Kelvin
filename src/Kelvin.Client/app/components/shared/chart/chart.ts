import { LitElement, TemplateResult, html, svg } from 'lit';
import { property } from 'lit/decorators.js';

import chartStyles from './chart.styles.js';
import sharedStyles from '../../../shared.styles.js';

export type ChartDataset = {
  type: 'state' | 'line';
  values: number[];
  color: string;
  min?: number;
  max?: number;
};

export class KelvinChart extends LitElement {
  static override styles = [sharedStyles, chartStyles];

  @property({ type: Array })
  datasets: Array<ChartDataset> = [];

  constructor() {
    super();
    this.datasets = [];
  }

  override render(): TemplateResult {
    return html`
      <!-- preserveAspectRatio="none" lets the chart stretch responsively -->
      <svg
        viewBox="0 0 1000 100"
        preserveAspectRatio="none"
      >
        ${this.datasets.map(dataset => {
          if (dataset.type === 'state') return this._renderState(dataset);
          if (dataset.type === 'line') return this._renderLine(dataset);
          return '';
        })}
      </svg>
    `;
  }

  // Renders binary data (e.g., HVAC On/Off) as shaded background bars
  _renderState(dataset: ChartDataset) {
    const { values, color } = dataset;
    if (!values || values.length < 2) return '';

    const stepX = 1000 / (values.length - 1);

    return svg`
      <g fill="${color}">
        ${values.map((val, i) => {
          if (!val) return '';
          return svg`<rect x="${i * stepX}" y="0" width="${stepX}" height="100" />`;
        })}
      </g>
    `;
  }

  // Renders continuous data (e.g., Temp, Humidity) as a line graph
  _renderLine(dataset: ChartDataset) {
    const { values, color, min = 0, max = 100 } = dataset;
    if (!values || values.length < 2) return '';

    const range = max - min;
    const stepX = 1000 / (values.length - 1);

    // Map the raw data to X,Y SVG coordinates
    const points = values
      .map((val, i) => {
        const x = i * stepX;
        // SVG Y-axis is inverted (0 is top, 100 is bottom), so we subtract from 100
        const y = 100 - ((val - min) / range) * 100;
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
}

customElements.define('kelvin-chart', KelvinChart);
