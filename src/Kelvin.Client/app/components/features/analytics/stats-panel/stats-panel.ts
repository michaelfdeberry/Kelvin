import { consume } from '@lit/context';
import { Task } from '@lit/task';
import { html, LitElement, TemplateResult } from 'lit';
import { customElement } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import statsPanelStyles from './stats-panel.styles.js';
import {
  callContext,
  controlContext,
  defaultCallStateChange,
  defaultControlStateChange,
  defaultLifecycleStateChange,
  defaultRelayStates,
  lifecycleContext,
  relayStatesContext,
} from '../../../../contexts/control-context.js';
import { defaultEnvironmentReadings, environmentReadingsContext } from '../../../../contexts/sensors-context.js';
import {
  defaultSchedules,
  defaultSetPoints,
  defaultThermostat,
  schedulesContext,
  setPointsContext,
  thermostatContext,
} from '../../../../contexts/thermostat-context.js';
import { loadControlStats } from '../../../../services/control-analytics.js';
import sharedStyles from '../../../../shared.styles.js';

import type { ControlStateChange } from '../../../../models/control-state-change.js';
import type { ControlStats } from '../../../../models/control-stats.js';
import type { RelayStates } from '../../../../models/gateway.js';
import type { EnvironmentReading } from '../../../../models/sensors.js';
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

  @consume({ context: setPointsContext, subscribe: true })
  setPoints: SetPoint[] = defaultSetPoints;

  @consume({ context: schedulesContext, subscribe: true })
  schedules: Schedule[] = defaultSchedules;

  @consume({ context: controlContext, subscribe: true })
  controlChange: Partial<ControlStateChange> = defaultControlStateChange;

  @consume({ context: callContext, subscribe: true })
  callChange: Partial<ControlStateChange> = defaultCallStateChange;

  @consume({ context: lifecycleContext, subscribe: true })
  lifecycleChange: Partial<ControlStateChange> = defaultLifecycleStateChange;

  @consume({ context: relayStatesContext, subscribe: true })
  relayStates: RelayStates = defaultRelayStates;

  @consume({ context: environmentReadingsContext, subscribe: true })
  environment: Partial<EnvironmentReading> = defaultEnvironmentReadings;

  // Everything else is live state; only the runtime totals, an aggregate over the last 24 hours, have to be fetched.
  private statsTask = new Task(this, {
    task: async (_, { signal }) => loadControlStats(signal),
    args: () => [this.thermostat.mode, this.thermostat.fanEnabled, this.thermostat.hysteresisC, this.getRefreshKey()],
  });

  private getRefreshKey(): string {
    const id = typeof this.controlChange.id === 'string' ? this.controlChange.id : '';
    const changedAt = typeof this.controlChange.changedAt === 'string' ? this.controlChange.changedAt : '';
    return `${id}:${changedAt}`;
  }

  private buildSections(stats?: ControlStats): StatSection[] {
    return [this.buildTelemetrySection(stats), this.buildHysteresisSection(), this.buildForecastLockoutSection(), this.buildRelaySection()];
  }

  private buildTelemetrySection(stats?: ControlStats): StatSection {
    // The live average, falling back to what the last call was made on until the first reading arrives.
    const temperatureC = this.environment.temperatureC ?? this.callChange.environmentTemperatureC;
    const serviceIsFaulted = this.lifecycleChange.state === 'Fault';

    return {
      title: 'System Telemetry',
      rows: [
        {
          label: 'Aggregate Average:',
          value: this.formatTemperature(temperatureC),
        },
        {
          label: 'Heating Runtime (24h):',
          value: stats ? this.formatDuration(stats.heatingSeconds) : '--',
        },
        {
          label: 'Cooling Runtime (24h):',
          value: stats ? this.formatDuration(stats.coolingSeconds) : '--',
        },
        {
          label: 'Gateway Control:',
          value: serviceIsFaulted ? 'Faulted' : 'Active',
          tone: serviceIsFaulted ? 'danger' : 'success',
        },
      ],
    };
  }

  private buildHysteresisSection(): StatSection {
    const hysteresisC = this.callChange.hysteresisC ?? this.thermostat.hysteresisC;
    const rows: StatRow[] = [
      {
        label: 'Dead Band:',
        value: `±${hysteresisC.toFixed(1)}°C (${this.celsiusDeltaToFahrenheit(hysteresisC).toFixed(1)}°F)`,
      },
    ];

    if (this.thermostat.mode === 'Heating' || this.thermostat.mode === 'Automatic') {
      rows.push(...this.buildThresholdRows('Heating', this.resolveSource('Heating'), hysteresisC));
    }

    if (this.thermostat.mode === 'Cooling' || this.thermostat.mode === 'Automatic') {
      rows.push(...this.buildThresholdRows('Cooling', this.resolveSource('Cooling'), hysteresisC));
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

  private buildRelaySection(): StatSection {
    return {
      title: 'Hardware Relays (GPIO)',
      rows: [
        { label: 'R1 (Cool - Y):', value: this.formatRelayState(this.relayStates.cooling === true) },
        { label: 'R2 (Heat - W):', value: this.formatRelayState(this.relayStates.heating === true) },
        { label: 'R3 (Fan - G):', value: this.formatRelayState(this.relayStates.fan === true) },
        { label: 'R4 (Gateway Control):', value: this.formatRelayState(this.relayStates.control === true, 'ARMED', 'REVERTED') },
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

    const hysteresisOffset = hysteresisC / 2;
    if (type === 'Heating') {
      const heatingTrigger = source.targetTemperatureC - hysteresisOffset;
      const heatingSatisfied = source.targetTemperatureC + hysteresisOffset;
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

    const coolingTrigger = source.targetTemperatureC + hysteresisOffset;
    const coolingSatisfied = source.targetTemperatureC - hysteresisOffset;
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

  private resolveSource(type: 'Heating' | 'Cooling'): HysteresisSource | undefined {
    const matchedSchedule = this.callChange.scheduleId
      ? this.schedules.find(schedule => schedule.id === this.callChange.scheduleId && schedule.type === type)
      : undefined;

    if (matchedSchedule) {
      return matchedSchedule;
    }

    const matchedSetPoint = this.callChange.setPointId
      ? this.setPoints.find(setPoint => setPoint.id === this.callChange.setPointId && setPoint.type === type)
      : undefined;

    if (matchedSetPoint) {
      return matchedSetPoint;
    }

    return this.findActiveSchedule(this.schedules, type) ?? this.setPoints.find(setPoint => setPoint.type === type);
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
    // The task keeps its last result while it refreshes, and apiGet already reports a failed load, so the runtime rows just show '--'.
    return this.renderSections(this.buildSections(this.statsTask.value));
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-stats-panel': StatsPanel;
  }
}
