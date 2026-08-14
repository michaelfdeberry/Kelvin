from __future__ import annotations

from dataclasses import dataclass
from time import monotonic

from .config import KioskConfig
from .models import SensorReading


@dataclass
class ChangeDetector:
    config: KioskConfig
    last_reading: SensorReading | None = None
    last_sent_at: float = 0.0

    def should_send(self, reading: SensorReading) -> bool:
        now = monotonic()

        if self.last_reading is None:
            self.last_reading = reading
            self.last_sent_at = now
            return True

        has_temp_change = abs(reading.temperature_c - self.last_reading.temperature_c) >= self.config.threshold_temperature_c
        has_humidity_change = abs(reading.humidity_percentage - self.last_reading.humidity_percentage) >= self.config.threshold_humidity_percent
        has_co2_change = abs(reading.co2_level_ppm - self.last_reading.co2_level_ppm) >= self.config.threshold_co2_ppm
        is_heartbeat_time = (now - self.last_sent_at) >= self.config.heartbeat_seconds

        should_send = has_temp_change or has_humidity_change or has_co2_change or is_heartbeat_time
        if should_send:
            self.last_reading = reading
            self.last_sent_at = now

        return should_send