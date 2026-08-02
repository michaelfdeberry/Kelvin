import { consume } from '@lit/context';
import { css, html, LitElement, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import { preferencesContext } from '../../../contexts/preferences-context';
import { Preferences } from '../../../models/preferences';
import sharedStyles from '../../../shared.styles';

@customElement('app-temperature')
export class Temperature extends LitElement {
  static override styles = [
    sharedStyles,
    css`
      :host {
        display: inline-block;
        color: inherit;
      }
    `,
  ];

  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @property({ type: Number }) temperature?: number;
  @property({ type: Boolean, attribute: 'show-unit' }) showUnit: boolean = false;

  override render() {
    if (this.temperature === undefined) return nothing;
    if (this.temperature === null) return nothing;

    if (this.preferences.temperatureUnit === 'Celsius') {
      return html`${this.temperature.toFixed(1)}°${when(this.showUnit, () => html`C`)}`;
    }

    if (this.preferences.temperatureUnit === 'Fahrenheit') {
      const fahrenheit = (this.temperature * 9) / 5 + 32;
      return html`${fahrenheit.toFixed(1)}°${when(this.showUnit, () => html`F`)}`;
    }

    return '';
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-temperature': Temperature;
  }
}
