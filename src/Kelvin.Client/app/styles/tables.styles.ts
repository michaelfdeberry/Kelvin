import { css } from 'lit';

const tableStyles = css`
  .table {
    width: 100%;
    border-collapse: collapse;
    font: inherit;
    color: var(--text-main);
  }

  .table th,
  .table td {
    padding: 0.6rem 0.75rem;
    text-align: left;
    border-bottom: 1px solid var(--border-subtle);
  }

  .table thead th {
    color: var(--text-muted);
    font-size: 0.85rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.03em;
    border-bottom: 1px solid var(--border-subtle);
  }

  .table tbody tr {
    font-size: 0.875rem;
    transition: background-color 140ms ease;
  }

  .table tbody tr:hover {
    background: var(--surface-overlay-panel);
  }

  .table tbody tr:last-child td {
    border-bottom: 0;
  }

  th.table__actions-header,
  .table__actions {
    text-align: right;
  }
`;

export default tableStyles;
