import { css } from 'lit';

export default css`
  .card__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .settings-gateway__pins {
    display: flex;
    gap: 1rem;

    @media (max-width: 768px) {
      flex-direction: column;
      align-items: flex-start;
    }
  }
`;
