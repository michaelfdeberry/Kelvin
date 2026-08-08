import { consume } from '@lit/context';
import { Task } from '@lit/task';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import statsPanelStyles from './stats-panel.styles.js';
import { defaultControlStateChange, controlContext } from '../../../../contexts/control-context.js';
import { defaultThermostat, thermostatContext } from '../../../../contexts/thermostat-context.js';
import { loadControlAnalyticsData, type ControlAnalyticsData } from '../../../../services/control-analytics.js';
import sharedStyles from '../../../../shared.styles.js';

import type { ControlStateChange } from '../../../../models/control-state-change.js';
import type { Schedule, SetPoint, Thermostat } from '../../../../models/thermostat.js';

type HysteresisSource = Schedule | SetPoint;

type StatRow = {
  label: string;
  value: string;
  tone?: 'success' | 'danger';
};

type StatSection = {
  title: string;
  rows: StatRow[];
};

@customElement('app-stats-panel')
export class StatsPanel extends LitElement {
  static override styles = [sharedStyles, statsPanelStyles];

  @consume({ context: thermostatContext, subscribe: true })
  thermostat: Thermostat = defaultThermostat;

  @consume({ context: controlContext, subscribe: true })
  controlChange: Partial<ControlStateChange> = defaultControlStateChange;

  private analyticsTask = new Task(this, {
    task: async (_, { signal }) => loadControlAnalyticsData(signal),
    args: () => [this.thermostat.mode, this.thermostat.fanEnabled, this.thermostat.hysteresisC, this.getRefreshKey()],
  });

  private getRefreshKey(): string {
    const id = typeof this.controlChange.id === 'string' ? this.controlChange.id : '';
    const changedAt = typeof this.controlChange.changedAt === 'string' ? this.controlChange.changedAt : '';
    return `${id}:${changedAt}`;
  }

  private buildSections(data: ControlAnalyticsData): StatSection[] {
    return [this.buildTelemetrySection(data), this.buildHysteresisSection(data), this.buildForecastLockoutSection(), this.buildRelaySection(data)];
  }

  private buildTelemetrySection(data: ControlAnalyticsData): StatSection {
    const context = data.latestCallChange ?? data.controlState.lastChange ?? undefined;
    const serviceIsFaulted = data.latestLifecycleChange?.state === 'Fault';

    return {
      title: 'System Telemetry',
      rows: [
        {
          label: 'Aggregate Average:',
          value: this.formatTemperature(context?.environmentTemperatureC),
        },
        {
          label: 'Heating Runtime (24h):',
          value: this.formatDuration(data.controlStats.heatingSeconds),
        },
        {
          label: 'Cooling Runtime (24h):',
          value: this.formatDuration(data.controlStats.coolingSeconds),
        },
        {
          label: 'Gateway Control:',
          value: serviceIsFaulted ? 'Faulted' : 'Active',
          tone: serviceIsFaulted ? 'danger' : 'success',
        },
      ],
    };
  }

  private buildHysteresisSection(data: ControlAnalyticsData): StatSection {
    const hysteresisC = data.latestCallChange?.hysteresisC ?? this.thermostat.hysteresisC;
    const rows: StatRow[] = [
      {
        label: 'Dead Band:',
        value: `±${hysteresisC.toFixed(1)}°C (${this.celsiusDeltaToFahrenheit(hysteresisC).toFixed(1)}°F)`,
      },
    ];

    if (this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic') {
      rows.push(...this.buildThresholdRows('Heating', this.resolveSource(data, 'Heating'), hysteresisC));
    }

    if (this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic') {
      rows.push(...this.buildThresholdRows('Cooling', this.resolveSource(data, 'Cooling'), hysteresisC));
    }

    return {
      title: 'Hysteresis Logic',
      rows,
    };
  }

  private buildForecastLockoutSection(): StatSection {
    const rows: StatRow[] = [];

    if (this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic') {
      rows.push({
        label: 'Heating Lockout:',
        value:
          typeof this.thermostat.heatingLockoutC === 'number'
            ? `${this.thermostat.heatingLockoutC.toFixed(1)}°C (${this.formatTemperature(this.thermostat.heatingLockoutC)})`
            : '--',
      });
    }

    if (this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic') {
      rows.push({
        label: 'Cooling Lockout:',
        value:
          typeof this.thermostat.coolingLockoutC === 'number'
            ? `${this.thermostat.coolingLockoutC.toFixed(1)}°C (${this.formatTemperature(this.thermostat.coolingLockoutC)})`
            : '--',
      });
    }

    return {
      title: 'Forecast Lockout Configuration',
      rows,
    };
  }

  private buildRelaySection(data: ControlAnalyticsData): StatSection {
    const controlEnabled = data.controlState.controlState === 'Enable';
    const callState = data.controlState.callState;

    return {
      title: 'Hardware Relays (GPIO)',
      rows: [
        { label: 'R1 (Cool - Y):', value: this.formatRelayState(controlEnabled && callState === 'Cooling') },
        { label: 'R2 (Heat - W):', value: this.formatRelayState(controlEnabled && callState === 'Heating') },
        { label: 'R3 (Fan - G):', value: this.formatRelayState(controlEnabled && data.controlState.fanOn) },
        { label: 'R4 (Gateway Control):', value: this.formatRelayState(controlEnabled, 'ARMED', 'REVERTED') },
      ],
    };
  }

  private buildThresholdRows(type: 'Heating' | 'Cooling', source: HysteresisSource | undefined, hysteresisC: number): StatRow[] {
    if (!source) {
      return [
        { label: `${type} Trigger:`, value: '--' },
        { label: `${type} Satisfied:`, value: '--' },
      ];
    }

    if (type === 'Heating') {
      const heatingTrigger = source.targetTemperatureC - hysteresisC;
      const heatingSatisfied = source.targetTemperatureC + hysteresisC;
      return [
        {
          label: 'Heating Target:',
          value: `= ${source.targetTemperatureC.toFixed(1)}°C (${this.formatTemperature(source.targetTemperatureC)})`,
        },
        {
          label: 'Heating Trigger:',
          value: `≤ ${heatingTrigger.toFixed(1)}°C (${this.formatTemperature(heatingTrigger)})`,
        },
        {
          label: 'Heating Satisfied:',
          value: `≥ ${heatingSatisfied.toFixed(1)}°C (${this.formatTemperature(heatingSatisfied)})`,
        },
      ];
    }

    const coolingTrigger = source.targetTemperatureC + hysteresisC;
    const coolingSatisfied = source.targetTemperatureC - hysteresisC;
    return [
      {
        label: 'Cooling Target:',
        value: `= ${source.targetTemperatureC.toFixed(1)}°C (${this.formatTemperature(source.targetTemperatureC)})`,
      },
      {
        label: 'Cooling Trigger:',
        value: `≥ ${coolingTrigger.toFixed(1)}°C (${this.formatTemperature(coolingTrigger)})`,
      },
      {
        label: 'Cooling Satisfied:',
        value: `≤ ${coolingSatisfied.toFixed(1)}°C (${this.formatTemperature(coolingSatisfied)})`,
      },
    ];
  }

  private resolveSource(data: ControlAnalyticsData, type: 'Heating' | 'Cooling'): HysteresisSource | undefined {
    const matchedSchedule = data.latestCallChange?.scheduleId
      ? data.schedules.find(schedule => schedule.id === data.latestCallChange?.scheduleId && schedule.type === type)
      : undefined;

    if (matchedSchedule) {
      return matchedSchedule;
    }

    const matchedSetPoint = data.latestCallChange?.setPointId
      ? data.setPoints.find(setPoint => setPoint.id === data.latestCallChange?.setPointId && setPoint.type === type)
      : undefined;

    if (matchedSetPoint) {
      return matchedSetPoint;
    }

    return this.findActiveSchedule(data.schedules, type) ?? data.setPoints.find(setPoint => setPoint.type === type);
  }

  private findActiveSchedule(schedules: Schedule[], type: 'Heating' | 'Cooling'): Schedule | undefined {
    const now = new Date();
    const currentSeconds = now.getHours() * 3600 + now.getMinutes() * 60 + now.getSeconds();

    return schedules.find(schedule => schedule.type === type && this.isScheduleActive(schedule, currentSeconds));
  }

  private isScheduleActive(schedule: Schedule, currentSeconds: number): boolean {
    const startSeconds = this.timeToSeconds(schedule.startTime);
    const endSeconds = this.timeToSeconds(schedule.endTime);

    if (startSeconds === undefined || endSeconds === undefined) {
      return false;
    }

    if (startSeconds <= endSeconds) {
      return currentSeconds >= startSeconds && currentSeconds <= endSeconds;
    }

    return currentSeconds >= startSeconds || currentSeconds <= endSeconds;
  }

  private timeToSeconds(time: string): number | undefined {
    const [hours, minutes, seconds = '0'] = time.split(':');
    const parsedHours = Number(hours);
    const parsedMinutes = Number(minutes);
    const parsedSeconds = Number(seconds);

    if ([parsedHours, parsedMinutes, parsedSeconds].some(Number.isNaN)) {
      return undefined;
    }

    return parsedHours * 3600 + parsedMinutes * 60 + parsedSeconds;
  }

  private formatRelayState(active: boolean, activeLabel = 'ON', inactiveLabel = 'OFF'): string {
    return active ? `LOW (${activeLabel})` : `HIGH (${inactiveLabel})`;
  }

  private formatTemperature(temperatureC?: number | null): string {
    if (typeof temperatureC !== 'number') {
      return '--';
    }

    const temperatureF = temperatureC * (9 / 5) + 32;
    return `${temperatureF.toFixed(1)}°F`;
  }

  private celsiusDeltaToFahrenheit(temperatureC: number): number {
    return temperatureC * (9 / 5);
  }

  private formatDuration(totalSeconds: number): string {
    if (!Number.isFinite(totalSeconds) || totalSeconds <= 0) {
      return '0m';
    }

    const roundedSeconds = Math.round(totalSeconds);
    const hours = Math.floor(roundedSeconds / 3600);
    const minutes = Math.floor((roundedSeconds % 3600) / 60);

    if (hours === 0) {
      return `${minutes}m`;
    }

    return `${hours}h ${minutes}m`;
  }

  private renderMessage(message: string, isError = false): TemplateResult {
    return html`
      <section class="stats-panel__section">
        <h3 class="stats-panel__section-title">System Telemetry</h3>
        <p
          class="${classMap({
            'stats-panel__message': true,
            'stats-panel__message--error': isError,
          })}"
        >
          ${message}
        </p>
      </section>
    `;
  }

  private renderSections(sections: StatSection[]): TemplateResult[] {
    return sections.map(
      section => html`
        <section class="stats-panel__section">
          <h3 class="stats-panel__section-title">${section.title}</h3>
          ${section.rows.map(
            row => html`
              <div class="stats-panel__row">
                <span>${row.label}</span>
                <span class="stats-panel__value">
                  ${when(
                    !!row.tone,
                    () => html`
                      <span
                        class="stats-panel__status-dot ${row.tone === 'danger' ? 'stats-panel__status-dot--danger' : ''}"
                        aria-hidden="true"
                      >
                      </span>
                      ${row.value}
                    `,
                    () => row.value,
                  )}
                </span>
              </div>
            `,
          )}
        </section>
      `,
    );
  }

  override render() {
    return this.analyticsTask.render({
      pending: () => this.renderMessage('Loading analytics...'),
      complete: data => this.renderSections(this.buildSections(data)),
      error: error => this.renderMessage(error instanceof Error ? error.message : 'Failed to load analytics.', true),
    });
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-stats-panel': StatsPanel;
  }
}
