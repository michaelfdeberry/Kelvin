import { css } from 'lit';

const routerStyles = css`
  :host {
    display: block;
    min-width: 0;
    height: 100%;
    view-transition-name: main-content;
  }

  :host([loading]) {
    opacity: 0.5;
    pointer-events: none;
  }
`;

export default routerStyles;
