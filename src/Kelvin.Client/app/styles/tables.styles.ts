import { css } from 'lit';

const tableStyles = css`
  .table-container {
    overflow-x: auto;
  }

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

  @media (max-width: 768px) {
    .table thead {
      display: none;
    }

    .table,
    .table tbody,
    .table tr,
    .table td {
      display: block;
      width: 100%;
    }

    .table tr {
      margin-bottom: 0.75rem;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--border-subtle);
      border-radius: var(--border-radius);
      background: var(--surface-overlay);
    }

    .table tr:last-child {
      margin-bottom: 0;
    }

    .table td {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem;
      padding: 0.4rem 0;
      border-bottom: none;
    }

    .table td::before {
      content: attr(data-label);
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--text-muted);
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }

    /* cells without a data-label render as a plain value row, e.g. an actions column */
    .table td:not([data-label])::before {
      content: none;
    }
  }
`;

export default tableStyles;
