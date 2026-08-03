import { css } from 'lit';

const cardStyles = css`
  .card {
    border: 1px solid var(--border-subtle);
    border-radius: var(--border-radius);
    padding: 1rem;
    background: var(--surface-overlay);
    margin-bottom: 1rem;
  }

  .card__title {
    margin: 0;
    font-size: 1.1rem;
  }

  .card__description {
    margin: 0.5rem 0 0;
    font-size: 0.9rem;
    color: var(--text-muted);
  }
`;

export default cardStyles;
