import { css } from 'lit';

const cardStyles = css`
  .card {
    border: 1px solid var(--border-subtle);
    border-radius: 14px;
    padding: 1rem;
    background: var(--surface-overlay);
  }

  .card__title {
    margin: 0;
    font-size: 1.1rem;
  }
`;

export default cardStyles;
