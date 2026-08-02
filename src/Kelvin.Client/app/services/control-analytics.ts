import { apiFetch } from './api.js';

import type { ControlStateChange } from '../models/control-state-change.js';
import type { ControlStateResponse } from '../models/control-state.js';
import type { ControlStats } from '../models/control-stats.js';
import type { Schedule, SchedulesResponse, SetPoint, SetPointsResponse } from '../models/thermostat.js';

interface ControlHistoryResponse {
  items?: ControlStateChange[];
}

export interface ControlAnalyticsData {
  controlState: ControlStateResponse;
  controlStats: ControlStats;
  latestCallChange?: ControlStateChange;
  latestLifecycleChange?: ControlStateChange;
  setPoints: SetPoint[];
  schedules: Schedule[];
}

export async function loadControlAnalyticsData(signal?: AbortSignal): Promise<ControlAnalyticsData> {
  const [controlState, controlStats, latestCallResponse, latestLifecycleResponse, setPointsResponse, schedulesResponse] = await Promise.all([
    apiFetch<ControlStateResponse>('control/state', { signal }),
    apiFetch<ControlStats>('control/stats', { signal }),
    apiFetch<ControlHistoryResponse>('control/history?kind=Call&pageSize=1', { signal }),
    apiFetch<ControlHistoryResponse>('control/history?kind=Lifecycle&pageSize=1', { signal }),
    apiFetch<SetPointsResponse>('thermostat/set-points', { signal }),
    apiFetch<SchedulesResponse>('thermostat/schedules', { signal }),
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
