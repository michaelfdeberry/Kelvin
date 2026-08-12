import { ContextProvider } from '@lit/context';
import { css, html, LitElement } from 'lit';
import { customElement } from 'lit/decorators.js';

import { controlContext, defaultControlStateChange } from '../contexts/control-context';
import { defaultEnvironmentReadings, environmentReadingsContext } from '../contexts/sensors-context';
import { signalrEvents } from '../events';
import { ControlStateChange } from '../models/control-state-change';
import { EnvironmentReading } from '../models/sensors';

@customElement('signalr-context')
export class SignalRContext extends LitElement {
  static override styles = css`
    :host {
      display: contents;
    }
  `;

  private controlProvider = new ContextProvider(this, {
    context: controlContext,
    initialValue: defaultControlStateChange,
  });

  private sensorReadingsProvider = new ContextProvider(this, {
    context: environmentReadingsContext,
    initialValue: defaultEnvironmentReadings as EnvironmentReading,
  });

  override connectedCallback(): void {
    super.connectedCallback();

    // i'm not cleaning these up because this context will live the life of the application
    this.addEventListener(signalrEvents.readingsHub.sensorReadingsUpdated, event => {
      const customEvent = event as CustomEvent<EnvironmentReading>;
      this.sensorReadingsProvider.setValue(customEvent.detail);
    });

    this.addEventListener(signalrEvents.controlHub.controlStateChanged, event => {
      const customEvent = event as CustomEvent<ControlStateChange>;
      this.controlProvider.setValue(customEvent.detail);
    });
  }

  override render() {
    return html`<slot></slot>`;
  }
}
