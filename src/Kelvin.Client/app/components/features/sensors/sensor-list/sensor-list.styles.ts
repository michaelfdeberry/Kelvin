import { css } from 'lit';

const sensorListStyles = css`
  :host {
    display: block;
    width: 100%;
    max-width: 800px;
  }

  .sensor-list__cards {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 1rem;
    width: 100%;

    &.sensor-list__cards--kiosk {
      max-width: 400px;
      margin: 0 auto;
    }
  }

  @media (max-height: 480px) {
    :host {
      display: none;
    }
  }
`;

export default sensorListStyles;
