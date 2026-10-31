import { html, HTMLTemplateResult } from 'lit';

import { isKioskMode } from './services/kiosk.js';

export type RouteParams = Record<string, string | undefined>;

export type Route = {
  name: string;
  title: string;
  pattern: URLPattern;
  render: (params?: RouteParams, query?: Record<string, string | undefined>) => HTMLTemplateResult | Promise<HTMLTemplateResult>;
  guard?: () => boolean | Promise<boolean>;
  redirectTo?: string;
};

export const routes: Route[] = [
  {
    name: 'home',
    title: 'Dashboard',
    pattern: new URLPattern({ pathname: '/' }),
    render: async () => {
      if (isKioskMode()) {
        await import('./components/views/kiosk/kiosk-view.js');
        return html`<app-kiosk-view></app-kiosk-view>`;
      }

      await import('./components/views/dashboard/dashboard-view.js');
      return html`<app-dashboard-view></app-dashboard-view>`;
    },
  },
  {
    name: 'analytics',
    title: 'Analytics',
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
    title: 'Settings',
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
    title: 'Not Found',
    pattern: new URLPattern({ pathname: '*' }),
    render: () => html`
      <div class="not-found">
        <h2>Page not found</h2>
        <p>The page you are looking for does not exist.</p>
      </div>
    `,
  },
];
