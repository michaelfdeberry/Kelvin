import { html, HTMLTemplateResult } from 'lit';

import { isKioskMode } from './services/kiosk.js';

export type RouteParams = Record<string, string | undefined>;

export type Route = {
  name: string;
  pattern: URLPattern;
  render: (params?: RouteParams, query?: Record<string, string | undefined>) => HTMLTemplateResult | Promise<HTMLTemplateResult>;
  guard?: () => boolean | Promise<boolean>;
  redirectTo?: string;
};

export const routes: Route[] = [
  {
    name: 'home',
    pattern: new URLPattern({ pathname: '/' }),
    render: async () => {
      await import('./components/views/dashboard/dashboard-view.js');
      return html`<app-dashboard-view></app-dashboard-view>`;
    },
  },
  {
    name: 'analytics',
    pattern: new URLPattern({ pathname: '/analytics' }),
    render: async () => {
      await import('./components/views/analytics/analytics-view.js');
      return html`<app-analytics-view></app-analytics-view>`;
    },
    guard: () => !isKioskMode(),
    redirectTo: '/',
  },
  {
    name: 'settings',
    pattern: new URLPattern({ pathname: '/settings' }),
    render: async () => {
      await import('./components/views/settings/settings-view.js');
      return html`<app-settings-view></app-settings-view>`;
    },
    guard: () => !isKioskMode(),
    redirectTo: '/',
  },
  {
    name: 'not-found',
    pattern: new URLPattern({ pathname: '*' }),
    render: () => html`<app-not-found-view></app-not-found-view>`,
  },
];
