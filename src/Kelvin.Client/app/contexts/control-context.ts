import { createContext } from '@lit/context';

import type { ControlStateChange } from '../models/control-state-change.js';

export const controlContext = createContext<Partial<ControlStateChange>>('control-state-change');

export const defaultControlStateChange: Partial<ControlStateChange> = {};
