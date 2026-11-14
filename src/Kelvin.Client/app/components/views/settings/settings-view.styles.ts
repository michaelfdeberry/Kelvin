import { css } from 'lit';

export default css`
  :host {
    display: block;
    padding: 1rem;
    color: var(--text-main);
  }

  .settings-view__panel {
    max-width: 1200px;
    margin: 0 auto;
    padding: 1rem;
    padding-bottom: 0;
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

  .settings-view__layout {
    display: grid;
    grid-template-columns: 12rem minmax(0, 1fr);
    gap: 1.5rem;
    align-items: start;
  }

  .settings-view__nav {
    position: sticky;
    top: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .settings-view__link {
    padding: 0.6rem 0.9rem;
    border-radius: 8px;
    color: var(--text-main);
    text-decoration: none;
    border: 1px solid transparent;
  }

  .settings-view__link:hover {
    background: var(--surface-overlay);
  }

  .settings-view__link--active {
    background: var(--surface-overlay-strong);
    border-color: var(--border-subtle);
    font-weight: 600;
  }

  .settings-view__content {
    display: grid;
    gap: 1rem;
    min-width: 0;
  }

  @media (max-width: 768px) {
    :host {
      padding: 1rem;
    }

    .settings-view__layout {
      grid-template-columns: 1fr;
    }

    .settings-view__nav {
      position: static;
      flex-direction: row;
      overflow-x: auto;
    }
  }
`;
