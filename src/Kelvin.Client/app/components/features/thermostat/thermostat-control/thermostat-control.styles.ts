import { css } from 'lit';

const thermostatControlStyles = css`
  :host {
    display: flex;
    flex-direction: column;
    align-items: center;
    width: 100%;
  }

  .thermostat {
    display: flex;
    flex-direction: column;
    align-items: center;
    --thermostat-accent: var(--accent-idle);
    --thermostat-accent-shadow: var(--accent-idle-shadow);
    --thermostat-accent-overlay: var(--accent-idle-overlay);
  }

  .thermostat--heating {
    --thermostat-accent: var(--accent-heat);
    --thermostat-accent-shadow: var(--accent-heat-shadow);
    --thermostat-accent-overlay: var(--accent-heat-overlay);
  }

  .thermostat--cooling {
    --thermostat-accent: var(--accent-cool);
    --thermostat-accent-shadow: var(--accent-cool-shadow);
    --thermostat-accent-overlay: var(--accent-cool-overlay);
  }

  .thermostat--off {
    --thermostat-accent: var(--accent-idle);
    --thermostat-accent-shadow: var(--accent-idle-shadow);
    --thermostat-accent-overlay: var(--accent-idle-overlay);
  }

  .thermostat--disabled {
    --thermostat-accent: var(--accent-danger);
    --thermostat-accent-shadow: var(--accent-danger-shadow);
    --thermostat-accent-overlay: var(--accent-danger-overlay);
  }

  .thermostat__dial {
    width: 280px;
    height: 280px;
    margin: auto 0;
    border-radius: 50%;
    background: conic-gradient(from 180deg, var(--bg-panel) 0%, var(--thermostat-accent) 50%, var(--bg-panel) 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
    box-shadow: 0 0 40px var(--thermostat-accent-shadow);
    flex-shrink: 0;
  }

  .thermostat__dial-inner {
    width: 240px;
    height: 240px;
    padding: 15px 0;
    border-radius: 50%;
    background: var(--bg-dark);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
  }

  .thermostat__target-temp {
    margin: 0;
    font-size: 1.25rem;
    color: var(--thermostat-accent);
  }

  .thermostat__current-temp {
    margin: 0;
    font-size: 4rem;
    flex: 1;
  }

  :host-context([kiosk]) .thermostat__dial {
    width: 370px;
    height: 370px;
  }

  :host-context([kiosk]) .thermostat__dial-inner {
    width: 320px;
    height: 320px;
  }

  :host-context([kiosk]) .thermostat__current-temp {
    font-size: 4.5rem;
  }

  .thermostat__target-temp,
  .thermostat__status {
    height: 30px;
  }

  .thermostat__status {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
  }

  .thermostat__status-badge {
    padding: 4px 12px;
    background: var(--thermostat-accent-overlay);
    border: 1px solid var(--thermostat-accent);
    border-radius: 20px;
    font-size: 0.75rem;
  }

  .thermostat__spacer,
  .thermostat__edit-button-container {
    height: 40px;
  }

  .thermostat__edit-button {
    margin-top: 10px;
  }

  .thermostat__controls {
    display: flex;
    gap: 1rem;
    margin: 1rem 0;
    flex-wrap: wrap;
    justify-content: center;
  }

  .thermostat__controls .thermostat__button {
    width: 86px;
  }

  .thermostat__button--auto {
    background: var(--accent-success);
    border-color: var(--accent-success);
  }

  .thermostat__button--heating {
    background: var(--accent-heat);
    border-color: var(--accent-heat);
  }

  .thermostat__button--cooling {
    background: var(--accent-cool);
    border-color: var(--accent-cool);
  }

  .thermostat__button--fan {
    background: var(--accent-primary);
    border-color: var(--accent-primary);
  }

  .thermostat__button--active {
    background: var(--accent-idle);
    border-color: var(--accent-idle);
  }

  .thermostat__button:disabled {
    cursor: not-allowed;
  }

  @media (max-width: 768px) {
    .thermostat__dial-container {
      transform: scale(0.9);
    }

    .thermostat__controls {
      padding-top: 1rem;
    }

    .thermostat__button,
    .thermostat__controls .thermostat__button {
      width: 100%;
      display: block;
    }
  }
`;

export default thermostatControlStyles;
