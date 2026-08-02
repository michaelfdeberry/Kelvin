import { css } from 'lit';

export default css`
  .toaster {
    position: fixed;
    bottom: 1rem;
    right: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    z-index: 1000;
    padding-left: 1rem;
    width: calc(100% - 2rem);
    max-width: 480px;
  }
`;
