import '../alert/alert.js';

import { html, LitElement, render, TemplateResult } from 'lit';
import { customElement, query } from 'lit/decorators.js';

import toasterStyles from './toaster.styles';
import { events } from '../../../events.js';
import { ToastDetail } from '../../../models/toast-detail.js';
import sharedStyles from '../../../shared.styles.js';

@customElement('app-toaster')
export class Toaster extends LitElement {
  static override styles = [sharedStyles, toasterStyles];

  @query('.toaster')
  private toasterContainer!: HTMLDivElement;

  private renderMessage(message: string | TemplateResult): string | TemplateResult {
    if (typeof message === 'string') {
      return html`<p>${message}</p>`;
    }
    return message;
  }

  private renderToasts(event: Event) {
    const toastEvent = event as CustomEvent<ToastDetail>;
    const detail = toastEvent.detail;

    const alert = document.createElement('app-alert');
    alert.heading = detail.heading ?? '';
    alert.type = detail.type;
    alert.dismissible = detail.dismissible ?? false;
    render(detail.message, alert);

    if (detail.duration && detail.duration > 0) {
      setTimeout(() => alert.dismiss(), detail.duration);
    }

    this.toasterContainer.insertBefore(alert, this.toasterContainer.firstChild);
  }

  override connectedCallback() {
    super.connectedCallback();

    this.renderToasts = this.renderToasts.bind(this);
    document.addEventListener(events.toast, this.renderToasts, { capture: true });
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener(events.toast, this.renderToasts, { capture: true });
  }

  override render() {
    return html`<div class="toaster"></div>`;
  }
}
