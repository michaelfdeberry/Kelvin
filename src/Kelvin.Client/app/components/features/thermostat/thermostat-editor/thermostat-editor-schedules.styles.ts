import { css } from 'lit';

export default css`
  :host {
    padding: 1rem;
  }

  .thermostat-editor-schedules__description {
    margin: 0;
    padding: 0 0 0.5rem 0;
    color: var(--text-muted);
    font-size: 0.95rem;
  }

  .thermostat-editor-schedules__list {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
  }

  .thermostat-editor-schedules__add {
    margin-top: 1.5rem;
    display: block;
    width: 100%;
    flex-shrink: 0;
  }

  .thermostat-editor-schedules__schedule {
    display: flex;
    flex-direction: row;
    gap: 0.5rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid var(--border-subtle);
  }

  @media (max-width: 768px) {
    .thermostat-editor-schedules__schedule {
      flex-direction: column;
    }

    .thermostat-editor-schedules__schedule-actions button {
      display: block;
      width: 100%;
      border: 1px solid var(--accent-danger);
      color: var(--accent-danger);
      margin-top: 1rem;
    }
  }

  .thermostat-editor-schedules__schedule:last-of-type {
    border-bottom: 0;
  }

  .thermostat-editor-schedules__schedule-actions {
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
  }
`;
