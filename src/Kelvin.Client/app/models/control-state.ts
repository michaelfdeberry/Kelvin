import type { ControlStateChange, ControlState } from './control-state-change.js';

export interface ControlStateResponse {
  controlState: ControlState;
  controlSince?: string | null;
  callState: ControlState;
  callSince?: string | null;
  fanOn: boolean;
  fanSince?: string | null;
  lastChange?: ControlStateChange | null;
}