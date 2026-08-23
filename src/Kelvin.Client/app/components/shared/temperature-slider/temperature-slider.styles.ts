import { css } from 'lit';

export default css`
  :host {
    display: block;
    margin-bottom: 1.25rem;
    padding-block: 0;
  }

  .slider__track {
    height: 2.5rem;
    border: 1px solid var(--accent-idle);
    background: var(--bg-dark);
  }

  .slider__thumb {
    width: 10px;
    height: 100%;
    border-radius: 4px;
  }

  :host([heating][cooling]) {
    .slider__segment--start {
      background: var(--slider-segment-start-color, var(--accent-heat));
    }

    .slider__segment--middle {
      background: var(--slider-segment-middle-color, var(--border-subtle));
    }

    .slider__segment--end {
      background: var(--slider-segment-end-color, var(--accent-primary));
    }
  }

  :host([cooling]:not([heating])) {
    .slider__segment--start {
      background: var(--slider-segment-start-color, var(--border-subtle));
    }

    .slider__segment--end {
      background: var(--slider-segment-end-color, var(--accent-primary));
    }
  }

  :host([heating]:not([cooling])) {
    .slider__segment--start {
      background: var(--slider-segment-start-color, var(--accent-heat));
    }

    .slider__segment--end {
      background: var(--slider-segment-end-color, var(--border-subtle));
    }
  }
`;
