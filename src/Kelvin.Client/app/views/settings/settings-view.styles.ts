import { css } from 'lit';

const settingsViewStyles = css`
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

  .settings-view__tabs {
    display: inline-flex;
    gap: 0.5rem;
    padding: 0.35rem;
    border-radius: 999px;
    border: 1px solid var(--border-subtle);
    background: var(--surface-overlay-strong);
  }

  .settings-view__tab-button {
    border: 0;
    background: transparent;
    color: var(--text-muted);
    padding: 0.5rem 0.9rem;
    border-radius: 999px;
    cursor: pointer;
    font: inherit;
    font-size: 0.95rem;
    transition:
      background-color 160ms ease,
      color 160ms ease;
  }

  .settings-view__tab-button:hover {
    color: var(--text-main);
  }

  .settings-view__tab-button--active {
    background: var(--accent-primary);
    color: var(--text-on-primary-strong);
  }

  .settings-view__error {
    margin: 0.6rem 0 0;
    color: var(--accent-danger);
  }

  @media (max-width: 700px) {
    :host {
      padding: 1rem;
    }
  }
`;

export default settingsViewStyles;
