import { html, LitElement, TemplateResult } from 'lit';
import { customElement, property, state } from 'lit/decorators.js';

import { events } from './events.js';
import routerStyles from './router.styles.js';
import { routes } from './routes.js';
import sharedStyles from './shared.styles.js';

// Polyfill check for older browsers / Firefox
if (!('URLPattern' in window)) {
  await import('urlpattern-polyfill');
}

@customElement('app-router')
export class AppRouter extends LitElement {
  @state() private currentTemplate: TemplateResult = html`<dashboard-view></dashboard-view>`;
  @property({ type: Boolean, reflect: true }) loading = false;

  static override styles = [sharedStyles, routerStyles];

  override connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('popstate', this.onLocationChange);
    window.addEventListener('click', this.onLinkClick);
    this.resolveRoute(new URL(window.location.href));
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    window.removeEventListener('popstate', this.onLocationChange);
    window.removeEventListener('click', this.onLinkClick);
  }

  private onLocationChange = (): void => {
    this.resolveRoute(new URL(window.location.href));
  };

  private onLinkClick = (e: MouseEvent): void => {
    const anchor = e.composedPath().find(el => (el as HTMLElement).tagName === 'A') as HTMLAnchorElement | undefined;
    if (anchor && anchor.href && new URL(anchor.href).origin === window.location.origin) {
      e.preventDefault();
      this.navigate(anchor.pathname + anchor.search);
    }
  };

  public navigate(path: string): void {
    const targetUrl = new URL(path, window.location.origin);
    this.resolveRoute(targetUrl, true);
  }

  private async resolveRoute(url: URL, updateHistory = false): Promise<void> {
    this.loading = true;

    // 1. Locate the route configuration mapping block
    const matchedRoute = routes.find(route => route.pattern.test({ pathname: url.pathname }));

    // Fallback error routing if no pattern maps cleanly
    if (!matchedRoute) {
      this.loading = false;
      this.updateRenderedView(html`Page Not Found`, url, updateHistory);
      return;
    }

    // 2. Resolve asynchronous route authentication guards
    if (matchedRoute.guard) {
      try {
        const passedGuard = await matchedRoute.guard();
        if (!passedGuard) {
          // Re-route processing stack to fallback address
          const fallbackPath = matchedRoute.redirectTo || '/';
          const fallbackUrl = new URL(fallbackPath, window.location.origin);
          this.resolveRoute(fallbackUrl, true);
          return;
        }
      } catch (err) {
        console.error('Guard evaluation error:', err);
        this.loading = false;
        return;
      }
    }

    // 3. Parse runtime params and query state strings
    const matchResult = matchedRoute.pattern.exec({ pathname: url.pathname });
    const routeParams = matchResult?.pathname.groups || {};
    const queryParams = Object.fromEntries(new URLSearchParams(url.search));

    const nextTemplate = await matchedRoute.render(routeParams, queryParams);
    this.loading = false;

    // 4. Update display view inside native transition boundaries
    this.updateRenderedView(nextTemplate, url, updateHistory);
  }

  private updateRenderedView(template: TemplateResult, url: URL, updateHistory: boolean): void {
    // Check if the running browser environment natively supports view transitions
    if (!document.startViewTransition) {
      if (updateHistory) window.history.pushState(null, '', url.href);
      this.currentTemplate = template;
      this.emitRouteChange(url);
      return;
    }

    // Animate DOM switch seamlessly using native microtask schedules
    document.startViewTransition(() => {
      if (updateHistory) window.history.pushState(null, '', url.href);
      this.currentTemplate = template;
      this.emitRouteChange(url);
    });
  }

  private emitRouteChange(url: URL): void {
    window.dispatchEvent(
      new CustomEvent(events.routeChanged, {
        detail: {
          pathname: url.pathname,
          search: url.search,
          href: url.href,
        },
      }),
    );
  }

  override render(): TemplateResult {
    return this.currentTemplate;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-router': AppRouter;
  }
}
