// The *Updated events carry the new data as their detail. They are dispatched both for server pushes (by the realtime hub) and by the
// client that made a change, so it updates its own state on success without waiting for the push to arrive.
// sensorsUpdated and devicesUpdated replace everything (what the server sends on connect); the *Changed and *Deleted events apply one item.
export const events = {
  preferencesSaved: 'preferences-saved',
  sensorsUpdated: 'sensors-updated',
  sensorChanged: 'sensor-changed',
  sensorDeleted: 'sensor-deleted',
  devicesUpdated: 'devices-updated',
  deviceChanged: 'device-changed',
  deviceDeleted: 'device-deleted',
  deviceStateChanged: 'device-state-changed',
  routeChanged: 'route-changed',
  alertDismissed: 'alert-dismissed',
  toast: 'toast',
  clearToast: 'clear-toast',
  thermostatUpdated: 'thermostat-updated',
};

export const signalrEvents = {
  controlHub: {
    controlStateChanged: 'signalr:control-hub:control-state-changed',
    callStateChanged: 'signalr:control-hub:call-state-changed',
    relayStatesChanged: 'signalr:control-hub:relay-states-changed',
  },
  readingsHub: {
    sensorReadingsUpdated: 'signalr:readings-hub:sensor-readings-updated',
  },
} as const;
