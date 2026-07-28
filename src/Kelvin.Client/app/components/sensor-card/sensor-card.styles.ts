import { css } from 'lit';

const sensorCardStyles = css`
  :host {
    display: block;
  }

  .sensor-card__card {
    background: var(--bg-panel);
    padding: 0.75rem;
    border-radius: var(--border-radius);
    text-align: center;
    box-sizing: border-box;
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
    font-size: 0.7rem;
    color: var(--text-muted);
    margin-top: 5px;
  }
`;

export default sensorCardStyles;