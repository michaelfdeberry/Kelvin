import resources from './api-resources.js';
import { apiGet } from './api.js';

import type { SensorPacketHistoryEntry } from '../models/sensors.js';

export type SensorHistoryQuery = {
  from: Date;
  to: Date;
};

// Asks the server to average packets into fixed-width time periods instead of paging through every raw
// packet the gateway ever recorded - one request regardless of the selected range, and a small response
// (periods) rather than a potentially enormous array of individual readings.
export async function loadSensorHistoryPeriods(query: SensorHistoryQuery, signal?: AbortSignal): Promise<SensorPacketHistoryEntry[]> {
  const response = await apiGet<{ periods: SensorPacketHistoryEntry[] }>(resources.sensors.getSensorHistoryPeriods, {
    signal,
    queryParams: { from: query.from.toISOString(), to: query.to.toISOString() },
  });

  return response.periods;
}

// Seeds continuity for a range with no readings in it by finding the most recent packet per sensor before it started.
export async function loadLatestSensorReadingsBefore(before: Date, signal?: AbortSignal): Promise<SensorPacketHistoryEntry[]> {
  const response = await apiGet<{ readings: SensorPacketHistoryEntry[] }>(resources.sensors.getLatestSensorReadingsBefore, {
    signal,
    queryParams: { before: before.toISOString() },
  });

  return response.readings;
}
