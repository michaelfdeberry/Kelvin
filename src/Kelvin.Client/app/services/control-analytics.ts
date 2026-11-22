import resources from './api-resources.js';
import { apiGet } from './api.js';

import type { ControlStateChange } from '../models/control-state-change.js';
import type { ControlStats } from '../models/control-stats.js';

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

// The runtime totals are an aggregate over a window of history, so unlike the live state they are not pushed.
export function loadControlStats(signal?: AbortSignal): Promise<ControlStats> {
  return apiGet<ControlStats>(resources.control.getControlStats, { signal });
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

// Seeds continuity for a range with no changes in it by finding the state that was already active when it started.
export async function loadLatestControlChangeBefore(
  kind: ControlStateChange['kind'],
  before: Date,
  signal?: AbortSignal,
): Promise<ControlStateChange | undefined> {
  const response = await apiGet<ControlHistoryResponse>(resources.control.getControlHistory, {
    signal,
    queryParams: { to: before.toISOString(), kind, page: 1, pageSize: 1 },
  });

  return response.items[0];
}
