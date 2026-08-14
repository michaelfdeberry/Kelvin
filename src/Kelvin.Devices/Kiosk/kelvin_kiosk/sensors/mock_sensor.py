from __future__ import annotations

import math
from time import monotonic

from ..models import SensorReading
from .base import SensorReader


class MockSensorReader(SensorReader):
    def read(self) -> SensorReading:
        tick = monotonic() / 30.0
        return SensorReading(
            temperature_c=22.0 + math.sin(tick),
            humidity_percentage=45.0 + math.cos(tick) * 4.0,
            co2_level_ppm=650 + int(math.sin(tick / 2.0) * 40),
        )