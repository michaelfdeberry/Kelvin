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

  :host-context([kiosk]) {
    .sensor-list__cards {
      grid-template-columns: repeat(1, 1fr);
    }
  }

  @media (max-width: 768px) {
    .sensor-list__cards {
      display: flex;
      flex-direction: column;
    }
  }
`;

export default sensorListStyles;
