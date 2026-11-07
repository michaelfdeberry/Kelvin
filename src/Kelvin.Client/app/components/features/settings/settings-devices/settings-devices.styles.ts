import { css } from 'lit';

export default css`
  :host {
    display: block;
  }

  .table-container {
    margin-top: 1rem;
  }

  .device-name {
    font-weight: 500;
  }

  .status {
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    font-size: 0.8rem;
    font-weight: 500;
  }

  .status::before {
    content: '';
    display: block;
    width: 8px;
    height: 8px;
    border-radius: 50%;
  }

  .status--enabled::before {
    background-color: var(--accent-success);
    box-shadow: 0 0 8px var(--accent-success-shadow);
  }

  .status--disabled::before {
    background-color: var(--text-muted);
  }

  .status--disabled {
    color: var(--text-muted);
  }

  .last-seen {
    color: var(--text-soft);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .table__actions-header {
    text-align: right;
  }

  .table__actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.25rem;
  }

  .settings-devices__form {
    padding: 1rem;
  }

  @media (max-width: 768px) {
    .table__actions {
      width: 100%;
    }
  }
`;
