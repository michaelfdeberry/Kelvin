import { css } from 'lit';

export default css`
  app-modal {
    --modal-body-padding: 0;
    --modal-heading-border-size: 0;
  }

  .humidifier-editor {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
    min-width: 0;
    padding: 1rem;
    overflow: hidden;
  }

  .humidifier-editor__description {
    margin: 0;
    padding: 0 0 0.5rem 0;
    color: var(--text-muted);
    font-size: 0.95rem;
  }

  .humidifier-editor__season {
    display: flex;
    gap: 0.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid var(--border-subtle);
  }

  .humidifier-editor__season .form-control {
    flex: 1;
  }

  .humidifier-editor__list {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
  }

  .humidifier-editor__add {
    margin-top: 1.5rem;
    display: block;
    width: 100%;
    flex-shrink: 0;
  }

  .humidifier-editor__row {
    display: flex;
    flex-direction: row;
    gap: 0.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid var(--border-subtle);
  }

  .humidifier-editor__row:last-of-type {
    border-bottom: 0;
  }

  .humidifier-editor__row-actions {
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
  }

  @media (max-width: 768px) {
    .humidifier-editor__season,
    .humidifier-editor__row {
      flex-direction: column;
    }

    .humidifier-editor__row-actions button {
      display: block;
      width: 100%;
      border: 1px solid var(--accent-danger);
      color: var(--accent-danger);
      margin-top: 1rem;
    }
  }
`;
