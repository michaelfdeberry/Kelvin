import { css } from 'lit';

export default css`
  :host {
    display: block;
    width: 100%;
    height: 100%;
    min-height: 150px;
  }

  .chart-container {
    position: relative;
    width: 100%;
    height: 100%;
  }

  canvas {
    position: absolute;
    inset: 0;
  }
`;
