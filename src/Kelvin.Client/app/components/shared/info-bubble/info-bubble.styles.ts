import { css } from 'lit';

export default css`
  :host {
    display: inline-flex;
    flex: none;
    vertical-align: middle;
  }

  *,
  *::before,
  *::after {
    box-sizing: border-box;
  }

  button {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 1.75rem;
    height: 1.75rem;
    padding: 0.25rem;
    border: 0;
    border-radius: 4px;
    background: transparent;
    color: var(--text-muted);
    cursor: pointer;
  }

  button:hover,
  button[aria-expanded='true'] {
    background: var(--surface-overlay-light);
    color: var(--text-main);
  }

  button:focus-visible {
    outline: 2px solid var(--control-border-color-focus);
    outline-offset: 2px;
  }

  button span,
  svg {
    display: block;
    width: 1.125rem;
    height: 1.125rem;
  }

  [popover] {
    position: fixed;
    inset: auto;
    width: min(20rem, calc(100vw - 2rem));
    max-height: calc(100dvh - 2rem);
    margin: 0;
    padding: 0.75rem 1rem;
    overflow: auto;
    border: 1px solid var(--border-subtle);
    border-radius: 6px;
    background: var(--bg-panel);
    color: var(--text-main);
    box-shadow: 0 4px 16px rgb(0 0 0 / 25%);
    font: inherit;
    font-size: 0.875rem;
    font-weight: normal;
    line-height: 1.5;
    letter-spacing: 0;
    overflow-wrap: anywhere;
    text-align: start;
  }
`;
