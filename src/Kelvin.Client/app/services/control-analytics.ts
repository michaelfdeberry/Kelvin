import resources from './api-resources.js';
import { apiGet } from './api.js';

import type { ControlStateChange } from '../models/control-state-change.js';
import type { ControlStateResponse } from '../models/control-state.js';
import type { ControlStats } from '../models/control-stats.js';
import type { Schedule, SchedulesResponse, SetPoint, SetPointsResponse } from '../models/thermostat.js';

type ControlHistoryResponse = {
  items: ControlStateChange[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type ControlHistoryQuery = {
  from: Date;
  to: Date;
  kind: ControlStateChange['kind'];
};

export type ControlAnalyticsData = {
  controlState: ControlStateResponse;
  controlStats: ControlStats;
  latestCallChange?: ControlStateChange;
  latestLifecycleChange?: ControlStateChange;
  setPoints: SetPoint[];
  schedules: Schedule[];
};

export async function loadControlAnalyticsData(signal?: AbortSignal): Promise<ControlAnalyticsData> {
  const [controlState, controlStats, latestCallResponse, latestLifecycleResponse, setPointsResponse, schedulesResponse] = await Promise.all([
    apiGet<ControlStateResponse>(resources.control.getControlState, { signal }),
    apiGet<ControlStats>(resources.control.getControlStats, { signal }),
    apiGet<ControlHistoryResponse>(resources.control.getControlHistory, { signal, queryParams: { kind: 'Call', pageSize: 1 } }),
    apiGet<ControlHistoryResponse>(resources.control.getControlHistory, { signal, queryParams: { kind: 'Lifecycle', pageSize: 1 } }),
    apiGet<SetPointsResponse>(resources.thermostat.getSetPoints, { signal }),
    apiGet<SchedulesResponse>(resources.thermostat.getSchedules, { signal }),
  ]);

  return {
    controlState,
    controlStats,
    latestCallChange: latestCallResponse.items?.[0],
    latestLifecycleChange: latestLifecycleResponse.items?.[0],
    setPoints: setPointsResponse.setPoints,
    schedules: schedulesResponse.schedules,
  };
}

export async function loadControlHistory(query: ControlHistoryQuery, signal?: AbortSignal): Promise<ControlStateChange[]> {
  const pageSize = 200;
  const items: ControlStateChange[] = [];

  for (let page = 1; ; page += 1) {
    const response = await apiGet<ControlHistoryResponse>(resources.control.getControlHistory, {
      signal,
      queryParams: {
        from: query.from.toISOString(),
        to: query.to.toISOString(),
        kind: query.kind,
        page,
        pageSize,
      },
    });

    items.push(...response.items);
    if (items.length >= response.totalCount || response.items.length === 0) {
      return items.sort((first, second) => Date.parse(first.changedAt) - Date.parse(second.changedAt));
    }
  }
}
