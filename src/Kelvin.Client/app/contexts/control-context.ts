import { createContext } from '@lit/context';

import type { ControlStateChange } from '../models/control-state-change.js';

// the control context will hold the latest overall control state
export const controlContext = createContext<Partial<ControlStateChange>>('control-state-change');

export const defaultControlStateChange: Partial<ControlStateChange> = {};

// the call context will only hold the latest call state, not the latest overall control state
export const callContext = createContext<Partial<ControlStateChange>>('call-state-change');

export const defaultCallStateChange: Partial<ControlStateChange> = {};
