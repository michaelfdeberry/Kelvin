import { css } from 'lit';

export default css`
  :host {
    display: block;
    min-height: 100%;
    padding: 2rem;
    box-sizing: border-box;
    color: var(--text-main);
  }

  .settings-view__panel {
    max-width: 860px;
    margin: 0 auto;
    padding: 1.5rem;
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    border: 1px solid var(--border-subtle);
    box-shadow: 0 14px 34px var(--shadow-color);
  }

  .settings-view__title {
    margin: 0;
    font-size: 1.75rem;
    letter-spacing: 0.01em;
  }

  .settings-view__description {
    margin: 0.5rem 0 1.25rem;
    color: var(--text-muted);
    line-height: 1.5;
  }

  @media (max-width: 700px) {
    :host {
      padding: 1rem;
    }
  }
`;
