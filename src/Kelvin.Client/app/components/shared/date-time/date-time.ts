import { consume } from '@lit/context';
import { html, LitElement, nothing, TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';

import { preferencesContext } from '../../../contexts/preferences-context';
import { Preferences } from '../../../models/preferences';

@customElement('app-date-time')
export class DateTime extends LitElement {
  @consume({ context: preferencesContext, subscribe: true })
  preferences!: Preferences;

  @property({ type: String, attribute: 'date-time' })
  dateTime?: string | Date;

  @property({ type: Boolean, attribute: 'time-only', reflect: true })
  timeOnly = false;

  @property({ type: Boolean, attribute: 'date-only', reflect: true })
  dateOnly = false;

  override render(): TemplateResult | typeof nothing {
    if (!this.dateTime) return nothing;

    if (this.dateOnly) {
      return html`<span>${new Date(this.dateTime).toLocaleDateString()}</span>`;
    }

    const isHour12 = this.preferences.timeFormat === 'Hour12';
    if (this.timeOnly) {
      return html`<span>${new Date(this.dateTime).toLocaleTimeString('en-US', { hour12: isHour12 })}</span>`;
    }

    return html`<span>${new Date(this.dateTime).toLocaleString('en-US', { hour12: isHour12 })}</span>`;
  }
}
