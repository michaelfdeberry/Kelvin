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
    padding: 1rem;
    border-radius: var(--border-radius);
    text-align: center;
    color: inherit;
    position: relative;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    align-items: center;
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
    --sensor-card-pulse-start: var(--accent-heat-pulse);
    --sensor-card-pulse-end: var(--accent-heat-pulse-fade);
  }
  .sensor-card__title,
  .sensor-card__subtitle {
    height: 2rem;
    line-height: 2rem;
    font-size: 1rem;
  }

  .sensor-card__title {
    color: var(--text-muted);
  }

  .sensor-card__value {
    font-size: 1.5rem;
  }

  .sensor-card__subtitle {
    display: flex;
    color: var(--text-muted);
    gap: 0.5rem;
  }

  .sensor-card__subtitle div:not(:last-child)::after {
    content: '•';
    display: inline-flex;
    margin-left: 0.5rem;
    align-content: center;
    align-items: center;
  }

  .sensor-card--offline,
  .sensor-card--low-battery,
  .sensor-card--unconfigured {
    position: relative;
    transition: all 0.2s ease;
    animation: pulse-glow 2.5s infinite;
  }

  .sensor-card--offline {
    --sensor-card-pulse-start: var(--accent-danger-pulse, var(--accent-heat-pulse));
    --sensor-card-pulse-end: var(--accent-danger-pulse-fade, var(--accent-heat-pulse-fade));
  }

  .sensor-card--low-battery,
  .sensor-card--unconfigured {
    --sensor-card-pulse-start: var(--accent-heat-pulse);
    --sensor-card-pulse-end: var(--accent-heat-pulse-fade);
  }

  .sensor-card--unconfigured {
    cursor: pointer;
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

  :host-context([kiosk]) {
    .sensor-card {
      padding: 2rem;
      gap: 1rem;
    }

    .sensor-card__title {
      font-size: 1.25rem;
      margin-bottom: 0;
    }

    .sensor-card__value {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      font-size: 2rem;
      line-height: 2rem;
    }

    .sensor-card__subtitle {
      font-size: 1.25rem;
    }
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
