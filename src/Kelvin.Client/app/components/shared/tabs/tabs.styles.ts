import { css } from 'lit';

export default css`
  :host {
    display: flex;
    flex-direction: column;
    min-height: 0;
    min-width: 0;
    width: 100%;
    max-width: 100%;
  }

  .tabs {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
    min-width: 0;
    width: 100%;
    max-width: 100%;
  }

  .tabs__tablist-wrapper {
    display: flex;
    flex-direction: row;
    align-items: stretch;
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
    min-width: 0;
    width: 100%;
    max-width: 100%;
    position: sticky;
    top: 0;
    z-index: 1;
    background: var(--bg-panel);
  }

  .tabs__tablist {
    display: flex;
    flex-direction: row;
    justify-content: flex-start;
    padding: 0 1rem;
    gap: 1rem;
    flex: 1;
    min-width: 0;
    overflow-x: auto;
    overflow-y: hidden;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .tabs__tablist::-webkit-scrollbar {
    display: none;
  }

  .tabs__scroll-button {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 1.75rem;
    border: 0;
    background: var(--bg-panel);
    color: var(--text-muted);
    font-size: 1.1rem;
    line-height: 1;
    cursor: pointer;
    padding: 0;
  }

  .tabs__scroll-button:hover {
    color: var(--text-main);
  }

  .tabs__scroll-button[hidden] {
    display: none;
  }

  .tabs__panels {
    display: flex;
    flex-direction: column;
    position: relative;
    flex: 1;
    min-height: 0;
    overflow: hidden;
  }

  slot[name='tab']::slotted(.tab) {
    background: none;
    border: 0;
    color: var(--text-muted);
    padding: 0.5rem;
    font-size: 0.95rem;
    font-weight: 500;
    cursor: pointer;
    border-bottom: 2px solid transparent;
    transition: all 0.2s;
    white-space: nowrap;
    flex: 0 0 auto;
  }

  slot[name='tab']::slotted(.tab):hover {
    color: var(--text-main);
  }

  slot[name='tab']::slotted(.tab.tab--selected) {
    color: var(--text-main);
    border-bottom-color: var(--accent-primary);
  }

  slot[name='panel']::slotted(.panel) {
    display: none;
  }

  slot[name='panel']::slotted(.panel.panel--selected) {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
  }
`;
