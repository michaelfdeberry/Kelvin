export const events = {
  preferencesSaved: 'preferences-saved',
  sensorsUpdated: 'sensors-updated',
  devicesUpdated: 'devices-updated',
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
  },
  readingsHub: {
    sensorReadingsUpdated: 'signalr:readings-hub:sensor-readings-updated',
  },
} as const;
