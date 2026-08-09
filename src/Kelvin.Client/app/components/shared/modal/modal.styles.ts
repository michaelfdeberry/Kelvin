import { css } from 'lit';

const modalStyles = css`
  :host {
    display: contents;
    --modal-padding: 1rem;
    --modal-header-padding: var(--modal-padding);
    --modal-body-padding: var(--modal-padding);
    --modal-actions-padding: var(--modal-padding);
    --modal-border-size: 1px;
    --modal-heading-border-size: var(--modal-border-size);
    --modal-actions-border-size: var(--modal-border-size);
    --modal-backdrop-background: var(--surface-overlay-strong);
  }

  .modal {
    position: fixed;
    inset: 0;
    z-index: 1200;
    background: var(--modal-backdrop-background);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 1rem;
  }

  .modal__dialog {
    width: min(100%, 560px);
    max-height: calc(100vh - 2rem);
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    background: var(--bg-panel);
    box-shadow: 0 24px 40px var(--shadow-color);
    color: var(--text-main);
    display: flex;
    flex-direction: column;
  }

  .modal__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    padding: var(--modal-header-padding);
    border-bottom: var(--modal-heading-border-size) solid var(--border-subtle);
  }

  .modal__title {
    margin: 0;
    font-size: 1.1rem;
    height: 40px;
    line-height: 40px;
  }

  .modal__close {
    border: 0;
    background: transparent;
    color: var(--text-muted);
    cursor: pointer;
    font: inherit;
    font-size: 1rem;
    line-height: 1;
    padding: 0.25rem;
  }

  .modal__close:hover {
    color: var(--text-main);
  }

  .modal__close:focus-visible {
    outline: 2px solid var(--accent-info);
    outline-offset: 2px;
    border-radius: 6px;
  }

  .modal__description {
    margin: 0 0 0.75rem;
    color: var(--text-muted);
  }

  .modal__body {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    overflow-x: hidden;
    padding: var(--modal-body-padding);
  }

  .modal__body :first-child {
    margin-top: 0;
  }

  .modal__body :last-child {
    margin-bottom: 0;
  }

  .modal__actions {
    display: flex;
    justify-content: flex-end;
    align-items: center;
    gap: 0.5rem;
    padding: var(--modal-actions-padding);
    border-top: var(--modal-actions-border-size) solid var(--border-subtle);
  }

  :host([small]) {
    --modal-padding: 0.5rem;
  }

  :host([small]) .modal__dialog {
    width: min(100%, 360px);
  }
`;

export default modalStyles;
