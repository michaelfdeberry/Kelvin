import { css } from 'lit';

export default css`
  :host {
    display: block;
  }

  .form-group {
    margin-top: 1rem;
  }

  .card__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .settings-thermostat {
    display: grid;
    gap: 1rem;
  }

  .settings-thermostat__toggle-label {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .settings-thermostat__description {
    margin: 0 0 1rem;
    color: var(--text-muted);
    line-height: 1.45;
  }

  .settings-thermostat__grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
    gap: 1rem;
  }

  /* Grid items share a row height, so pinning the input to the bottom keeps inputs aligned when a label wraps. */
  .settings-thermostat__grid .form-control {
    display: flex;
    margin-top: 0;
  }

  .settings-thermostat__grid .form-control__label {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    width: 100%;
  }
`;
