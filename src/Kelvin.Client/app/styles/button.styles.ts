import { css } from 'lit';

const buttonStyles = css`
  .button {
    border: 1px solid var(--accent-idle);
    border-radius: 10px;
    background: var(--bg-panel);
    color: var(--text-main);
    font: inherit;
    font-weight: 600;
    padding: 0.55rem 0.85rem;
    cursor: pointer;
    line-height: 1;
    transition:
      background-color 140ms ease,
      border-color 140ms ease,
      color 140ms ease,
      transform 140ms ease,
      box-shadow 140ms ease;
  }

  .button:hover:not(:disabled) {
    border-color: var(--accent-primary);
    box-shadow: 0 6px 16px rgb(2 6 23 / 18%);
  }

  .button:active:not(:disabled) {
    box-shadow: none;
  }

  .button:focus-visible {
    outline: 2px solid var(--accent-info);
    outline-offset: 2px;
  }

  .button:disabled {
    opacity: 0.55;
    cursor: not-allowed;
  }

  .button--primary {
    border-color: var(--accent-primary-strong);
    background: var(--accent-primary-strong);
    color: var(--text-on-primary);
  }

  .button--secondary {
    border-color: var(--border-subtle);
    background: var(--surface-overlay-panel);
    color: var(--text-main);
  }

  .button--secondary:hover:not(:disabled) {
    background: var(--surface-overlay-strong);
    border-color: var(--accent-idle);
  }

  .button--primary:hover:not(:disabled) {
    background: var(--accent-primary);
    border-color: var(--accent-primary);
  }

  .button--success {
    border-color: var(--accent-success);
    background: var(--accent-success);
    color: var(--text-on-success);
  }

  .button--success:hover:not(:disabled) {
    background: var(--accent-success-strong);
    border-color: var(--accent-success-strong);
  }

  .button--warning {
    border-color: var(--accent-heat);
    background: var(--accent-heat);
    color: var(--text-on-warning);
  }

  .button--warning:hover:not(:disabled) {
    background: var(--accent-heat-strong);
    border-color: var(--accent-heat-strong);
  }

  .button--danger {
    border-color: var(--accent-danger);
    background: var(--accent-danger);
    color: var(--text-on-danger);
  }

  .button--danger:hover:not(:disabled) {
    background: var(--accent-danger-strong);
    border-color: var(--accent-danger-strong);
  }

  .button--small {
    font-size: 0.85rem;
    padding: 0.35rem 0.65rem;
  }

  .button--icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 0.5rem;
    border-radius: 6px;
    border: none;
    background: transparent;
    color: var(--text-muted);
    line-height: 1;
  }

  .button--icon:hover:not(:disabled) {
    background: var(--surface-overlay-light);
    color: var(--text-soft);
    box-shadow: none;
  }

  .button--icon.button--danger:hover:not(:disabled) {
    background: var(--accent-danger-overlay);
    color: var(--accent-danger);
  }

  .button__icon {
    width: 1.1rem;
    height: 1.1rem;
  }
`;

export default buttonStyles;
