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

function toSensorTimestamp(createdAt: string): number | undefined {
  const timestamp = Date.parse(createdAt);
  return Number.isFinite(timestamp) ? timestamp : undefined;
}

// Groups packets per sensor, seeding each sensor's series with its last reading before the range so a
// sensor with no new packets in the visible window still shows a flat continuation instead of a gap.
export function toSensorSeries(
  history: SensorPacketHistoryEntry[],
  seeds: SensorPacketHistoryEntry[],
  key: SensorMeasurementKey,
  domain: AnalyticsDomain,
): Map<string, ChartPoint[]> {
  const bySensor = new Map<string, ChartPoint[]>();

  const addPoint = (entry: SensorPacketHistoryEntry, at: number) => {
    if (!entry.sensorId) return;
    const value = entry[key];
    if (!Number.isFinite(value)) return;

    const points = bySensor.get(entry.sensorId) ?? [];
    points.push({ at, value });
    bySensor.set(entry.sensorId, points);
  };

  for (const seed of seeds) {
    addPoint(seed, domain.from);
  }

  for (const entry of history) {
    const at = toSensorTimestamp(entry.createdAt);
    if (at === undefined || at < domain.from || at > domain.to) continue;

    addPoint(entry, at);
  }

  for (const points of bySensor.values()) {
    points.sort((first, second) => first.at - second.at);
  }

  return bySensor;
}

const hour = 60 * 60 * 1000;

function getBucketSizeMs(rangeMs: number): number {
  if (rangeMs <= 24 * hour) return 5 * 60 * 1000;
  if (rangeMs <= 7 * 24 * hour) return 30 * 60 * 1000;
  return 2 * hour;
}

// Buckets packets across all sensors into fixed-width windows and averages them, smoothing independently
// timed per-sensor readings into a single line instead of a jagged stitch of whichever sensor reported last.
export function toBucketedSensorAverage(
  history: SensorPacketHistoryEntry[],
  seeds: SensorPacketHistoryEntry[],
  key: SensorMeasurementKey,
  domain: AnalyticsDomain,
): ChartPoint[] {
  if (domain.to <= domain.from) return [];

  const bucketMs = getBucketSizeMs(domain.to - domain.from);
  const sums = new Map<number, { total: number; count: number }>();

  const addEntry = (entry: SensorPacketHistoryEntry, at: number) => {
    const value = entry[key];
    if (!Number.isFinite(value)) return;

    const bucket = domain.from + Math.floor((at - domain.from) / bucketMs) * bucketMs;
    const bucketSum = sums.get(bucket) ?? { total: 0, count: 0 };
    bucketSum.total += value;
    bucketSum.count += 1;
    sums.set(bucket, bucketSum);
  };

  for (const seed of seeds) {
    addEntry(seed, domain.from);
  }

  for (const entry of history) {
    const at = toSensorTimestamp(entry.createdAt);
    if (at === undefined || at < domain.from || at > domain.to) continue;

    addEntry(entry, at);
  }

  return [...sums.entries()].map(([at, { total, count }]) => ({ at, value: total / count })).sort((first, second) => first.at - second.at);
}
