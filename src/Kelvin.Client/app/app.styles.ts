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

  :host-context([kiosk]) .app-shell__shell {
    grid-template-columns: minmax(0, 1fr);
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

  ::view-transition-old(main-content) {
    animation: 250ms ease-out both fade-out;
  }

  ::view-transition-new(main-content) {
    animation: 300ms ease-in both fade-in;
  }

  @keyframes fade-in {
    from {
      opacity: 0;
      transform: translateY(8px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  @keyframes fade-out {
    from {
      opacity: 1;
    }
    to {
      opacity: 0;
      transform: translateY(-8px);
    }
  }
`;

export default appShellStyles;
