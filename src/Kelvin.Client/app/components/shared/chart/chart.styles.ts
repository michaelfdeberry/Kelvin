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
    overflow: visible;
    /* Ensures the SVG stretches to fit the container */
    display: block;
  }
  .chart-bg {
    fill: var(--bg-panel, #1e293b);
    border-radius: 8px;
  }
`;
