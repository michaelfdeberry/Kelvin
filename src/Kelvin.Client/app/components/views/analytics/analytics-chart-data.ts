import type { ControlState, ControlStateChange } from '../../../models/control-state-change.js';
import type { ChartInterval, ChartPoint } from '../../shared/chart/chart.js';

export type AnalyticsDomain = {
  from: number;
  to: number;
};

type MeasurementKey = 'environmentTemperatureC' | 'humidityPercentage' | 'targetTemperatureC';

function toTimestamp(changedAt: string): number | undefined {
  const timestamp = Date.parse(changedAt);
  return Number.isFinite(timestamp) ? timestamp : undefined;
}

export function toMeasurementPoints(changes: ControlStateChange[], key: MeasurementKey): ChartPoint[] {
  const pointsByTimestamp = new Map<number, ChartPoint>();

  for (const change of changes) {
    const at = toTimestamp(change.changedAt);
    const value = change[key];
    if (at === undefined || value === undefined || !Number.isFinite(value)) {
      continue;
    }

    pointsByTimestamp.set(at, { at, value });
  }

  return [...pointsByTimestamp.values()].sort((first, second) => first.at - second.at);
}

export function toStateIntervals(changes: ControlStateChange[], activeStates: ReadonlySet<ControlState>, domain: AnalyticsDomain): ChartInterval[] {
  if (domain.to <= domain.from) {
    return [];
  }

  let state = changes[0]?.previousState;
  let stateStartedAt = domain.from;
  const intervals: ChartInterval[] = [];

  for (const change of changes) {
    const changedAt = toTimestamp(change.changedAt);
    if (changedAt === undefined || changedAt < domain.from || changedAt > domain.to) {
      continue;
    }

    if (state !== undefined && activeStates.has(state) && changedAt > stateStartedAt) {
      intervals.push({ from: stateStartedAt, to: changedAt });
    }

    state = change.state;
    stateStartedAt = changedAt;
  }

  if (state !== undefined && activeStates.has(state) && domain.to > stateStartedAt) {
    intervals.push({ from: stateStartedAt, to: domain.to });
  }

  return intervals;
}
