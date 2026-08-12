const apiResources = {
  control: {
    getControlHistory: '/api/control/history',
    getControlState: '/api/control/state',
    getControlStats: '/api/control/stats',
  },
  gateways: {
    getGateway: '/api/gateway',
    updateGateway: '/api/gateway',
    getRelayStates: '/api/gateway/relays/states',
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
    getLatestReadings: '/api/sensors/readings/latest',
    getSensors: '/api/sensors',
    restoreSensor: '/api/sensors/{id}',
    updateSensor: '/api/sensors/{id:guid}',
  },
  thermostat: {
    createSchedule: '/api/thermostat/schedules',
    createSetPoint: '/api/thermostat/set-points',
    getSchedules: '/api/thermostat/schedules',
    getSetPoints: '/api/thermostat/set-points',
    getThermostat: '/api/thermostat',
    updateSchedule: '/api/thermostat/schedules/{id:guid}',
    updateSetPoint: '/api/thermostat/set-points/{id:guid}',
    updateThermostat: '/api/thermostat',
    updateThermostatSettings: '/api/thermostat/settings',
  },
  weather: {
    getWeatherForecast: '/api/weather/forecast',
  },
} as const;

type LeafValues<T> = T extends string ? T : T extends Record<string, unknown> ? { [K in keyof T]: LeafValues<T[K]> }[keyof T] : never;

export type ApiResourcePath = LeafValues<typeof apiResources>;
export type ApiRouteParams = Record<string, string | number>;

export default apiResources;
