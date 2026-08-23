from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class SensorReading:
    temperature_c: float
    humidity_percentage: float
    co2_level_ppm: int = 0
    battery_level_percentage: float | None = None

    def to_sensor_packet_payload(self, mac_address: str) -> dict[str, object]:
        return {
            "sensorPacket": {
                "macAddress": mac_address,
                "temperatureC": self.temperature_c,
                "humidityPercentage": self.humidity_percentage,
                "co2LevelPpm": self.co2_level_ppm,
                "batteryLevelPercentage": self.battery_level_percentage,
            }
        }