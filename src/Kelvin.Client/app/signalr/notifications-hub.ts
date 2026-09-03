import { ContextProvider } from '@lit/context';
import { customElement } from 'lit/decorators.js';

import { SignalRHubBase } from './signalr-hub-base';
import { bannerContext, defaultBanner } from '../contexts/banner-context';
import { events } from '../events';
import { ToastDetail } from '../models/toast-detail';

const NOTIFY_HANDLER = 'Notify';

type Notification = ToastDetail & { banner?: boolean };

@customElement('signalr-notifications-hub')
export class NotificationsHub extends SignalRHubBase {
  protected override readonly hubUrl = '/hubs/notifications';
  protected override hubName = 'notifications';

  private bannerContextProvider = new ContextProvider(this, {
    context: bannerContext,
    initialValue: defaultBanner,
  });

  protected override onSignalrConnected(): void {
    this.registerHubHandler<Notification>(NOTIFY_HANDLER, events.toast, (notification: Notification) => {
      // update the context, returning undefined will prevent the event from being dispatched to the toaster
      if (notification.banner) {
        this.bannerContextProvider.setValue(notification);
        return;
      }

      // dispatch the notification as a toast
      return notification;
    });
  }
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface HTMLElementTagNameMap {
    'signalr-notifications-hub': NotificationsHub;
  }
}
