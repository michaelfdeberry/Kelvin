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
    justify-content: space-between;
    padding: 1rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    min-width: 0;
    min-height: 0;
    overflow-y: auto;
  }

  .dashboard-view__settings-button {
    display: block;
    width: 100%;
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

  .dashboard-view__settings-button {
    display: none;
  }

  @media (max-width: 768px) {
    .dashboard-view__main {
      justify-content: unset;
    }

    app-sensor-list {
      display: none;
    }

    app-thermostat-control {
      margin: 2rem 0;
      flex: 1;
    }

    .dashboard-view__settings-button {
      display: block;
      padding: 12px 24px;
      background: var(--bg-panel);
      border: 1px solid var(--border-subtle);
      border-radius: 30px;
      color: var(--text-main);
      font-size: 1rem;
      cursor: pointer;
      transition: 160ms;
      text-align: center;
      text-decoration: none;
    }
  }
`;

export default dashboardViewStyles;
