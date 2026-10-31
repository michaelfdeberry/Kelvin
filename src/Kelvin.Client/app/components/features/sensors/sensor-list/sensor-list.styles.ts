import { css } from 'lit';

const sensorListStyles = css`
  :host {
    display: block;
    width: 100%;
  }

  .sensor-list__cards {
    width: 100%;
    gap: 1rem;
    display: grid;
    flex-wrap: unset;
    grid-template-columns: repeat(2, 1fr);

    > app-sensor-card {
      flex: unset;
      max-width: 100%;
    }
  }

  .sensor-list__cards > *:last-child:nth-child(odd) {
    grid-column: span 2;
  }

  @media (max-width: 768px) {
    .sensor-list__cards {
      display: flex;
      flex-direction: column;

      > app-sensor-card {
        flex: unset;
        max-width: 100%;
      }
    }
  }
`;

export default sensorListStyles;
