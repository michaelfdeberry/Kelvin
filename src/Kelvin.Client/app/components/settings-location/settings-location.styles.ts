import { css } from 'lit';

const settingsLocationTabStyles = css`
  :host {
    display: block;
  }

  .settings-location-tab {
    margin-top: 1rem;
    display: grid;
    gap: 1rem;
  }

  .settings-location-tab__card {
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    padding: 1rem;
    background: var(--surface-overlay);
  }

  .settings-location-tab__card-content {
    display: grid;
    grid-template-columns: 1fr auto;
  }

  .settings-location-tab__title {
    margin: 0;
    font-size: 1.1rem;
  }

  .settings-location-tab__meta {
    margin: 0.65rem 0 0;
    line-height: 1.5;
  }

  .settings-location-tab__hint {
    margin: 0.5rem 0 0;
    color: var(--text-muted);
    line-height: 1.45;
  }

  .settings-location-tab__search {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 0.6rem;
    margin-top: 0.8rem;
  }

  .settings-location-tab__actions {
    padding-top: 0.8rem;
    display: flex;
    justify-content: end;
    margin-top: 0.8rem;
    gap: 0.5rem;
  }

  .settings-location-tab__error {
    margin: 0.6rem 0 0;
    color: var(--accent-danger);
  }

  @media (max-width: 700px) {
    .settings-location-tab__search {
      grid-template-columns: 1fr;
    }

    .settings-location-tab__button {
      width: 100%;
    }
  }
`;

export default settingsLocationTabStyles;
