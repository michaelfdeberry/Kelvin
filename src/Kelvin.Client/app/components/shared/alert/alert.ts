import { html, LitElement, nothing } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { classMap } from 'lit/directives/class-map.js';
import { when } from 'lit/directives/when.js';

import alertStyles from './alert.styles.js';
import { events } from '../../../events.js';
import { AlertType } from '../../../models/alert-type.js';
import { dispatchCustomEvent } from '../../../services/utilities.js';
import sharedStyles from '../../../shared.styles.js';

const titleByType: Record<AlertType, string> = {
  information: 'Information',
  warning: 'Warning',
  success: 'Success',
  error: 'Error',
};

const badgeByType: Record<AlertType, string> = {
  information: 'i',
  warning: '❢',
  success: '✔',
  error: '✖',
};

@customElement('app-alert')
export class Alert extends LitElement {
  static override styles = [sharedStyles, alertStyles];

  @property({ type: String, reflect: true }) type: AlertType = 'information';
  @property({ type: String }) heading = '';
  @property({ type: Boolean, reflect: true }) dismissible = false;
  @property({ type: Boolean, reflect: true }) banner = false;

  private isValidState(value: string): value is AlertType {
    return value === 'information' || value === 'warning' || value === 'success' || value === 'error';
  }

  dismiss(event?: Event) {
    event?.stopPropagation();
    dispatchCustomEvent(this, events.alertDismissed);
    this.remove();
  }

  override willUpdate(): void {
    if (!this.isValidState(this.type)) {
      this.type = 'information';
    }
  }

  override render() {
    const title = this.heading || titleByType[this.type];
    const badge = badgeByType[this.type];
    const role = this.type === 'error' ? 'alert' : 'status';

    return html`
      <section
        class="${classMap({
          alert: true,
          'alert--banner': this.banner,
        })}"
        role=${role}
        aria-live="polite"
        aria-atomic="true"
      >
        <div class="alert__container">
          <span
            class="alert__badge"
            aria-hidden="true"
          >
            ${badge}
          </span>
          <div class="alert__content">
            ${title ? html`<h3 class="alert__title">${title}</h3>` : nothing}
            <p class="alert__message">
              <slot></slot>
            </p>
          </div>
          <div class="alert__actions">
            <slot name="actions"></slot>
          </div>
          ${when(
            this.dismissible,
            () => html`
              <div>
                <button
                  class="alert__dismiss-button"
                  aria-label="Dismiss alert"
                  @click=${this.dismiss}
                >
                  <span aria-hidden="true">✖</span>
                </button>
              </div>
            `,
          )}
        </div>
      </section>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    'app-alert': Alert;
  }
}
