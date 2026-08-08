import { consume } from '@lit/context';
import { css, html, LitElement, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { when } from 'lit/directives/when.js';

import { preferencesContext } from '../../../contexts/preferences-context';
import { Preferences } from '../../../models/preferences';
import { getPreferredUnit, toPreferredUnit } from '../../../services/utilities';
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

    const temp = toPreferredUnit(this.preferences.temperatureUnit, this.temperature, 1);
    return html`${temp}${when(
      this.showUnit,
      () => getPreferredUnit(this.preferences.temperatureUnit),
      () => html`°`,
    )}`;
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- declaration merging requires interface
  interface HTMLElementTagNameMap {
    'app-temperature': Temperature;
  }
}
