import resources from './api-resources.js';
import { apiGet } from './api.js';

import type { SensorPacketHistoryEntry } from '../models/sensors.js';

type SensorHistoryResponse = {
  items: SensorPacketHistoryEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type SensorHistoryQuery = {
  from: Date;
  to: Date;
};

export async function loadSensorHistory(query: SensorHistoryQuery, signal?: AbortSignal): Promise<SensorPacketHistoryEntry[]> {
  const pageSize = 200;
  const items: SensorPacketHistoryEntry[] = [];

  for (let page = 1; ; page += 1) {
    const response = await apiGet<SensorHistoryResponse>(resources.sensors.getSensorHistory, {
      signal,
      queryParams: {
        from: query.from.toISOString(),
        to: query.to.toISOString(),
        page,
        pageSize,
      },
    });

    items.push(...response.items);
    if (items.length >= response.totalCount || response.items.length === 0) {
      return items.sort((first, second) => Date.parse(first.createdAt) - Date.parse(second.createdAt));
    }
  }
}

// Seeds continuity for a range with no readings in it by finding the most recent packet per sensor before it started.
export async function loadLatestSensorReadingsBefore(before: Date, signal?: AbortSignal): Promise<SensorPacketHistoryEntry[]> {
  const response = await apiGet<SensorHistoryResponse>(resources.sensors.getSensorHistory, {
    signal,
    queryParams: { to: before.toISOString(), page: 1, pageSize: 200 },
  });

  const latestBySensor = new Map<string, SensorPacketHistoryEntry>();
  for (const entry of response.items) {
    if (entry.sensorId && !latestBySensor.has(entry.sensorId)) {
      latestBySensor.set(entry.sensorId, entry);
    }
  }

  return [...latestBySensor.values()];
}
