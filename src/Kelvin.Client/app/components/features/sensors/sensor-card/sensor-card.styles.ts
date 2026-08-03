import { css } from 'lit';

const sensorCardStyles = css`
  :host {
    display: block;
  }

  button {
    all: unset;
  }

  .sensor-card {
    width: 100%;
    background: var(--bg-panel);
    padding: 0.75rem;
    border-radius: var(--border-radius);
    text-align: center;
    color: inherit;
    position: relative;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    border: 0;
    --sensor-card-pulse-start: var(--accent-heat-pulse);
    --sensor-card-pulse-end: var(--accent-heat-pulse-fade);
  }

  .sensor-card__title {
    color: var(--text-muted);
    font-size: 0.8rem;
    margin-bottom: 5px;
  }

  .sensor-card__value {
    font-size: 1.1rem;
    font-weight: 700;
  }

  .sensor-card__subtitle {
    display: flex;
    font-size: 0.7rem;
    color: var(--text-muted);
    margin-top: 5px;
    gap: 0.5rem;
  }

  .sensor-card__subtitle div:not(:last-child)::after {
    content: '•';
    display: inline-flex;
    margin-left: 0.5rem;
    align-content: center;
    align-items: center;
  }

  .sensor-card--low-battery,
  .sensor-card--unconfigured {
    position: relative;
    transition: all 0.2s ease;
    animation: pulse-glow 2.5s infinite;
  }

  .sensor-card--low-battery {
    --sensor-card-pulse-start: var(--accent-danger-pulse, var(--accent-heat-pulse));
    --sensor-card-pulse-end: var(--accent-danger-pulse-fade, var(--accent-heat-pulse-fade));
  }

  .sensor-card--unconfigured {
    cursor: pointer;
    --sensor-card-pulse-start: var(--accent-heat-pulse);
    --sensor-card-pulse-end: var(--accent-heat-pulse-fade);
  }

  .sensor-card--unconfigured:hover {
    background-color: var(--accent-heat-overlay);
  }

  .badge {
    position: absolute;
    top: -10px;
    left: 50%;
    transform: translateX(-50%);
  }

  /* Pulsing Shadow Animation */
  @keyframes pulse-glow {
    0% {
      box-shadow: 0 0 0 0 var(--sensor-card-pulse-start);
    }
    70% {
      box-shadow: 0 0 0 10px var(--sensor-card-pulse-end);
    }
    100% {
      box-shadow: 0 0 0 0 var(--sensor-card-pulse-end);
    }
  }
`;

export default sensorCardStyles;
