import { css } from 'lit';

const sharedStyles = css`
  .button {
    border: 1px solid var(--accent-idle);
    border-radius: 10px;
    background: var(--bg-panel);
    color: var(--text-main);
    font: inherit;
    font-weight: 600;
    padding: 0.55rem 0.85rem;
    cursor: pointer;
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

  .radios {
    margin-top: 0.8rem;
    display: grid;
    gap: 0.45rem;
    max-height: 240px;
    overflow: auto;
  }

  .radio__label {
    border: 1px solid var(--accent-idle);
    border-radius: 10px;
    background: var(--bg-panel);
    color: var(--text-main);
    font: inherit;
    font-weight: 600;
    padding: 0.55rem 0.85rem;
    cursor: pointer;
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    gap: 1rem;
    align-items: start;
  }

  .radio {
    appearance: none;
    margin: 0;
    width: 1.25rem;
    height: 1.25rem;
    border: 1px solid var(--accent-idle);
    border-radius: 10px;
    background: var(--bg-panel);
    cursor: pointer;
  }

  .radio:checked {
    border-color: var(--accent-primary-strong);
    background: var(--accent-primary-strong);
  }

  .input {
    min-width: 0;
    border: 1px solid var(--accent-idle);
    border-radius: 10px;
    background: var(--bg-dark);
    color: var(--text-main);
    font: inherit;
    padding: 0.55rem 0.75rem;
  }
`;

export default sharedStyles;
