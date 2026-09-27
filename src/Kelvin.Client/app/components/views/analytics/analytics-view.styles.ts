import { css } from 'lit';

const analyticsViewStyles = css`
  :host {
    display: block;
    padding: 1rem;
    color: var(--text-main);
  }

  .analytics-view__panel {
    max-width: 1200px;
    margin: 0 auto;
    padding: 1rem;
    padding-bottom: 0;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
  }

  .analytics-view__header,
  .analytics-view__chart-heading {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 1rem;
  }

  .analytics-view__header {
    margin-bottom: 1.5rem;
  }

  .analytics-view__title,
  .analytics-view__chart h2 {
    margin: 0;
    letter-spacing: 0;
  }

  .analytics-view__title {
    font-size: 1.75rem;
    letter-spacing: 0.01em;
  }

  .analytics-view__description,
  .analytics-view__chart p {
    margin: 0.5rem 0 0;
    color: var(--text-muted);
    line-height: 1.5;
  }

  .analytics-view__range-control {
    display: grid;
    gap: 0.3rem;
    color: var(--text-muted);
    font-size: 0.875rem;
  }

  .analytics-view__status,
  .analytics-view__empty {
    margin: 0;
    padding: 1rem;
    color: var(--text-muted);
    border: 1px solid var(--border-subtle);
    background: var(--bg-panel);
  }

  .analytics-view__status--error {
    color: var(--accent-danger);
  }
  /* 
  .analytics-view__chart {
    margin-top: 1.5rem;
    padding-top: 1.5rem;
    border-top: 1px solid var(--border-subtle);
  } */

  .analytics-view__chart-heading {
    margin-bottom: 1rem;
  }

  .analytics-view__chart h2 {
    font-size: 1.1rem;
  }

  app-kelvin-chart {
    height: 16rem;
    border: 1px solid var(--border-subtle);
    border-radius: var(--border-radius);
    overflow: hidden;
  }

  @media (max-width: 640px) {
    :host {
      padding: 0.75rem;
    }

    .analytics-view__header,
    .analytics-view__chart-heading {
      flex-direction: column;
    }

    .analytics-view__range-control,
    .analytics-view__range-control select {
      width: 100%;
    }
  }
`;

export default analyticsViewStyles;
