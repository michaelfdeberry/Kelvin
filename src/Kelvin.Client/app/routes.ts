import { html, HTMLTemplateResult } from 'lit';

export type RouteParams = Record<string, string | undefined>;

export type Route = {
  name: string;
  pattern: URLPattern;
  render: (params?: RouteParams, query?: Record<string, string | undefined>) => HTMLTemplateResult | Promise<HTMLTemplateResult>;
  guard?: () => boolean | Promise<boolean>;
  redirectTo?: string;
}

export const routes: Route[] = [
  {
    name: 'home',
    pattern: new URLPattern({ pathname: '/' }),
    render: () => html`<home-view></home-view>`,
  },
  {
    name: 'user-profile',
    pattern: new URLPattern({ pathname: '/user/:id' }),
    render: (params?: RouteParams) => html`<user-view .userId=${params?.id}></user-view>`,
  },
  {
    name: 'not-found',
    pattern: new URLPattern({ pathname: '*' }),
    render: () => html`<not-found-view></not-found-view>`,
  },
  {
    name: 'dashboard',
    pattern: new URLPattern({ pathname: '/dashboard' }),
    render: async () => {
      // Trigger the lazy network download
      await import('./pages/dashboard-view.js');
      // Return the element; it will display automatically as soon as it upgrades
      return html`<dashboard-view></dashboard-view>`;
    },
  },
  // {
  //   name: 'dashboard',
  //   pattern: new URLPattern({ pathname: '/dashboard', search: '*' }),
  //   // Guard blocks unauthorized navigation
  //   guard: () => isAuthenticated(), 
  //   redirectTo: '/login',
  //   render: () => html`<dashboard-view></dashboard-view>`
  // },
];
