import { css } from 'lit';

const dashboardViewStyles = css`
  :host {
    display: block;
    min-height: 100vh;
    color: var(--text-main);
  }

  .dashboard-view__shell {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 350px;
    min-height: 100vh;
    background: var(--bg-dark);
  }

  .dashboard-view__main {
    padding: 1rem 2rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    min-width: 0;
    min-height: 100vh;
    overflow-y: auto;
    box-sizing: border-box;
  }

  .dashboard-view__stats {
    background: var(--bg-panel);
    border-left: 1px solid var(--border-subtle);
    overflow-y: auto;
  }

  @media (max-width: 1024px) {
    .dashboard-view__shell {
      grid-template-columns: 1fr;
    }

    .dashboard-view__stats {
      display: none;
    }
  }

  @media (max-height: 480px) {
    .dashboard-view__main {
      padding: 0.5rem 1rem;
    }
  }
`;

export default dashboardViewStyles;
