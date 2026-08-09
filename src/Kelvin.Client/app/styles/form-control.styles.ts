import { css } from 'lit';

const formControlStyles = css`
  .form-group {
    display: block;
    max-width: 100%;
    position: relative;
  }

  .form-group__actions {
    margin-top: 1rem;
    display: flex;
    gap: 0.5rem;
    justify-content: flex-end;
  }

  .form-control {
    width: 100%;
    margin-top: 1rem;
  }

  .form-control__label {
    display: block;
    font-weight: 600;
  }

  .form-control__input {
    display: block;
    max-width: 100%;
    width: 100%;
    margin-top: 0.5rem;
  }

  .form-control--inline {
    display: flex;
    justify-content: flex-end;
  }

  .form-control--inline .form-control__label {
    display: inline-flex;
    gap: 0.5rem;
    align-items: center;
    text-align: end;
    line-height: 1;
    white-space: nowrap;
    justify-content: flex-end;
  }

  .form-control--inline .form-control__input {
    margin-top: 0;
    width: 100px;
  }

  .form-group fieldset {
    margin-top: 2rem;
    border: 0;
    padding: 0;
  }

  .form-group fieldset legend {
    display: block;
    margin-top: 0.5rem;
    font-weight: 600;
  }
`;

export default formControlStyles;
