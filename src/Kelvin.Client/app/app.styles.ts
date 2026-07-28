import { css } from 'lit';

const appShellStyles = css`
  :host {
    display: block;
    min-height: 100vh;
    color: var(--text-main);
  }

  .app-shell__shell {
    display: grid;
    grid-template-columns: 80px minmax(0, 1fr);
    min-height: 100vh;
    background: var(--bg-dark);
  }

  .app-shell__main {
    min-width: 0;
    min-height: 100vh;
    view-transition-name: main-content;
  }

  .app-shell__main--loading {
    opacity: 0.5;
    pointer-events: none;
  }

  @media (max-width: 1024px) {
    .app-shell__shell {
      grid-template-columns: 1fr;
    }

    app-sidebar {
      display: none;
    }
  }
`;

export default appShellStyles;
