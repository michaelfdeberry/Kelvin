import { css } from 'lit';

import badgeStyles from './styles/badge.styles.js';
import buttonStyles from './styles/button.styles.js';
import cardStyles from './styles/card.styles.js';
import checkboxStyles from './styles/checkbox.styles.js';
import formControlStyles from './styles/form-control.styles.js';
import inputStyles from './styles/input.styles.js';
import radiosStyles from './styles/radios.styles.js';
import tablesStyles from './styles/tables.styles.js';

const sharedStyles = css`
  :host,
  :host *,
  :host *::before,
  :host *::after {
    box-sizing: border-box;
  }

  ${buttonStyles}
  ${inputStyles}
  ${radiosStyles}
  ${checkboxStyles}
  ${formControlStyles} 
  ${cardStyles}
  ${badgeStyles}
  ${tablesStyles}
  
  .text-muted {
    color: var(--text-muted);
    font-size: 0.85rem;
  }

  /* Kiosk touchscreen has no reactive pointer - hide it everywhere, overriding any cursor set above. */
  :host-context([kiosk]),
  :host-context([kiosk]) * {
    cursor: none !important;
  }
`;

export default sharedStyles;
