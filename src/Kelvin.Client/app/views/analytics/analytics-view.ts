import { html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import sharedStyles from '../../shared.styles.js';
import analyticsViewStyles from './analytics-view.styles.js';

@customElement('app-analytics-view')
export class AnalyticsView extends LitElement {
  static override styles = [sharedStyles, analyticsViewStyles];

  override render() {
    return html`
      <section class="analytics-view__placeholder">
        <h1 class="analytics-view__title">Analytics</h1>
        <p class="analytics-view__description">
          This route is stubbed for now. The future analytics page can surface trends, charts, and historical telemetry.
        </p>
      </section>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-analytics-view': AnalyticsView;
  }
}
