import { css } from 'lit';

export default css`
  :host {
    display: block;
    --slider-thumb-size: 1.1rem;
    padding-block: calc(var(--slider-thumb-size) / 2);
  }

  :host([disabled]) {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .slider {
    position: relative;
    width: 100%;
  }

  .slider__track {
    position: relative;
    height: 0.35rem;
    border-radius: var(--border-radius);
    background: var(--slider-track-color, var(--accent-idle));
    touch-action: none;
  }

  :host([disabled]) .slider__track {
    pointer-events: none;
  }

  .slider__segment {
    position: absolute;
    top: 0;
    bottom: 0;
    border-radius: inherit;
  }

  .slider__segment--start {
    left: 0;
    background: var(--slider-segment-start-color, var(--accent-primary));
  }

  :host([range]) .slider__segment--start {
    background: var(--slider-segment-start-color, var(--slider-track-color, var(--accent-idle)));
  }

  .slider__segment--middle {
    background: var(--slider-segment-middle-color, var(--accent-primary));
  }

  .slider__segment--end {
    background: var(--slider-segment-end-color, var(--accent-idle));
  }

  :host(:not([range])) .slider__segment--end {
    background: var(--slider-segment-end-color, var(--slider-track-color, var(--accent-idle)));
  }

  .slider__thumb {
    position: absolute;
    top: 50%;
    width: var(--slider-thumb-size);
    height: var(--slider-thumb-size);
    transform: translate(-50%, -50%);
    border-radius: 50%;
    background: var(--text-on-primary-strong);
    border: 1px solid var(--accent-primary-strong);
    box-shadow: 0 2px 8px var(--shadow-color);
    cursor: pointer;
    transition:
      box-shadow 140ms ease,
      transform 140ms ease;
  }

  :host([disabled]) .slider__thumb {
    pointer-events: none;
    cursor: not-allowed;
  }

  .slider__thumb:hover {
    box-shadow:
      0 2px 8px var(--shadow-color),
      0 0 0 6px var(--surface-overlay-light);
  }

  .slider__thumb:focus-visible {
    outline: none;
    border-color: var(--accent-info);
    box-shadow: 0 0 0 3px rgb(147 197 253 / 24%);
  }

  .slider__thumb--active {
    z-index: 2;
  }

  .slider__bubble {
    position: absolute;
    bottom: calc(100% + 0.5rem);
    left: 50%;
    transform: translateX(-50%);
    padding: 0.2rem 0.5rem;
    border-radius: var(--border-radius);
    border: 1px solid var(--border-subtle);
    background: var(--bg-panel);
    color: var(--text-main);
    font-size: 0.75rem;
    white-space: nowrap;
    pointer-events: none;
    box-shadow: 0 4px 12px var(--shadow-color);
  }

  @media (prefers-reduced-motion: reduce) {
    .slider__thumb {
      transition: none;
    }
  }
`;
