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
    width: 100%;
    height: 300px;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
    padding: 1rem;
    gap: 1rem;
    flex-direction: column;
    --thermostat-accent: var(--text-muted);
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

  .thermostat.thermostat--heating {
    background: linear-gradient(135deg, var(--bg-panel) 40%, var(--accent-heat-shadow) 100%);
  }

  .thermostat.thermostat--cooling {
    background: linear-gradient(135deg, var(--bg-panel) 40%, var(--accent-cool-shadow) 100%);
  }

  .thermostat__header {
    display: flex;
    justify-content: space-between;
  }

  .thermostat__target-temp,
  .thermostat__status {
    height: 30px;
  }

  .thermostat__state {
    display: flex;
    flex-direction: row;
    flex: 1;
  }

  .thermostat__temps {
    flex: 1;
  }

  .thermostat__target-temp {
    margin: 0;
    font-size: 1.25rem;
    color: var(--thermostat-accent);
  }

  .thermostat__current-temp {
    margin: 0;
    font-size: 4.5rem;
    flex: 1;
    line-height: 1;
  }

  .thermostat__status {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
  }

  .thermostat__status-badge {
    padding: 4px 12px;
    background: var(--thermostat-accent-overlay);
    border: 1px solid var(--thermostat-accent);
    border-radius: 20px;
    font-size: 0.75rem;
  }

  .thermostat__air-quality {
    display: flex;
    flex-direction: row;
    gap: 2rem;
  }

  .thermostat__air-quality-item {
    font-size: 0.875rem;

    .thermostat__air-quality-value {
      margin-top: 0.25rem;
      font-size: 1.5rem;
      color: unset;

      .unit {
        font-size: 0.75rem;
      }
    }
  }

  .thermostat__controls {
    display: flex;
    gap: 0.5rem;
    justify-content: center;
    width: 100%;
  }

  .thermostat__button-group {
    display: flex;
    flex: 1;
    flex-wrap: wrap;
    flex-direction: row;
    height: 3rem;
    background: var(--bg-panel);
    border-radius: calc(var(--border-radius) + 2px);
    border: 1px solid var(--border-subtle);
    overflow: hidden;
    align-items: center;
    justify-content: space-between;
    gap: 0.125rem;

    .thermostat__button {
      border-color: transparent;
      flex: 1;
    }
  }

  .button.thermostat__button {
    &:hover {
      box-shadow: none;
    }
  }

  .button.thermostat__button--active {
    background: var(--accent-idle);
    border-color: var(--accent-idle);
    border-radius: var(--border-radius);
  }

  .button.thermostat__button--auto {
    background: var(--accent-success);
    border-color: var(--accent-success);
  }

  .button.thermostat__button--heating {
    background: var(--accent-heat);
    border-color: var(--accent-heat);
  }

  .button.thermostat__button--cooling {
    background: var(--accent-cool);
    border-color: var(--accent-cool);
  }

  .button.thermostat__button--edit,
  .button.thermostat__button--fan {
    height: 3rem;
    background: var(--bg-panel);
    border-radius: var(--border-radius);
    border: 1px solid var(--border-subtle);
  }

  .button.thermostat__button--fan {
    width: 120px;

    &.thermostat__button--active {
      border: 1px solid var(--border-subtle);
      background: var(--accent-primary);
    }
  }

  .button.thermostat__button--edit {
    width: auto;
  }

  .thermostat__button:disabled {
    cursor: not-allowed;
    opacity: 1;
  }

  @media (max-width: 768px) {
    .thermostat {
      height: auto;
    }

    .thermostat__state {
      flex-direction: column;
    }

    .thermostat__air-quality {
      flex-direction: column;
      gap: 1rem;
    }

    .thermostat__controls {
      padding-top: 1rem;
      flex-direction: column;
      width: 100%;
    }

    .button.thermostat__button--edit,
    .button.thermostat__button--fan {
      width: 100%;
      display: block;
      height: 2.5rem;
    }
  }

  :host-context([kiosk]) {
    display: flex;
    flex: 1;

    .thermostat {
      padding: 2rem;
      height: 100%;
    }

    .thermostat__current-temp {
      font-size: 6rem;
    }

    .thermostat__air-quality-item .text-muted {
      font-size: 1rem;
    }

    .thermostat__button-group {
      display: flex;
      flex: 1;
      flex-wrap: wrap;
      flex-direction: row;
      height: 3.5rem;
      background: var(--bg-panel);
      border-radius: calc(var(--border-radius) + 2px);
      border: 1px solid var(--border-subtle);
      overflow: hidden;
      align-items: center;
      justify-content: space-between;
      gap: 0.125rem;

      .thermostat__button {
        border-color: transparent;
        flex: 1;
        height: 3rem;
      }
    }

    .button.thermostat__button {
      &:hover {
        box-shadow: none;
      }
    }

    .button.thermostat__button--edit,
    .button.thermostat__button--fan {
      height: 3.5rem;
      background: var(--bg-panel);
      border-radius: var(--border-radius);
      border: 1px solid var(--border-subtle);
    }

    .button.thermostat__button--edit {
      width: 3.5rem;
    }
  }
`;

export default thermostatControlStyles;
