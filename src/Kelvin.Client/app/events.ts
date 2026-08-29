export const events = {
  preferencesSaved: 'preferences-saved',
  sensorsUpdated: 'sensors-updated',
  routeChanged: 'route-changed',
  alertDismissed: 'alert-dismissed',
  toast: 'toast',
  thermostatUpdated: 'thermostat-updated',
};

export const signalrEvents = {
  controlHub: {
    controlStateChanged: 'signalr:control-hub:control-state-changed',
    thermostatStateChanged: 'signalr:control-hub:thermostat-state-changed',
  },
  readingsHub: {
    sensorReadingsUpdated: 'signalr:readings-hub:sensor-readings-updated',
  },
} as const;
