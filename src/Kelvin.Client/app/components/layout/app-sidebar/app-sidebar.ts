import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { unsafeSVG } from 'lit/directives/unsafe-svg.js';

import appSidebarStyles from './app-sidebar.styles.js';
import chartIcon from '../../../../assets/icons/chart.svg?raw';
import settingsIcon from '../../../../assets/icons/gear.svg?raw';
import { events } from '../../../events.js';
import { NavItem } from '../../../models/nav-item.js';
import sharedStyles from '../../../shared.styles.js';

const navItems: NavItem[] = [
  { href: '/', label: 'Home', icon: 'K' },
  { href: '/analytics', label: 'Analytics', icon: unsafeSVG(chartIcon) },
  { href: '/settings', label: 'Settings', icon: unsafeSVG(settingsIcon) },
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
        ${navItems.map(item => {
          return html`
            <a
              class=${classMap({
                'app-sidebar__link': true,
                'app-sidebar__link--active': this.isLinkActive(item.href),
              })}
              href=${item.href}
            >
              <span
                class=${classMap({
                  'app-sidebar__icon': true,
                  button__icon: typeof item.icon !== 'string',
                })}
                aria-hidden="true"
                >${item.icon}</span
              >
              <span class="app-sidebar__label">${item.label}</span>
            </a>
          `;
        })}
      </nav>
    `;
  }

  private isLinkActive(href: string): boolean {
    const pathname = window.location.pathname;
    return href === '/' ? pathname === '/' : pathname.startsWith(href);
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-sidebar': AppSidebar;
  }
}
