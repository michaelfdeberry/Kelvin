import { css } from 'lit';

const appSidebarStyles = css`
  :host {
    display: block;
    max-height: 100vh;
    position: sticky;
    top: 0;
  }

  .app-sidebar__nav {
    height: 100%;
    background: var(--bg-panel);
    border-right: 1px solid var(--border-subtle);
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 2rem 0;
    gap: 2rem;
    box-sizing: border-box;
  }

  .app-sidebar__link {
    width: 40px;
    height: 40px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 8px;
    background: var(--border-subtle);
    color: var(--text-main);
    text-decoration: none;
    font-weight: 700;
    transition:
      background-color 160ms ease,
      transform 160ms ease;
  }

  .app-sidebar__link:hover {
    background-color: color-mix(in oklab, var(--accent-heat), white 10%);
  }

  .app-sidebar__link--active {
    background: var(--accent-heat);
  }

  .app-sidebar__icon {
    line-height: 1;
  }

  .app-sidebar__label {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }
`;

export default appSidebarStyles;
