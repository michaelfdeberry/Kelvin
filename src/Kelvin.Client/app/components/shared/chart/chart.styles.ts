import { css } from 'lit';

export default css`
  :host {
    display: block;
    width: 100%;
    height: 100%;
    min-height: 150px;
  }
  svg {
    width: 100%;
    height: 100%;
    overflow: hidden;
    display: block;
  }

  .chart-surface {
    fill: var(--surface-overlay);
  }

  .chart-grid {
    stroke: var(--border-subtle);
    stroke-opacity: 0.72;
    stroke-width: 1;
  }

  .chart-axis-labels {
    fill: var(--text-muted);
    font-size: 7px;
  }

  .chart-axis-labels--time {
    font-size: 6.5px;
  }

  .chart-tooltip__guide {
    stroke: var(--text-muted);
    stroke-dasharray: 3 3;
    stroke-width: 1;
  }

  .chart-tooltip rect {
    fill: var(--bg-panel);
    stroke: var(--border-subtle);
    stroke-width: 1;
  }

  .chart-tooltip text {
    fill: var(--text-main);
    font-size: 7px;
  }

  .chart-tooltip .chart-tooltip__time {
    fill: var(--text-muted);
  }
`;
