import { consume } from '@lit/context';
import { html, LitElement, nothing } from 'lit';
import { customElement, state } from 'lit/decorators.js';

import { extendToDomainEnd, toCrossSensorAverage, toMeasurementPoints, toSensorSeries, toStateIntervals, withSeed } from './analytics-chart-data.js';
import analyticsViewStyles from './analytics-view.styles.js';
import '../../shared/chart/chart.js';
import { preferencesContext } from '../../../contexts/preferences-context.js';
import { sensorsContext } from '../../../contexts/sensors-context.js';
import { Preferences } from '../../../models/preferences.js';
import { loadControlHistory, loadLatestControlChangeBefore } from '../../../services/control-analytics.js';
import { loadLatestSensorReadingsBefore, loadSensorHistoryPeriods } from '../../../services/sensor-analytics.js';
import { getPreferredUnit, presentAsPreferredUnit } from '../../../services/utilities.js';
import sharedStyles from '../../../shared.styles.js';

import type { ControlStateChange } from '../../../models/control-state-change.js';
import type { Sensor, SensorPacketHistoryEntry } from '../../../models/sensors.js';
import type { ChartDataset, ChartDomain } from '../../shared/chart/chart.js';

type RangePreset = '24h' | '7d' | '30d';
type LoadStatus = 'loading' | 'ready' | 'error';

const rangeDurations: Record<RangePreset, number> = {
  '24h': 24 * 60 * 60 * 1000,
  '7d': 7 * 24 * 60 * 60 * 1000,
  '30d': 30 * 24 * 60 * 60 * 1000,
};

// Cycles through distinct tokens for per-sensor overlay lines, so they read as clearly different from the
// indoor/target temperature lines (--accent-primary/--accent-success) as well as from each other.
const sensorLineColors = [
  'var(--accent-info)',
  'var(--accent-danger)',
  'var(--accent-heat)',
  'var(--accent-cool)',
  'var(--accent-idle)',
  'var(--accent-primary-strong)',
];

const emptyDomain: ChartDomain = { from: 0, to: 1 };

@customElement('app-analytics-view')
export class AnalyticsView extends LitElement {
  static override styles = [sharedStyles, analyticsViewStyles];

  @consume({ context: preferencesContext, subscribe: true })
  private preferences!: Preferences;

  @consume({ context: sensorsContext, subscribe: true })
  private sensors!: Sensor[];

  @state()
  private rangePreset: RangePreset = '24h';

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
  private airQualityData: ChartDataset[] = [];

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
                  'Heating and cooling activity is shown behind the indoor temperature. Per-sensor readings are hidden by default - enable them from the legend.',
                  this.temperatureData,
                  this.temperatureData.some(
                    dataset => (dataset.type === 'line' && dataset.points.length > 0) || (dataset.type === 'state' && dataset.intervals.length > 0),
                  ),
                )}
                ${this.renderChart(
                  'Air quality',
                  'Indoor humidity and CO2 levels recorded by the sensors.',
                  this.airQualityData,
                  this.airQualityData.some(dataset => dataset.type === 'line' && dataset.points.length > 0),
                )}
                ${this.renderChart(
                  'Control ownership',
                  'Intervals where Kelvin had control of the equipment.',
                  this.controlData,
                  this.controlData.some(dataset => dataset.type === 'state' && dataset.intervals.length > 0),
                )}
                ${this.renderChart(
                  'Fan runtime',
                  'Intervals where the circulation fan was on.',
                  this.fanData,
                  this.fanData.some(dataset => dataset.type === 'state' && dataset.intervals.length > 0),
                )}
              `
            : nothing
        }
      </section>
    `;
  }

  private renderChart(heading: string, description: string, datasets: ChartDataset[], hasData: boolean) {
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
    const signal = abortController.signal;

    const to = new Date();
    const from = new Date(to.getTime() - rangeDurations[this.rangePreset]);
    const domain: ChartDomain = { from: from.getTime(), to: to.getTime() };
    this.domain = domain;
    this.status = 'loading';

    try {
      const [callChanges, fanChanges, controlChanges, callSeed, fanSeed, controlSeed, sensorPeriods, sensorSeeds] = await Promise.all([
        loadControlHistory({ from, to, kind: 'Call' }, signal),
        loadControlHistory({ from, to, kind: 'Fan' }, signal),
        loadControlHistory({ from, to, kind: 'Control' }, signal),
        loadLatestControlChangeBefore('Call', from, signal),
        loadLatestControlChangeBefore('Fan', from, signal),
        loadLatestControlChangeBefore('Control', from, signal),
        loadSensorHistoryPeriods({ from, to }, signal),
        loadLatestSensorReadingsBefore(from, signal),
      ]);

      if (signal.aborted) {
        return;
      }

      this.setChartData(
        withSeed(callChanges, callSeed, domain),
        withSeed(fanChanges, fanSeed, domain),
        withSeed(controlChanges, controlSeed, domain),
        sensorPeriods,
        sensorSeeds,
        domain,
      );
      this.status = 'ready';
    } catch {
      if (signal.aborted) {
        return;
      }

      this.status = 'error';
    }
  }

  private setChartData(
    callChanges: ControlStateChange[],
    fanChanges: ControlStateChange[],
    controlChanges: ControlStateChange[],
    sensorPeriods: SensorPacketHistoryEntry[],
    sensorSeeds: SensorPacketHistoryEntry[],
    domain: ChartDomain,
  ) {
    const measurementChanges = [...callChanges, ...fanChanges, ...controlChanges].sort(
      (first, second) => Date.parse(first.changedAt) - Date.parse(second.changedAt),
    );

    const temperatureUnitFormatter = (value: number) =>
      `${presentAsPreferredUnit(this.preferences.temperatureUnit, value)} ${getPreferredUnit(this.preferences.temperatureUnit)}`;

    this.temperatureData = [
      {
        type: 'state',
        key: 'heating-band',
        intervals: toStateIntervals(callChanges, new Set(['Heating']), domain),
        color: 'var(--accent-heat)',
        label: 'Heating',
      },
      {
        type: 'state',
        key: 'cooling-band',
        intervals: toStateIntervals(callChanges, new Set(['Cooling']), domain),
        color: 'var(--accent-cool)',
        label: 'Cooling',
      },
      {
        type: 'line',
        key: 'indoor-temperature',
        points: extendToDomainEnd(toMeasurementPoints(measurementChanges, 'environmentTemperatureC'), domain),
        color: 'var(--accent-primary)',
        label: 'Indoor temperature',
        valueFormatter: temperatureUnitFormatter,
      },
      {
        type: 'line',
        key: 'target-temperature',
        points: extendToDomainEnd(toMeasurementPoints(measurementChanges, 'targetTemperatureC'), domain),
        color: 'var(--accent-success)',
        label: 'Target temperature',
        valueFormatter: temperatureUnitFormatter,
      },
      ...this.buildSensorTemperatureDatasets(sensorPeriods, sensorSeeds, domain, temperatureUnitFormatter),
    ];

    this.fanData = [
      {
        type: 'state',
        key: 'fan-on-band',
        intervals: toStateIntervals(fanChanges, new Set(['FanOn']), domain),
        color: 'var(--accent-info)',
        label: 'Fan on',
      },
    ];

    this.controlData = [
      {
        type: 'state',
        key: 'control-enabled-band',
        intervals: toStateIntervals(controlChanges, new Set(['Enable']), domain),
        color: 'var(--accent-success)',
        label: 'Kelvin control',
      },
    ];

    this.airQualityData = [
      {
        type: 'line',
        key: 'humidity',
        points: extendToDomainEnd(toCrossSensorAverage(sensorPeriods, sensorSeeds, 'humidityPercentage', domain), domain),
        color: 'var(--accent-info)',
        axis: 'y',
        min: 0,
        max: 100,
        label: 'Humidity',
        valueFormatter: value => `${value.toFixed(1)}%`,
      },
      {
        type: 'line',
        key: 'co2',
        points: extendToDomainEnd(toCrossSensorAverage(sensorPeriods, sensorSeeds, 'cO2LevelPpm', domain), domain),
        color: 'var(--accent-danger)',
        axis: 'y1',
        label: 'CO2',
        valueFormatter: value => `${Math.round(value)} ppm`,
      },
    ];
  }

  private buildSensorTemperatureDatasets(
    sensorPeriods: SensorPacketHistoryEntry[],
    sensorSeeds: SensorPacketHistoryEntry[],
    domain: ChartDomain,
    valueFormatter: (value: number) => string,
  ): ChartDataset[] {
    const seriesBySensor = toSensorSeries(sensorPeriods, sensorSeeds, 'temperatureC', domain);
    const historicalNames = this.buildHistoricalSensorNames(sensorPeriods, sensorSeeds);
    const sensorIds = [...seriesBySensor.keys()].sort((first, second) =>
      this.getSensorName(first, historicalNames).localeCompare(this.getSensorName(second, historicalNames)),
    );

    return sensorIds.map((sensorId, index) => ({
      type: 'line',
      key: `sensor-temperature-${sensorId}`,
      points: extendToDomainEnd(seriesBySensor.get(sensorId)!, domain),
      color: sensorLineColors[index % sensorLineColors.length] ?? 'var(--accent-info)',
      hidden: true,
      label: this.getSensorName(sensorId, historicalNames),
      valueFormatter,
    }));
  }

  // The sensors context only tracks currently active sensors, so a sensor removed since a history/seed
  // entry was recorded would otherwise fall back to a generic, indistinguishable label - the name the
  // server attached to that entry is the only place it still exists.
  private buildHistoricalSensorNames(sensorPeriods: SensorPacketHistoryEntry[], sensorSeeds: SensorPacketHistoryEntry[]): Map<string, string> {
    const names = new Map<string, string>();
    for (const entry of [...sensorSeeds, ...sensorPeriods]) {
      if (entry.sensorId && entry.sensorName && !names.has(entry.sensorId)) {
        names.set(entry.sensorId, entry.sensorName);
      }
    }

    return names;
  }

  private getSensorName(sensorId: string, historicalNames: Map<string, string>): string {
    return this.sensors?.find(sensor => sensor.id === sensorId)?.name || historicalNames.get(sensorId) || 'Sensor';
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-analytics-view': AnalyticsView;
  }
}
