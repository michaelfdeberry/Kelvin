import { css } from 'lit';

const statsPanelStyles = css`
  :host {
    display: block;
    padding: 2rem;
    overflow-y: auto;
    box-sizing: border-box;
  }

  .stats-panel__section {
    margin-bottom: 2rem;
  }

  .stats-panel__section-title {
    font-size: 1rem;
    color: var(--text-muted);
    border-bottom: 1px solid var(--border-subtle);
    padding-bottom: 10px;
    margin: 0 0 1rem;
  }

  .stats-panel__row {
    display: flex;
    justify-content: space-between;
    gap: 1rem;
    margin-bottom: 10px;
    font-size: 0.9rem;
  }

  .stats-panel__value {
    font-family: monospace;
    font-size: 1rem;
    text-align: right;
  }

  .stats-panel__status-dot {
    width: 8px;
    height: 8px;
    background: var(--accent-success);
    border-radius: 50%;
    display: inline-block;
    margin-right: 5px;
  }
`;

export default statsPanelStyles;
