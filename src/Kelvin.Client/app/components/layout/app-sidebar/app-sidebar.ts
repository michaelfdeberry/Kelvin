import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import appSidebarStyles from './app-sidebar.styles.js';
import { events } from '../../../events.js';
import { NavItem } from '../../../models/nav-item.js';
import sharedStyles from '../../../shared.styles.js';

const navItems: NavItem[] = [
  { href: '/', label: 'Home', icon: 'K' },
  { href: '/analytics', label: 'Analytics', icon: '📈' },
  { href: '/settings', label: 'Settings', icon: '⚙️' },
];

@customElement('app-sidebar')
export class AppSidebar extends LitElement {
  static override styles = [sharedStyles, appSidebarStyles];

  override connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener(events.routeChanged, this.onLocationChange);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    window.removeEventListener(events.routeChanged, this.onLocationChange);
  }

  private onLocationChange = (): void => {
    this.requestUpdate();
  };

  override render() {
    return html`
      <nav
        class="app-sidebar__nav"
        aria-label="Primary navigation"
      >
        ${navItems.map(
          item => html`
            <a
              class=${this.getLinkClass(item.href)}
              href=${item.href}
            >
              <span
                class="app-sidebar__icon"
                aria-hidden="true"
                >${item.icon}</span
              >
              <span class="app-sidebar__label">${item.label}</span>
            </a>
          `,
        )}
      </nav>
    `;
  }

  private getLinkClass(href: string): string {
    const pathname = window.location.pathname;
    const active = href === '/' ? pathname === '/' : pathname.startsWith(href);
    return active ? 'app-sidebar__link app-sidebar__link--active' : 'app-sidebar__link';
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sidebar': AppSidebar;
  }
}
