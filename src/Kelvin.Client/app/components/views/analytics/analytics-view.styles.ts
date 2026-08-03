import { css } from 'lit';

const analyticsViewStyles = css`
  :host {
    display: block;
    padding: 2rem;
    background: var(--bg-dark);
    color: var(--text-main);
    height: 100vh;
    overflow: auto;
  }

  .analytics-view__placeholder {
    max-width: 640px;
    padding: 1.5rem;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
  }

  .analytics-view__title {
    margin: 0 0 0.5rem;
    font-size: 1.75rem;
  }

  .analytics-view__description {
    margin: 0;
    color: var(--text-muted);
    line-height: 1.5;
  }
`;

export default analyticsViewStyles;
