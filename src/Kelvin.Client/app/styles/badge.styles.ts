import { css } from 'lit';

export default css`
  .badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 0.125rem 0.5rem;
    border-radius: 999px;
    font-size: 0.75rem;
    font-weight: 600;
    line-height: 1.25;
    white-space: nowrap;
    background-color: var(--bg-panel);
    color: var(--text-main);
  }

  .badge.badge--success {
    background-color: var(--accent-success);
    color: var(--text-main);
  }

  .badge.badge--info {
    background-color: var(--accent-info);
    color: var(--bg-dark);
  }

  .badge.badge--warning {
    background-color: var(--accent-heat);
    color: var(--text-main);
  }

  .badge.badge--danger {
    background-color: var(--accent-danger);
    color: var(--text-on-danger);
  }

  .badge.badge--neutral {
    background-color: var(--bg-panel);
    color: var(--text-muted);
  }
`;
