import { css } from 'lit';

const kioskViewStyles = css`
  :host {
    display: block;
    height: 100%;
    min-height: 100%;
    color: var(--text-main);
  }

  .kiosk-view__refresh-button {
    position: absolute;
    top: 0.5rem;
    left: 0.5rem;
    z-index: 1;
    width: 2rem;
    height: 2rem;
  }

  .kiosk-view__refresh-button .button__icon {
    width: 1.1rem;
    height: 1.1rem;
  }

  .kiosk-view__shell {
    position: relative;
    display: grid;
    grid-template-columns: minmax(0, 65%) minmax(0, 1fr);
    height: 100%;
    min-height: 0;
    background: var(--bg-dark);
  }

  .kiosk-view__main {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    padding: 2rem;
    min-width: 0;
    min-height: 0;
    overflow: hidden;
  }

  .kiosk-view__weather {
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 1rem;
    overflow-y: auto;
  }
`;

export default kioskViewStyles;
