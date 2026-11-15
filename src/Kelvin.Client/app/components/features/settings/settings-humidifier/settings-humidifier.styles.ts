import { css } from 'lit';

export default css`
  :host {
    display: block;
  }

  .settings-humidifier {
    display: grid;
    gap: 1rem;
  }

  .card__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .settings-humidifier__description {
    margin: 1rem 0;
    color: var(--text-muted);
    line-height: 1.45;
  }

  .settings-humidifier__summary {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem 2rem;
    margin: 0;
  }

  .settings-humidifier__summary dt {
    color: var(--text-muted);
    font-size: 0.8rem;
  }

  .settings-humidifier__summary dd {
    margin: 0;
    font-weight: 500;
  }

  .table-container {
    margin-top: 1rem;
  }
`;
