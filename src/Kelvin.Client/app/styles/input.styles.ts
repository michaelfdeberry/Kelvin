import { css } from 'lit';

const inputStyles = css`
  .input:not([type='checkbox']):not([type='radio']),
  .select {
    display: block;
    min-width: 0;
    max-width: 100%;
    border: 1px solid var(--control-border-color);
    border-radius: var(--border-radius);
    background: var(--bg-dark);
    color: var(--text-main);
    font: inherit;
    padding: 0 1rem;
    height: var(--control-height);
    transition:
      border-color 140ms ease,
      box-shadow 140ms ease;
  }

  .input::placeholder {
    color: var(--text-muted);
  }

  .input[type='number']::-webkit-outer-spin-button,
  .input[type='number']::-webkit-inner-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }

  /* Firefox */
  .input[type='number'] {
    -moz-appearance: textfield;
    appearance: textfield; /* Standard property */
  }

  .input:hover:not(:disabled),
  .select:hover:not(:disabled) {
    border-color: var(--control-border-color-hover);
  }

  .input:focus,
  .select:focus {
    outline: none;
  }

  .input:focus-visible,
  .select:focus-visible {
    border-color: var(--control-border-color-focus);
    box-shadow: var(--control-focus-ring);
  }

  .input:read-only:not(:disabled) {
    color: var(--text-muted);
  }

  .input:disabled,
  .select:disabled {
    opacity: var(--control-disabled-opacity);
    cursor: not-allowed;
  }

  .select {
    appearance: none;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%23ffffff' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'/%3E%3C/svg%3E");
    background-repeat: no-repeat;
    background-position: right 0.75rem center;
    background-size: 1rem;
    padding-right: 2.5rem;
  }
`;

export default inputStyles;
