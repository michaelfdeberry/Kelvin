import { css } from 'lit';

export default css`
  :host {
    display: block;
    --slider-thumb-size: 3rem;
    --slider-track-color: var(--bg-panel);
    --slider-segment-start-color: var(--bg-panel);
    --slider-segment-end-color: var(--bg-panel);
    --slider-segment-middle-color: var(--slider-range);
    --temperature-thumb-accent: var(--accent-idle);
    padding: 0;
  }

  .slider__track {
    height: 0.875rem;
    border-radius: 999px;
    background: var(--slider-track-color);
  }

  .slider__track::before {
    content: '';
    position: absolute;
    inset: -1rem 0;
  }

  .slider__thumb {
    border: 4px solid var(--temperature-thumb-accent);
    background: var(--text-main);
    z-index: 1;
  }

  .slider__thumb--low {
    --temperature-thumb-accent: var(--slider-heat);
  }

  .slider__thumb--high {
    --temperature-thumb-accent: var(--slider-cool);
  }

  .slider__thumb:focus-visible {
    border-color: var(--temperature-thumb-accent);
  }

  .slider__bubble {
    display: none;
  }

  :host([range]) {
    --slider-segment-start-color: var(--slider-heat);
    --slider-segment-end-color: var(--slider-cool);
  }

  :host([cooling]:not([heating]):not([range])) {
    --slider-segment-end-color: var(--slider-cool);
    --temperature-thumb-accent: var(--slider-cool);
  }

  :host([heating]:not([range])) {
    --slider-segment-start-color: var(--slider-heat);
    --temperature-thumb-accent: var(--slider-heat);
  }

  .temperature-slider__readout {
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: 0.5rem 1.5rem;
    color: var(--text-muted);
    font-family: 'JetBrains Mono', monospace;
    font-size: 1.125rem;
    line-height: 1.5;
  }

  .temperature-slider__heating {
    color: var(--slider-heat);
  }

  .temperature-slider__cooling {
    color: var(--slider-cool);
  }

  .temperature-slider__scale {
    position: relative;
    margin-inline: calc(var(--slider-thumb-size) / 2);
    padding-top: 1.75rem;
  }

  .temperature-slider__scale--with-current {
    padding-top: 3.5rem;
  }

  .temperature-slider__current-marker {
    position: absolute;
    top: 1.5rem;
    left: var(--current-position);
    width: 2px;
    height: 1rem;
    background: var(--text-muted);
    transform: translateX(-50%);
    pointer-events: none;
  }

  .temperature-slider__current-label {
    position: absolute;
    top: 0;
    left: clamp(3.5rem, var(--current-position), calc(100% - 3.5rem));
    transform: translateX(-50%);
    white-space: nowrap;
    color: var(--text-muted);
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.875rem;
    line-height: 1.25rem;
    pointer-events: none;
  }

  .temperature-slider__limits {
    display: flex;
    justify-content: space-between;
    padding-top: 1.5rem;
    color: var(--text-muted);
    font-family: 'JetBrains Mono', monospace;
    font-size: 0.875rem;
    line-height: 1.5;
  }
`;
