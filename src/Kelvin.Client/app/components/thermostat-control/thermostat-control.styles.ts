import { css } from 'lit';

const thermostatControlStyles = css`
  :host {
    display: flex;
    flex-direction: column;
    align-items: center;
    width: 100%;
  }

  .thermostat-control__dial-container {
    width: 280px;
    height: 280px;
    margin: auto 0;
    border-radius: 50%;
    background: conic-gradient(from 180deg, var(--bg-panel) 0%, var(--accent-heat) 50%, var(--bg-panel) 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
    box-shadow: 0 0 40px var(--accent-heat-shadow);
    flex-shrink: 0;
  }

  .thermostat-control__dial-inner {
    width: 240px;
    height: 240px;
    border-radius: 50%;
    background: var(--bg-dark);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
  }

  .thermostat-control__target-temp {
    margin: 0;
    margin-top: 5px;
    font-size: 1.25rem;
    color: var(--accent-heat);
  }

  .thermostat-control__current-temp {
    margin: 0;
    font-size: 4.5rem;
    font-weight: 200;
    line-height: 1;
  }

  .thermostat-control__status-badge {
    margin-top: 10px;
    padding: 4px 12px;
    background: var(--accent-heat-overlay);
    border: 1px solid var(--accent-heat);
    border-radius: 20px;
    font-size: 0.75rem;
  }

  .thermostat-control__controls {
    display: flex;
    gap: 1rem;
    margin: 1rem 0;
    flex-wrap: wrap;
    justify-content: center;
  }

  .thermostat-control__button {
    padding: 12px 24px;
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    border-radius: 30px;
    color: var(--text-main);
    font-size: 1rem;
    cursor: default;
    transition: 160ms ease;
  }

  .thermostat-control__button--active {
    background: var(--accent-heat);
    border-color: var(--accent-heat);
  }

  @media (max-height: 480px) {
    .thermostat-control__dial-container {
      transform: scale(0.9);
    }
  }
`;

export default thermostatControlStyles;
