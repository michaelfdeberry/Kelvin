import '../alert/alert.js';

import { html, LitElement, render } from 'lit';
import { customElement, query } from 'lit/decorators.js';

import toasterStyles from './toaster.styles';
import { events } from '../../../events.js';
import { ToastDetail } from '../../../models/toast-detail.js';
import resources from '../../../services/api-resources.js';
import { apiDelete } from '../../../services/api.js';
import sharedStyles from '../../../shared.styles.js';

@customElement('app-toaster')
export class Toaster extends LitElement {
  static override styles = [sharedStyles, toasterStyles];

  @query('.toaster')
  private toasterContainer!: HTMLDivElement;

  private renderToasts(event: Event): void {
    const toastEvent = event as CustomEvent<ToastDetail & { id?: string }>;
    const detail = toastEvent.detail;

    const alert = document.createElement('app-alert');
    alert.heading = detail.heading ?? '';
    alert.type = detail.type;
    alert.dismissible = detail.dismissible ?? false;

    render(detail.message, alert);

    if (detail.duration && detail.duration > 0) {
      setTimeout(() => alert.dismiss(), detail.duration);
    }

    if (detail.id) {
      alert.dataset.notificationId = detail.id;
      alert.addEventListener(events.alertDismissed, async () => {
        await apiDelete<void>(resources.notifications.dismissNotification, { routeParams: { id: detail.id as string } });
      });
    }

    this.toasterContainer.appendChild(alert);
  }

  private async clearToast(event: Event): Promise<void> {
    console.log('Clearing toast with event:', event);
    const toastEvent = event as CustomEvent<string>;
    const notificationId = toastEvent.detail;

    const alerts = Array.from(this.toasterContainer.querySelectorAll('app-alert'));
    for (const alert of alerts) {
      console.log('Checking alert with notificationId:', alert.dataset.notificationId);
      if (alert.dataset.notificationId === notificationId) {
        alert.remove();
      }
    }
  }

  override connectedCallback() {
    super.connectedCallback();

    this.renderToasts = this.renderToasts.bind(this);
    this.clearToast = this.clearToast.bind(this);

    document.addEventListener(events.toast, this.renderToasts, { capture: true });
    document.addEventListener(events.clearToast, this.clearToast, { capture: true });
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener(events.toast, this.renderToasts, { capture: true });
    document.removeEventListener(events.clearToast, this.clearToast, { capture: true });
  }

  override render() {
    return html`<div class="toaster"></div>`;
  }
}
