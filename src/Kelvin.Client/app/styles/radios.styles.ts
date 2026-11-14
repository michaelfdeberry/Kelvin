import { css } from 'lit';

const radiosStyles = css`
  .radios {
    margin-top: 0.8rem;
    display: grid;
    gap: 0.45rem;
    max-height: 240px;
    overflow: auto;
  }

  .radio__label {
    border: 1px solid var(--control-border-color);
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    color: var(--text-main);
    font: inherit;
    font-weight: 600;
    padding: 0.5rem 1rem;
    height: var(--control-height);
    cursor: pointer;
    display: grid;
    grid-template-columns: auto minmax(0, 1fr);
    gap: 1rem;
    align-items: start;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    transition:
      background-color 140ms ease,
      border-color 140ms ease,
      box-shadow 140ms ease,
      transform 140ms ease;
  }

  .radio__label span {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .radio__label:hover {
    border-color: var(--control-border-color-hover);
  }

  .radio {
    appearance: none;
    margin: 0;
    width: 1.25rem;
    height: 1.25rem;
    border: 1px solid var(--control-border-color);
    border-radius: var(--border-radius);
    background: var(--bg-panel);
    cursor: pointer;
    transition:
      background-color 140ms ease,
      border-color 140ms ease,
      box-shadow 140ms ease,
      transform 140ms ease;
  }

  .radio:hover:not(:disabled) {
    border-color: var(--control-border-color-hover);
  }

  .radio:active:not(:disabled) {
    transform: scale(0.96);
  }

  .radio:focus-visible {
    outline: none;
    border-color: var(--control-border-color-focus);
    box-shadow: var(--control-focus-ring);
  }

  .radio:disabled {
    opacity: var(--control-disabled-opacity);
    cursor: not-allowed;
  }

  .radio:checked {
    border-color: var(--accent-primary-strong);
    background: var(--accent-primary-strong);
  }
`;

export default radiosStyles;
