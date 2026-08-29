import { css } from 'lit';

export default css`
  .number-input {
    min-width: 0;
    max-width: 100%;
    border: 1px solid var(--accent-idle);
    border-radius: var(--border-radius);
    color: var(--text-main);
    font: inherit;
    height: 2.5rem;
    display: flex;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    overflow: hidden;

    transition:
      border-color 140ms ease,
      box-shadow 140ms ease,
      background-color 140ms ease;

    .number-input__button {
      height: 100%;
      width: 2.5rem;
      border: none;
      color: var(--button-text-color);
      background-color: var(--bg-dark);
      cursor: pointer;
      font-size: 24px;
    }

    .number-input__input {
      height: 100%;
      width: 100%;
      text-align: center;
      border-radius: 0;
      border: none;
      outline: none;
      background: var(--bg-dark);
      color: var(--text-main);
      font: inherit;

      &:hover:not(:disabled) {
        border-color: var(--accent-idle);
        background-color: var(--surface-overlay-panel);
      }

      &:focus {
        outline: none;
      }

      &[type='number']::-webkit-outer-spin-button,
      &[type='number']::-webkit-inner-spin-button {
        -webkit-appearance: none;
        margin: 0;
      }
    }

    &:focus-within {
      box-shadow: 0 0 0 2px rgb(147 197 253 / 22%);
    }
  }
`;
