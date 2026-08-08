import { css } from 'lit';

export default css`
  :host {
    display: block;
  }

  .sensor-info {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .sensor-name {
    font-weight: 500;
  }

  .battery-pill {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.2rem 0.5rem;
    border-radius: 9999px;
    background: var(--bg-dark);
    font-size: 0.7rem;
    font-weight: 600;
    border: 1px solid var(--border-subtle);
    white-space: nowrap;
  }
  .battery-pill--high {
    color: var(--accent-success);
  }
  .battery-pill--med {
    color: var(--accent-warning);
  }
  .battery-pill--low {
    color: var(--accent-danger);
    border-color: var(--accent-danger-pulse);
  }

  .features {
    display: flex;
    gap: 0.5rem;
  }

  .badge {
    display: inline-flex;
    align-items: center;
    padding: 0.2rem 0.5rem;
    border-radius: 6px;
    font-size: 0.7rem;
    font-weight: 600;
    background: transparent;
    border: 1px dashed var(--border-subtle);
    color: var(--text-muted);
    opacity: 0.5;
  }

  .badge--active {
    opacity: 1;
    background: var(--accent-cyan-overlay);
    color: var(--accent-cyan);
    border: 1px solid var(--accent-cyan-overlay-strong);
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

  /* Action Buttons */
  .table__actions-header {
    text-align: right;
  }

  .table__actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.25rem;
  }

  @media (max-width: 768px) {
    .table__actions {
      width: 100%;
    }
  }
`;
