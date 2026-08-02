import { css } from 'lit';

const checkboxStyles = css`
  .form-control__label:has(> .checkbox) {
    display: inline-flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.5rem 0.85rem;
    border-radius: 10px;
    background: var(--bg-panel);
    color: var(--text-main);
    font: inherit;
    font-weight: 600;
    cursor: pointer;
    transition:
      background-color 140ms ease,
      border-color 140ms ease,
      box-shadow 140ms ease,
      transform 140ms ease;
  }

  .form-control__label:has(> .checkbox:focus-visible) {
    border-color: var(--accent-info);
    box-shadow: 0 0 0 2px rgb(147 197 253 / 24%);
  }

  .form-control__input.checkbox {
    width: 1.25rem;
    height: 1.25rem;
    margin: 0;
  }

  .checkbox {
    appearance: none;
    flex-shrink: 0;
    border: 1px solid var(--accent-idle);
    border-radius: 0.25rem;
    background: var(--bg-panel);
    cursor: pointer;
    transition:
      background-color 140ms ease,
      border-color 140ms ease,
      box-shadow 140ms ease,
      transform 140ms ease;
    position: relative;
  }

  .checkbox:hover {
    border-color: var(--accent-primary);
  }

  .checkbox:active {
    transform: scale(0.96);
  }

  .checkbox:focus-visible {
    outline: none;
    border-color: var(--accent-info);
    box-shadow: 0 0 0 2px rgb(147 197 253 / 24%);
  }

  .checkbox:checked {
    border-color: var(--accent-primary-strong);
    background: var(--accent-primary-strong);
  }

  .checkbox:checked::after {
    content: '';
    position: absolute;
    inset: 0;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%23ffffff' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='5 12 10 17 19 8'/%3E%3C/svg%3E");
    background-repeat: no-repeat;
    background-position: center;
    background-size: 0.85rem;
  }
`;

export default checkboxStyles;
