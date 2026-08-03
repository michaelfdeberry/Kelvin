import { css } from 'lit';

export default css`
  app-modal {
    --modal-body-padding: 0;
    --modal-heading-border-size: 0;
  }

  .thermostat-editor {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 300px;
    min-width: 0;
    overflow: hidden;
  }

  .thermostat-editor app-tabs {
    flex: 1;
    min-height: 0;
    min-width: 0;
    width: 100%;
    max-width: 100%;
  }

  .thermostat-editor__panel {
    padding: 1rem;
  }

  .thermostat-editor__panel-description {
    margin: 0;
    padding: 0 0 0.5rem 0;
    color: var(--text-muted);
    font-size: 0.95rem;
  }

  .thermostat-editor__schedules {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
  }

  .thermostat-editor__schedule-list {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
  }

  .thermostat-editor__add-schedule {
    margin-top: 1.5rem;
    display: block;
    width: 100%;
    flex-shrink: 0;
  }

  .thermostat-editor__schedule {
    display: flex;
    flex-direction: row;
    gap: 0.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid var(--border-subtle);
  }

  @media (max-width: 768px) {
    .thermostat-editor__schedule {
      flex-direction: column;
    }

    .thermostat-editor__schedule-actions button {
      display: block;
      width: 100%;
      border: 1px solid var(--accent-danger);
      color: var(--accent-danger);
      margin-top: 1rem;
    }
  }

  .thermostat-editor__schedule:last-of-type {
    border-bottom: 0;
  }

  .thermostat-editor__schedule-details > .form-control {
    flex: 1;
  }

  .thermostat-editor__schedule-actions {
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
  }
`;
