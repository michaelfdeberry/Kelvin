const apiResources = {
  control: {
    getControlHistory: '/api/control/history',
    getControlState: '/api/control/state',
    getControlStats: '/api/control/stats',
  },
  devices: {
    deleteDevice: '/api/devices/{id}',
    disableDevice: '/api/devices/{deviceId}/disable',
    enableDevice: '/api/devices/{deviceId}/enable',
    getDevices: '/api/devices',
    getDeviceStates: '/api/devices/state',
    restoreDevice: '/api/devices/{id}',
    updateDevice: '/api/devices/{id}',
  },
  gateways: {
    getGateway: '/api/gateway',
    updateGateway: '/api/gateway',
    getRelayStates: '/api/gateway/relays/states',
  },
  humidifiers: {
    getHumidifiers: '/api/humidifiers',
    updateHumidifierConfiguration: '/api/humidifiers/{id}/configuration',
  },
  locations: {
    getCurrentLocation: '/api/locations/current',
    searchLocations: '/api/locations/search',
    setCurrentLocation: '/api/locations/current',
  },
  preferences: {
    getPreferences: '/api/preferences',
    updatePreferences: '/api/preferences',
  },
  sensors: {
    deleteSensor: '/api/sensors/{id}',
    disableSensor: '/api/sensors/{sensorId}/disable',
    enableSensor: '/api/sensors/{sensorId}/enable',
    getLatestReading: '/api/sensors/readings/latest',
    getLatestSensorReadingsBefore: '/api/sensors/readings/latest-before',
    getSensorHistory: '/api/sensors/readings/history',
    getSensorHistoryPeriods: '/api/sensors/readings/periods',
    getSensors: '/api/sensors',
    restoreSensor: '/api/sensors/{id}',
    updateSensor: '/api/sensors/{id:guid}',
  },
  thermostat: {
    getSchedules: '/api/thermostat/schedules',
    getSetPoints: '/api/thermostat/set-points',
    getThermostat: '/api/thermostat',
    updateThermostat: '/api/thermostat',
    updateThermostatConfiguration: '/api/thermostat/configuration',
    updateThermostatSettings: '/api/thermostat/settings',
  },
  weather: {
    getWeatherForecast: '/api/weather/forecast',
  },
  notifications: {
    dismissNotification: '/api/notifications/{id}',
  },
} as const;

type LeafValues<T> = T extends string ? T : T extends Record<string, unknown> ? { [K in keyof T]: LeafValues<T[K]> }[keyof T] : never;

export type ApiResourcePath = LeafValues<typeof apiResources>;
export type ApiRouteParams = Record<string, string | number>;

export default apiResources;
