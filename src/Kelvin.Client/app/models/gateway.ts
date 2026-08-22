export type Gateway = {
  macAddress: string;
  heatingPin?: number;
  coolingPin?: number;
  fanPin?: number;
  controlPin?: number;
};

export type RelayStates = {
  heating?: boolean;
  cooling?: boolean;
  fan?: boolean;
  control?: boolean;
};
