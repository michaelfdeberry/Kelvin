import { html, LitElement } from 'lit';
import { customElement, property } from 'lit/decorators.js';

import sharedStyles from '../../shared.styles.js';
import sensorCardStyles from './sensor-card.styles.js';

@customElement('app-sensor-card')
export class SensorCard extends LitElement {
  @property({ type: String }) override title = '';

  @property({ type: String }) value = '';

  @property({ type: String }) subtitle = '';

  static override styles = [sharedStyles, sensorCardStyles];

  override render() {
    return html`
      <div class="sensor-card__card">
        <div class="sensor-card__title">${this.title}</div>
        <div class="sensor-card__value">${this.value}</div>
        <div class="sensor-card__subtitle">${this.subtitle}</div>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-sensor-card': SensorCard;
  }
}
