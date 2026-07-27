import { css, html, LitElement, TemplateResult } from 'lit';
import { customElement, state } from 'lit/decorators.js';
import { routes } from './routes.js';

// Polyfill check for older browsers / Firefox
if (!('URLPattern' in window)) {
  await import('urlpattern-polyfill');
}

@customElement('app-shell')
export class AppShell extends LitElement {
  @state() private currentTemplate: TemplateResult = html`<home-view></home-view>`;
  @state() private isResolvingRoute = false;

  static override styles = css`
    :host {
      display: block;
      font-family: system-ui, sans-serif;
    }
    main {
      padding: 1rem;
      /* Opt-in this specific container into modern View Transitions */
      view-transition-name: main-content; 
    }
    .loading-overlay {
      opacity: 0.5;
      pointer-events: none;
    }
  `;

  override connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('popstate', this.onLocationChange);
    this.addEventListener('click', this.onLinkClick);
    
    // Resolve initial page load route path
    this.resolveRoute(new URL(window.location.href));
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    window.removeEventListener('popstate', this.onLocationChange);
    this.removeEventListener('click', this.onLinkClick);
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
    this.isResolvingRoute = true;

    // 1. Locate the route configuration mapping block
    const matchedRoute = routes.find(route => 
      route.pattern.test({ pathname: url.pathname })
    );

    // Fallback error routing if no pattern maps cleanly
    if (!matchedRoute) {
      this.isResolvingRoute = false;
      this.updateRenderedView(html`<not-found-view></not-found-view>`, url, updateHistory);
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
        this.isResolvingRoute = false;
        return;
      }
    }
  
    // 3. Parse runtime params and query state strings
    const matchResult = matchedRoute.pattern.exec({ pathname: url.pathname });
    const routeParams = matchResult?.pathname.groups || {};
    const queryParams = Object.fromEntries(new URLSearchParams(url.search));

    const nextTemplate = await matchedRoute.render(routeParams, queryParams);    
    this.isResolvingRoute = false;
    
    // 4. Update display view inside native transition boundaries
    this.updateRenderedView(nextTemplate, url, updateHistory);
  }

  private updateRenderedView(template: TemplateResult, url: URL, updateHistory: boolean): void {
    // Check if the running browser environment natively supports view transitions
    if (!document.startViewTransition) {
      if (updateHistory) window.history.pushState(null, '', url.href);
      this.currentTemplate = template;
      return;
    }

    // Animate DOM switch seamlessly using native microtask schedules
    document.startViewTransition(() => {
      if (updateHistory) window.history.pushState(null, '', url.href);
      this.currentTemplate = template;
    });
  }

  override render(): TemplateResult {
    return html`
      <header>
        <nav>
          <a href="/">Home</a> | 
          <a href="/dashboard">Dashboard (Protected Check)</a>
        </nav>
      </header>

      <!-- Toggle style overlays cleanly when resolving background network tasks -->
      <main class="${this.isResolvingRoute ? 'loading-overlay' : ''}">
        ${this.currentTemplate}
      </main>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-shell': AppShell;
  }
}
