import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { css, LitElement } from 'lit';

import { dispatchCustomEvent } from '../services/utilities';

export abstract class SignalRHubBase extends LitElement {
  static override styles = css`
    :host {
      display: contents;
    }
  `;

  private connection: HubConnection | undefined;
  private reconnectTimer: ReturnType<typeof setTimeout> | undefined;
  private reconnectAttempt = 0;
  private intentionalStop = false;

  protected abstract readonly hubUrl: string;
  protected abstract readonly hubName: string;

  protected abstract onSignalrConnected(): void;

  override connectedCallback(): void {
    super.connectedCallback();

    this.intentionalStop = false;
    void this.startConnection();
  }

  override disconnectedCallback(): void {
    this.intentionalStop = true;
    this.clearReconnectTimer();
    void this.stopConnection();

    super.disconnectedCallback();
  }

  protected async startConnection(): Promise<void> {
    const activeConnection = this.ensureConnection();
    if ([HubConnectionState.Connected, HubConnectionState.Connecting, HubConnectionState.Reconnecting].includes(activeConnection.state)) {
      return;
    }

    try {
      await activeConnection.start();
      this.reconnectAttempt = 0;
    } catch (error) {
      console.error(`Failed to start SignalR ${this.hubName} hub connection:`, error);
      this.scheduleReconnect();
    }
  }

  protected registerHubHandler<T>(handlerName: string, eventName: string): void {
    this.connection?.on(handlerName, (payload: T) => {
      dispatchCustomEvent<T>(this, eventName, payload);
    });
  }

  private ensureConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }

    const activeConnection = new HubConnectionBuilder().withUrl(this.hubUrl).withAutomaticReconnect().configureLogging(LogLevel.Warning).build();

    activeConnection.onclose(error => {
      if (this.intentionalStop) {
        return;
      }

      if (error) {
        console.warn(`SignalR ${this.hubName} hub disconnected unexpectedly:`, error);
      }

      this.scheduleReconnect();
    });

    this.connection = activeConnection;

    this.onSignalrConnected();
    return activeConnection;
  }

  private scheduleReconnect(): void {
    if (this.intentionalStop || this.reconnectTimer || !this.isConnected) {
      return;
    }

    const delayMs = Math.min(30000, 1000 * 2 ** this.reconnectAttempt);
    this.reconnectAttempt += 1;

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = undefined;
      void this.startConnection();
    }, delayMs);
  }

  private clearReconnectTimer(): void {
    if (!this.reconnectTimer) {
      return;
    }

    clearTimeout(this.reconnectTimer);
    this.reconnectTimer = undefined;
  }

  private async stopConnection(): Promise<void> {
    if (!this.connection) {
      return;
    }

    const activeConnection = this.connection;
    this.connection = undefined;

    try {
      await activeConnection.stop();
    } catch (error) {
      console.error(`Failed to stop SignalR ${this.hubName} hub connection:`, error);
    }
  }
}
