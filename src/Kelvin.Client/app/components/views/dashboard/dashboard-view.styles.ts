import { css } from 'lit';

const dashboardViewStyles = css`
  :host {
    position: relative;
    display: block;
    height: 100%;
    min-height: 100%;
    color: var(--text-main);
  }

  .dashboard-view__shell {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 350px;
    height: 100%;
    min-height: 0;
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
    min-height: 0;
    overflow-y: auto;
    box-sizing: border-box;
  }

  .dashboard-view__alerts-demo {
    width: min(100%, 680px);
    display: grid;
    gap: 0.75rem;
  }

  .dashboard-view__modal-demo {
    width: min(100%, 680px);
  }

  .dashboard-view__modal-demo-text {
    margin: 0.5rem 0 1rem;
    color: var(--text-muted);
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
