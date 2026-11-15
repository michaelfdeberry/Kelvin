import { css } from 'lit';

export default css`
  .number-input {
    min-width: 0;
    max-width: 100%;
    border: 1px solid var(--control-border-color);
    border-radius: var(--border-radius);
    color: var(--text-main);
    font: inherit;
    height: var(--control-height);
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    overflow: hidden;

    transition:
      border-color 140ms ease,
      box-shadow 140ms ease;

    .number-input__button {
      height: 100%;
      width: 2.5rem;
      border: none;
      color: var(--text-main);
      background-color: var(--bg-dark);
      cursor: pointer;
      font-size: 24px;

      &:disabled {
        cursor: not-allowed;
      }
    }

    .number-input__input {
      -webkit-appearance: textfield;
      -moz-appearance: textfield;
      appearance: textfield;
      height: 100%;
      width: 100%;
      text-align: center;
      border-radius: 0;
      border: none;
      outline: none;
      background: var(--bg-dark);
      color: var(--text-main);

      &::placeholder {
        color: var(--text-muted);
      }

      &:disabled {
        cursor: not-allowed;
      }

      &:focus {
        outline: none;
      }

      &::-webkit-outer-spin-button,
      &::-webkit-inner-spin-button {
        -webkit-appearance: none;
        margin: 0;
      }
    }

    &:hover:not(:has(:disabled)) {
      border-color: var(--control-border-color-hover);
    }

    &:has(:disabled) {
      opacity: var(--control-disabled-opacity);
    }

    &:focus-within {
      border-color: var(--control-border-color-focus);
      box-shadow: var(--control-focus-ring);
    }
  }
`;
