import { css } from 'lit';

const settingsGeneralTabStyles = css`
  :host {
    display: block;
  }

  .settings-general-tab {
    margin-top: 1rem;
  }

  .settings-general-tab__card {
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    padding: 1rem;
    background: var(--surface-overlay);
  }

  .settings-general-tab__title {
    margin: 0;
    font-size: 1.1rem;
  }

  .settings-general-tab__meta {
    margin: 0.65rem 0 0;
    line-height: 1.5;
  }

  .settings-general-tab__hint {
    margin: 0.5rem 0 0;
    color: var(--text-muted);
    line-height: 1.45;
  }
`;

export default settingsGeneralTabStyles;
