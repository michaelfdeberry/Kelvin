import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, state } from 'lit/decorators.js';

import { toMeasurementPoints, toStateIntervals } from './analytics-chart-data.js';
import analyticsViewStyles from './analytics-view.styles.js';
import '../../shared/chart/chart.js';
import { preferencesContext } from '../../../contexts/preferences-context.js';
import { Preferences } from '../../../models/preferences.js';
import { loadControlHistory } from '../../../services/control-analytics.js';
import { getPreferredUnit, toPreferredUnit } from '../../../services/utilities.js';
import sharedStyles from '../../../shared.styles.js';

import type { ControlStateChange } from '../../../models/control-state-change.js';
import type { ChartDataset, ChartDomain } from '../../shared/chart/chart.js';

type RangePreset = '24h' | '7d' | '30d';
type LoadStatus = 'loading' | 'ready' | 'error';

const rangeDurations: Record<RangePreset, number> = {
  '24h': 24 * 60 * 60 * 1000,
  '7d': 7 * 24 * 60 * 60 * 1000,
  '30d': 30 * 24 * 60 * 60 * 1000,
};

const emptyDomain: ChartDomain = { from: 0, to: 1 };

@customElement('app-analytics-view')
export class AnalyticsView extends LitElement {
  static override styles = [sharedStyles, analyticsViewStyles];

  @consume({ context: preferencesContext, subscribe: true })
  private preferences!: Preferences;

  @state()
  private rangePreset: RangePreset = '7d';

  @state()
  private status: LoadStatus = 'loading';

  @state()
  private domain: ChartDomain = emptyDomain;

  @state()
  private temperatureData: ChartDataset[] = [];

  @state()
  private fanData: ChartDataset[] = [];

  @state()
  private controlData: ChartDataset[] = [];

  @state()
  private humidityData: ChartDataset[] = [];

  private abortController?: AbortController;

  override connectedCallback() {
    super.connectedCallback();
    void this.loadHistory();
  }

  override disconnectedCallback() {
    this.abortController?.abort();
    super.disconnectedCallback();
  }

  override render() {
    return html`
      <section
        class="analytics-view__panel"
        aria-labelledby="analytics-title"
      >
        <header class="analytics-view__header">
          <div>
            <h1
              id="analytics-title"
              class="analytics-view__title"
            >
              Analytics
            </h1>
            <p class="analytics-view__description">Equipment activity and indoor conditions over time.</p>
          </div>

          <label class="analytics-view__range-control">
            <span>Range</span>
            <select
              class="select"
              @change=${this.handleRangeChange}
              .value=${this.rangePreset}
            >
              <option value="24h">Last 24 hours</option>
              <option value="7d">Last 7 days</option>
              <option value="30d">Last 30 days</option>
            </select>
          </label>
        </header>

        ${
          this.status === 'loading'
            ? html`<p
                class="analytics-view__status"
                role="status"
              >
                Loading history...
              </p>`
            : nothing
        }
        ${
          this.status === 'error'
            ? html`<p
                class="analytics-view__status analytics-view__status--error"
                role="alert"
              >
                Analytics data could not be loaded.
              </p>`
            : nothing
        }
        ${
          this.status === 'ready'
            ? html`
                ${this.renderChart(
                  'Temperature',
                  'Heating and cooling activity is shown behind the indoor temperature.',
                  this.temperatureData,
                  this.temperatureData.some(dataset => dataset.type === 'line' && dataset.points.length >= 2),
                  html`
                    <div
                      class="analytics-view__legend"
                      aria-label="Temperature chart legend"
                    >
                      <span><i class="analytics-view__legend-swatch analytics-view__legend-swatch--heating"></i>Heating</span>
                      <span><i class="analytics-view__legend-swatch analytics-view__legend-swatch--cooling"></i>Cooling</span>
                      <span><i class="analytics-view__legend-swatch analytics-view__legend-swatch--temperature"></i>Indoor temperature</span>
                      <span><i class="analytics-view__legend-swatch analytics-view__legend-swatch--target-temperature"></i>Target temperature</span>
                    </div>
                  `,
                )}
                ${this.renderChart(
                  'Fan runtime',
                  'Intervals where the circulation fan was on.',
                  this.fanData,
                  this.fanData.some(dataset => dataset.type === 'state' && dataset.intervals.length > 0),
                )}
                ${this.renderChart(
                  'Control ownership',
                  'Intervals where Kelvin had control of the equipment.',
                  this.controlData,
                  this.controlData.some(dataset => dataset.type === 'state' && dataset.intervals.length > 0),
                )}
                ${this.renderChart(
                  'Humidity',
                  'Indoor relative humidity recorded with control events.',
                  this.humidityData,
                  this.humidityData.some(dataset => dataset.type === 'line' && dataset.points.length >= 2),
                )}
              `
            : nothing
        }
      </section>
    `;
  }

  private renderChart(heading: string, description: string, datasets: ChartDataset[], hasData: boolean, legend?: TemplateResult) {
    return html`
      <section
        class="card analytics-view__chart"
        aria-label=${heading}
      >
        <div class="card__title analytics-view__chart-heading">
          <div>
            <h2>${heading}</h2>
            <p>${description}</p>
          </div>
          ${legend ?? nothing}
        </div>
        ${
          hasData
            ? html`<app-kelvin-chart
                .datasets=${datasets}
                .domain=${this.domain}
              ></app-kelvin-chart>`
            : html`<p class="analytics-view__empty">No data is available for this range.</p>`
        }
      </section>
    `;
  }

  private handleRangeChange(event: Event) {
    this.rangePreset = (event.target as HTMLSelectElement).value as RangePreset;
    void this.loadHistory();
  }

  private async loadHistory() {
    this.abortController?.abort();
    const abortController = new AbortController();
    this.abortController = abortController;

    const to = new Date();
    const from = new Date(to.getTime() - rangeDurations[this.rangePreset]);
    this.domain = { from: from.getTime(), to: to.getTime() };
    this.status = 'loading';

    try {
      const [callChanges, fanChanges, controlChanges] = await Promise.all([
        loadControlHistory({ from, to, kind: 'Call' }, abortController.signal),
        loadControlHistory({ from, to, kind: 'Fan' }, abortController.signal),
        loadControlHistory({ from, to, kind: 'Control' }, abortController.signal),
      ]);

      if (abortController.signal.aborted) {
        return;
      }

      this.setChartData(callChanges, fanChanges, controlChanges);
      this.status = 'ready';
    } catch {
      if (abortController.signal.aborted) {
        return;
      }

      this.status = 'error';
    }
  }

  private setChartData(callChanges: ControlStateChange[], fanChanges: ControlStateChange[], controlChanges: ControlStateChange[]) {
    const measurementChanges = [...callChanges, ...fanChanges, ...controlChanges].sort(
      (first, second) => Date.parse(first.changedAt) - Date.parse(second.changedAt),
    );

    this.temperatureData = [
      { type: 'state', intervals: toStateIntervals(callChanges, new Set(['Heating']), this.domain), color: 'var(--accent-heat)', label: 'Heating' },
      { type: 'state', intervals: toStateIntervals(callChanges, new Set(['Cooling']), this.domain), color: 'var(--accent-cool)', label: 'Cooling' },
      {
        type: 'line',
        points: toMeasurementPoints(measurementChanges, 'environmentTemperatureC'),
        color: 'var(--accent-primary)',
        label: 'Indoor temperature',
        valueFormatter: value => `${toPreferredUnit(this.preferences.temperatureUnit, value)} ${getPreferredUnit(this.preferences.temperatureUnit)}`,
      },
      {
        type: 'line',
        points: toMeasurementPoints(measurementChanges, 'targetTemperatureC'),
        color: 'var(--accent-success)',
        label: 'Target temperature',
        valueFormatter: value => `${toPreferredUnit(this.preferences.temperatureUnit, value)} ${getPreferredUnit(this.preferences.temperatureUnit)}`,
      },
    ];
    this.fanData = [
      { type: 'state', intervals: toStateIntervals(fanChanges, new Set(['FanOn']), this.domain), color: 'var(--accent-info)', label: 'Fan on' },
    ];
    this.controlData = [
      {
        type: 'state',
        intervals: toStateIntervals(controlChanges, new Set(['Enable']), this.domain),
        color: 'var(--accent-success)',
        label: 'Kelvin control',
      },
    ];
    this.humidityData = [
      {
        type: 'line',
        points: toMeasurementPoints(measurementChanges, 'humidityPercentage'),
        color: 'var(--accent-info)',
        min: 0,
        max: 100,
        label: 'Humidity',
        valueFormatter: value => `${value.toFixed(1)}%`,
      },
    ];
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-analytics-view': AnalyticsView;
  }
}
