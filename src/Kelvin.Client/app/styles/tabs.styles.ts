import { css } from 'lit';

export default css`
  .tabs {
    display: inline-flex;
    gap: 0.5rem;
    padding: 0.35rem;
    border-radius: 999px;
    border: 1px solid var(--border-subtle);
    background: var(--surface-overlay-strong);
  }

  .tabs__tab-button {
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

  .tabs__tab-button:hover {
    color: var(--text-main);
  }

  .tabs__tab-button--active {
    background: var(--accent-primary);
    color: var(--text-on-primary-strong);
  }
`;
