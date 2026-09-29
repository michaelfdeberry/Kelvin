import type { ControlState, ControlStateChange } from '../../../models/control-state-change.js';
import type { SensorPacketHistoryEntry } from '../../../models/sensors.js';
import type { ChartInterval, ChartPoint } from '../../shared/chart/chart.js';

export type AnalyticsDomain = {
  from: number;
  to: number;
};

type MeasurementKey = 'environmentTemperatureC' | 'humidityPercentage' | 'targetTemperatureC';
type SensorMeasurementKey = 'temperatureC' | 'humidityPercentage' | 'cO2LevelPpm';

function toTimestamp(changedAt: string): number | undefined {
  const timestamp = Date.parse(changedAt);
  return Number.isFinite(timestamp) ? timestamp : undefined;
}

// Prepends a synthetic change carrying the state that was already active when the range started, so a range
// with no changes in it (e.g. the last 24 hours with no control activity) still reflects the last real one
// instead of rendering as empty.
export function withSeed(changes: ControlStateChange[], seed: ControlStateChange | undefined, domain: AnalyticsDomain): ControlStateChange[] {
  if (!seed) return changes;

  const seededChange: ControlStateChange = { ...seed, changedAt: new Date(domain.from).toISOString(), previousState: seed.state };
  return [seededChange, ...changes];
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

// Extends a series' last point to the end of the domain, so a value that hasn't changed recently still
// draws as a flat continuation to "now" instead of the line simply stopping partway through the chart.
export function extendToDomainEnd(points: ChartPoint[], domain: AnalyticsDomain): ChartPoint[] {
  const last = points.at(-1);
  if (!last || last.at >= domain.to) return points;

  return [...points, { at: domain.to, value: last.value }];
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

function toSensorTimestamp(timestamp: string): number | undefined {
  const at = Date.parse(timestamp);
  return Number.isFinite(at) ? at : undefined;
}

// Groups server-aggregated rows per sensor, seeding each sensor's series with its last reading before the
// range so a sensor with no new packets in the visible window still shows a flat continuation instead of a
// gap. Periods are already averaged server-side, so no further aggregation happens here.
export function toSensorSeries(
  entries: SensorPacketHistoryEntry[],
  seeds: SensorPacketHistoryEntry[],
  key: SensorMeasurementKey,
  domain: AnalyticsDomain,
): Map<string, ChartPoint[]> {
  const bySensor = new Map<string, ChartPoint[]>();

  const addPoint = (sensorId: string | undefined, at: number, value: number) => {
    if (!sensorId || !Number.isFinite(value)) return;

    const points = bySensor.get(sensorId) ?? [];
    points.push({ at, value });
    bySensor.set(sensorId, points);
  };

  for (const seed of seeds) {
    addPoint(seed.sensorId, domain.from, seed[key]);
  }

  for (const entry of entries) {
    const at = toSensorTimestamp(entry.timestamp);
    if (at === undefined || at < domain.from || at > domain.to) continue;

    addPoint(entry.sensorId, at, entry[key]);
  }

  for (const points of bySensor.values()) {
    points.sort((first, second) => first.at - second.at);
  }

  return bySensor;
}

// Averages server-aggregated rows across all sensors that share a period, weighted by each row's sample
// count, into a single line - e.g. combining every sensor's humidity/CO2 into one Air Quality series.
export function toCrossSensorAverage(
  entries: SensorPacketHistoryEntry[],
  seeds: SensorPacketHistoryEntry[],
  key: SensorMeasurementKey,
  domain: AnalyticsDomain,
): ChartPoint[] {
  const sums = new Map<number, { total: number; weight: number }>();

  const addValue = (at: number, value: number, weight: number) => {
    if (!Number.isFinite(at) || !Number.isFinite(value) || weight <= 0) return;

    const sum = sums.get(at) ?? { total: 0, weight: 0 };
    sum.total += value * weight;
    sum.weight += weight;
    sums.set(at, sum);
  };

  for (const seed of seeds) {
    addValue(domain.from, seed[key], seed.sampleCount);
  }

  for (const entry of entries) {
    const at = toSensorTimestamp(entry.timestamp);
    if (at === undefined || at < domain.from || at > domain.to) continue;

    addValue(at, entry[key], entry.sampleCount);
  }

  return [...sums.entries()].map(([at, { total, weight }]) => ({ at, value: total / weight })).sort((first, second) => first.at - second.at);
}
