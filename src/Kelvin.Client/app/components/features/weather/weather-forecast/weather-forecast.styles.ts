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
    max-width: 600px;
    padding: 0.8rem;
    background: var(--bg-panel);
    justify-content: space-between;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
  }

  .weather-forecast__day {
    display: flex;
    flex-direction: column;
    align-items: center;
    font-size: 0.9rem;
    min-width: 80px;
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
  }

  :host-context([kiosk]) .weather-forecast__container {
    flex-direction: column;
    max-width: none;
    width: 100%;
    height: 100%;
    justify-content: space-between;
    gap: 1rem;
    background: none;
    border: none;
    box-shadow: none;
    padding: 0;
  }

  :host-context([kiosk]) .weather-forecast__day {
    justify-content: space-between;
    flex: 1;
    padding: 1rem 0;
    width: 100%;
    min-width: 0;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
  }

  :host-context([kiosk]) .weather-forecast__day-label {
    margin-bottom: 0;
  }

  :host-context([kiosk]) .weather-forecast__icon {
    font-size: 3rem;
  }

  @media (max-width: 768px) {
    /* Hide the last child on mobile, provides only a 2 day forecast instead of 3 */
    .weather-forecast__day:nth-child(4) {
      display: none;
    }
  }
`;

export default weatherForecastStyles;
