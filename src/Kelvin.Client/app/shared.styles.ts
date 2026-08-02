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
  ${buttonStyles}
  ${inputStyles}
  ${radiosStyles}
  ${checkboxStyles}
  ${formControlStyles} 
  ${cardStyles}
  ${badgeStyles}
  ${tablesStyles}
`;

export default sharedStyles;
