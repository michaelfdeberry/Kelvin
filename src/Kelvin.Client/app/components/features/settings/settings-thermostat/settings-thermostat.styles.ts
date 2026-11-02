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

  .settings-thermostat__toggle-label {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .settings-thermostat__section {
    display: grid;
    gap: 0.75rem;
    padding: 1rem 0;
    border-top: 1px solid var(--border-subtle);
  }

  .settings-thermostat__section:first-child {
    padding-top: 0;
    border-top: none;
  }

  .settings-thermostat__section-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .settings-thermostat__section-title {
    margin: 0;
    font-size: 1.1rem;
  }

  .settings-thermostat__description {
    margin: 0;
    color: var(--text-muted);
    line-height: 1.45;
  }

  .settings-thermostat__grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
    gap: 1rem;
  }
`;
