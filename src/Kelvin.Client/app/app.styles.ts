import { css } from 'lit';

const appShellStyles = css`
  :host {
    --banner-height: 80px;

    display: block;
    min-height: 100vh;
    color: var(--text-main);
  }

  .app-shell__banner {
    width: 100%;
    flex-shrink: 0;
  }

  .app-shell__banner app-alert {
    display: flex;
    height: var(--banner-height);
  }

  .app-shell__banner app-alert p {
    flex: 1;
  }

  .app-shell__shell {
    display: grid;
    grid-template-columns: 80px minmax(0, 1fr);
    min-height: 100vh;
    background: var(--bg-dark);
  }

  .app-shell__banner + .app-shell__shell {
    min-height: calc(100vh - var(--banner-height));
  }

  .app-shell__main {
    min-width: 0;
    min-height: 100vh;
  }

  .app-shell__banner + .app-shell__shell > .app-shell__main {
    min-height: calc(100vh - var(--banner-height));
  }

  @media (max-width: 1024px) {
    .app-shell__shell {
      grid-template-columns: 1fr;
    }

    app-sidebar {
      display: none;
    }
  }

  @media (max-width: 768px) {
    .app-shell__banner {
      display: none;
    }

    .app-shell__shell {
      height: auto;
      min-height: unset;
      overflow: auto;
    }
  }
`;

export default appShellStyles;
