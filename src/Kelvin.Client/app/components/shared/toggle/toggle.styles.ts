import { css } from 'lit';

export default css`
  :host {
    display: inline-flex;
  }

  .toggle {
    position: relative;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    user-select: none;
  }

  .toggle__input {
    position: absolute;
    width: 1px;
    height: 1px;
    margin: 0;
    opacity: 0;
    pointer-events: none;
  }

  .toggle__slider {
    position: relative;
    display: inline-flex;
    align-items: center;
    width: 3.1rem;
    height: 1.8rem;
    border: 1px solid var(--accent-idle);
    border-radius: 999px;
    background: var(--surface-overlay-panel);
    box-shadow: inset 0 1px 0 var(--surface-overlay-light);
    transition:
      background-color 160ms ease,
      border-color 160ms ease,
      box-shadow 160ms ease;
  }

  .toggle__slider::before {
    content: '';
    position: absolute;
    left: 4px;
    right: 2px;
    width: 1.35rem;
    height: 1.35rem;
    border-radius: 50%;
    background: var(--text-on-primary-strong);
    box-shadow: 0 2px 8px var(--shadow-color);
    transition:
      transform 180ms ease,
      background-color 180ms ease,
      box-shadow 180ms ease;
  }

  .toggle__icon {
    position: absolute;
    top: 50%;
    right: 0.45rem;
    transform: translateY(-50%);
    z-index: 1;
    font-size: 0.74rem;
    font-weight: 700;
    color: var(--text-soft);
    transition:
      opacity 140ms ease,
      transform 180ms ease,
      color 140ms ease;
  }

  .toggle:not(.toggle--checked) .toggle__icon {
    opacity: 0.8;
  }

  .toggle--checked .toggle__icon {
    right: auto;
    left: 0.45rem;
    color: var(--text-on-primary-strong);
    opacity: 1;
  }

  .toggle:hover .toggle__slider {
    border-color: var(--accent-primary);
    box-shadow:
      inset 0 1px 0 var(--surface-overlay-light),
      0 6px 16px rgb(2 6 23 / 20%);
  }

  .toggle__input:focus-visible + .toggle__slider {
    border-color: var(--accent-info);
    box-shadow:
      0 0 0 2px rgb(147 197 253 / 24%),
      inset 0 1px 0 var(--surface-overlay-light);
  }

  .toggle__input:checked + .toggle__slider {
    border-color: var(--accent-primary-strong);
    background: linear-gradient(120deg, var(--accent-primary-strong), var(--accent-primary));
  }

  .toggle__input:checked + .toggle__slider::before {
    transform: translateX(1.3rem);
    background: var(--text-on-primary-strong);
    box-shadow: 0 4px 12px var(--shadow-color);
  }

  .toggle__input:disabled + .toggle__slider {
    opacity: 0.6;
    cursor: not-allowed;
    box-shadow: none;
  }

  .toggle__input:disabled ~ .toggle__icon {
    opacity: 0.55;
  }

  .toggle__label {
    margin-left: 0.5rem;
    font-size: 0.875rem;
    line-height: 1.25rem;
  }

  @media (prefers-reduced-motion: reduce) {
    .toggle__slider,
    .toggle__slider::before,
    .toggle__icon {
      transition: none;
    }
  }
`;
