import { css } from 'lit';

const alertStyles = css`
  :host {
    --alert-accent: var(--accent-info);
    --alert-title: Information;
    display: block;
  }

  :host([type='warning']) {
    --alert-accent: var(--accent-heat);
  }

  :host([type='success']) {
    --alert-accent: var(--accent-success);
  }

  :host([type='error']) {
    --alert-accent: var(--accent-danger);
  }

  .alert {
    padding: 0.75rem 1rem;
    border: 1px solid color-mix(in srgb, var(--alert-accent) 65%, var(--border-subtle));
    border-left-width: 4px;
    border-radius: 12px;
    background: color-mix(in srgb, var(--alert-accent) 16%, var(--surface-overlay-panel));
    box-shadow: 0 6px 16px var(--shadow-color);
    color: var(--text-main);
    margin: 0.5rem 0;
    box-sizing: border-box;
  }

  .alert--banner {
    border-radius: 0;
    width: 100%;
    border: 0;
    margin: 0;
  }

  .alert--banner .alert__container {
    margin: 0 auto;
    width: 100%;
    max-width: 960px;
  }

  .alert__container {
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 0.75rem;
  }

  .alert__badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 1.5rem;
    height: 1.5rem;
    border-radius: 50%;
    background: color-mix(in srgb, var(--alert-accent) 24%, var(--surface-overlay-strong));
    color: var(--text-main);
    font-weight: 700;
    line-height: 1;
  }

  .alert__content {
    min-width: 0;
  }

  .alert__title {
    margin: 0;
    font-size: 0.9rem;
    font-weight: 700;
    color: color-mix(in srgb, var(--alert-accent) 70%, var(--text-main));
  }

  .alert__message {
    margin: 0.2rem 0 0;
    color: var(--text-main);
    font-size: 0.9rem;
  }

  .alert__dismiss-button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    background: transparent;
    color: var(--text-muted);
    border: 0;
  }

  .alert__dismiss-button:hover {
    color: var(--text-main);
  }

  .alert__actions {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    justify-content: flex-end;
    padding-bottom: 0.75rem;
  }
`;

export default alertStyles;
