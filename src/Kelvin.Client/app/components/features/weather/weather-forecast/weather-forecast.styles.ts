import { css } from 'lit';

const weatherForecastStyles = css`
  :host {
    display: flex;
    width: 100%;
    align-items: center;
    justify-content: center;
  }

  .weather-forecast__container {
    display: flex;
    width: 100%;
    height: 106px;
    padding: 0.8rem;
    background: var(--bg-panel);
    justify-content: space-between;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);

    p {
      text-align: center;
      margin: 0;
    }
  }

  .weather-forecast__day {
    display: flex;
    flex-direction: column;
    align-items: center;
    font-size: 0.9rem;
    min-width: 80px;
    height: 100%;
  }

  .weather-forecast__day-label {
    color: var(--text-muted);
    font-size: 0.8rem;
    text-transform: uppercase;
    margin-bottom: 4px;
  }

  .weather-forecast__icon {
    font-size: 1.5rem;
    margin-bottom: 4px;
  }

  .weather-forecast__temps {
    font-weight: 700;
  }

  .weather-forecast__temps-low {
    color: var(--text-muted);
    font-weight: 400;
    margin-left: 4px;
  }

  :host-context([kiosk]) {
    height: 100%;
    align-items: stretch;

    .weather-forecast__day {
      height: auto;
    }

    .weather-forecast__container {
      flex-direction: column;
      max-width: none;
      width: 100%;
      height: 100%;
      justify-content: space-between;
      gap: 2rem;
      padding: 2rem;
    }

    .weather-forecast__day-label {
      margin-bottom: 0;
      font-size: 1rem;
    }

    .weather-forecast__icon {
      font-size: 3rem;
    }

    .weather-forecast__temps {
      font-size: 1.2rem;
    }
  }

  @media (max-width: 768px) {
    /* Hide the last child on mobile, provides only a 2 day forecast instead of 3 */
    .weather-forecast__day:nth-child(4) {
      display: none;
    }
  }
`;

export default weatherForecastStyles;
