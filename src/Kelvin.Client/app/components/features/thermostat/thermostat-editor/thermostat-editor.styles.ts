import { css } from 'lit';

export default css`
  app-modal {
    --modal-body-padding: 0;
    --modal-heading-border-size: 0;
  }

  .thermostat-editor {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 300px;
    min-width: 0;
    overflow: hidden;
  }

  .thermostat-editor app-tabs {
    flex: 1;
    min-height: 0;
    min-width: 0;
    width: 100%;
    max-width: 100%;
  }
`;
