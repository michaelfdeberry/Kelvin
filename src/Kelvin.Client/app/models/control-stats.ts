export type ControlStats = {
  from: string;
  to: string;
  heatingSeconds: number;
  coolingSeconds: number;
  dwellSeconds: number;
  controlledSeconds: number;
  revertedSeconds: number;
  fanSeconds: number;
  heatingCycles: number;
  coolingCycles: number;
  averageHeatingCycleSeconds?: number | null;
  averageCoolingCycleSeconds?: number | null;
};
