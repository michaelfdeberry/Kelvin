import { css } from 'lit';

const modalStyles = css`
  :host {
    display: contents;
  }

  .modal {
    position: fixed;
    inset: 0;
    z-index: 1200;
    background: var(--surface-overlay-strong);
    display: grid;
    place-items: center;
    padding: 1rem;
    box-sizing: border-box;
  }

  .modal__dialog {
    width: min(100%, 560px);
    max-height: calc(100vh - 2rem);
    overflow: auto;
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    background: var(--bg-panel);
    box-shadow: 0 24px 40px var(--shadow-color);
    color: var(--text-main);
    padding: 1rem;
    box-sizing: border-box;
  }

  .modal__header {
    display: flex;
    align-items: start;
    justify-content: space-between;
    gap: 0.75rem;
    margin-bottom: 0.5rem;
  }

  .modal__title {
    margin: 0;
    font-size: 1.1rem;
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
    margin-bottom: 1rem;
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
  }
`;

export default modalStyles;
